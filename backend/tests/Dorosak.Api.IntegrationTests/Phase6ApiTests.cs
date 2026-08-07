using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Dorosak.Application.Common.Results;
using Dorosak.Application.Features.Identity;
using Dorosak.Application.Features.Phase6;
using Dorosak.Infrastructure.Identity;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Dorosak.Api.IntegrationTests;

[Collection(ApiTestGroup.Name)]
public sealed class Phase6ApiTests(ApiFixture fixture)
{
    [Fact]
    public async Task PublicCatalog_IsReleaseBackedAndCursorBoundToCanonicalQuery()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using HttpRequestMessage categoriesRequest = new(
            HttpMethod.Get,
            "/api/v1/catalog/categories?limit=1");
        categoriesRequest.Headers.AcceptLanguage.ParseAdd("en");
        using HttpResponseMessage categories = await fixture.Client.SendAsync(categoriesRequest, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, categories.StatusCode);
        using JsonDocument categoriesDocument = JsonDocument.Parse(
            await categories.Content.ReadAsStringAsync(cancellationToken));
        JsonElement categoryData = categoriesDocument.RootElement.GetProperty("data");
        Assert.Equal(1, categoryData.GetProperty("items").GetArrayLength());
        string cursor = Assert.IsType<string>(categoryData.GetProperty("nextCursor").GetString());

        using HttpRequestMessage mismatchedCursorRequest = new(
            HttpMethod.Get,
            $"/api/v1/catalog/categories?limit=2&cursor={Uri.EscapeDataString(cursor)}");
        mismatchedCursorRequest.Headers.AcceptLanguage.ParseAdd("en");
        using HttpResponseMessage mismatched = await fixture.Client.SendAsync(mismatchedCursorRequest, cancellationToken);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, mismatched.StatusCode);
        Assert.Equal("CURSOR.INVALID", await ReadProblemCodeAsync(mismatched, cancellationToken));

        using HttpResponseMessage catalog = await fixture.Client.GetAsync(
            "/api/v1/catalog/courses?limit=24",
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, catalog.StatusCode);
        using JsonDocument catalogDocument = JsonDocument.Parse(
            await catalog.Content.ReadAsStringAsync(cancellationToken));
        JsonElement catalogData = catalogDocument.RootElement.GetProperty("data");
        Assert.Equal(0, catalogData.GetProperty("items").GetArrayLength());
        Assert.False(catalogData.GetProperty("hasMore").GetBoolean());
        Assert.Equal(JsonValueKind.Null, catalogData.GetProperty("nextCursor").ValueKind);
    }

    [Fact]
    public async Task Search_NormalizesContractAndRejectsOpaqueCursorTampering()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using HttpResponseMessage empty = await fixture.Client.GetAsync(
            "/api/v1/search?q=%D8%A5%D9%90%D8%AF%D8%A7%D8%B1%D8%A9",
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, empty.StatusCode);
        using JsonDocument document = JsonDocument.Parse(await empty.Content.ReadAsStringAsync(cancellationToken));
        Assert.Equal(0, document.RootElement.GetProperty("data").GetProperty("items").GetArrayLength());
        Assert.Contains(
            "noindex",
            empty.Headers.TryGetValues("X-Robots-Tag", out IEnumerable<string>? robots) ? string.Join(',', robots) : string.Empty,
            StringComparison.OrdinalIgnoreCase);

        using HttpResponseMessage invalid = await fixture.Client.GetAsync(
            "/api/v1/search?q=backend&cursor=invalid.cursor",
            cancellationToken);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, invalid.StatusCode);
        Assert.Equal("CURSOR.INVALID", await ReadProblemCodeAsync(invalid, cancellationToken));

        using HttpResponseMessage detail = await fixture.Client.GetAsync(
            "/api/v1/catalog/courses/private-draft",
            cancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, detail.StatusCode);
    }

    [Fact]
    public async Task InstructorEndpoints_EnforceIfMatchAndHideOtherOwners()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        SignedInTeacher owner = await CreateApprovedTeacherAsync("owner", cancellationToken);
        SignedInTeacher otherTeacher = await CreateApprovedTeacherAsync("other", cancellationToken);
        SignedInTeacher student = await CreateStudentAsync("student", cancellationToken);

        Guid courseId;
        await using (AsyncServiceScope scope = fixture.Factory.Services.CreateAsyncScope())
        {
            ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();
            Result<CourseMutationResponse> created = await sender.Send(
                new CreateCourseCommand(
                    owner.UserId,
                    "en",
                    "Beginner",
                    [new CourseLocalizationInput(
                        "en",
                        "Private Owner Course",
                        "Draft metadata",
                        "This course is not a public release.")],
                    ["technology"],
                    []),
                cancellationToken);
            Assert.True(created.IsSuccess);
            courseId = created.Value.CourseId;
        }

        using (HttpRequestMessage idorRequest = Authorized(
                   HttpMethod.Get,
                   $"/api/v1/instructor/courses/{courseId:D}",
                   otherTeacher.AccessToken))
        using (HttpResponseMessage idor = await fixture.Client.SendAsync(idorRequest, cancellationToken))
        {
            Assert.Equal(HttpStatusCode.NotFound, idor.StatusCode);
        }

        using (HttpRequestMessage studentRequest = Authorized(
                   HttpMethod.Get,
                   "/api/v1/instructor/courses",
                   student.AccessToken))
        using (HttpResponseMessage denied = await fixture.Client.SendAsync(studentRequest, cancellationToken))
        {
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        }

        using (HttpRequestMessage getRequest = Authorized(
                   HttpMethod.Get,
                   $"/api/v1/instructor/courses/{courseId:D}",
                   owner.AccessToken))
        using (HttpResponseMessage current = await fixture.Client.SendAsync(getRequest, cancellationToken))
        {
            string currentBody = await current.Content.ReadAsStringAsync(cancellationToken);
            Assert.True(current.StatusCode == HttpStatusCode.OK, currentBody);
            Assert.Equal("\"v1\"", current.Headers.ETag?.Tag);
            using JsonDocument detailsDocument = JsonDocument.Parse(currentBody);
            JsonElement details = detailsDocument.RootElement.GetProperty("data");
            Assert.Equal("Beginner", details.GetProperty("level").GetString());
            Assert.Equal("technology", details.GetProperty("categoryCodes")[0].GetString());
            Assert.Equal(0, details.GetProperty("tagCodes").GetArrayLength());
        }

        var metadata = new
        {
            defaultLocale = "en",
            level = "Beginner",
            localizations = new[]
            {
                new
                {
                    locale = "en",
                    title = "Private Owner Course",
                    subtitle = "Updated metadata",
                    description = "This course is still a private draft.",
                },
            },
            categoryCodes = new[] { "technology" },
            tagCodes = Array.Empty<string>(),
        };

        using (HttpRequestMessage missingIfMatch = Authorized(
                   HttpMethod.Patch,
                   $"/api/v1/instructor/courses/{courseId:D}",
                   owner.AccessToken))
        {
            missingIfMatch.Content = JsonContent.Create(metadata);
            using HttpResponseMessage response = await fixture.Client.SendAsync(missingIfMatch, cancellationToken);
            Assert.Equal((HttpStatusCode)428, response.StatusCode);
        }

        using (HttpRequestMessage stale = Authorized(
                   HttpMethod.Patch,
                   $"/api/v1/instructor/courses/{courseId:D}",
                   owner.AccessToken))
        {
            stale.Headers.IfMatch.ParseAdd("\"v99\"");
            stale.Content = JsonContent.Create(metadata);
            using HttpResponseMessage response = await fixture.Client.SendAsync(stale, cancellationToken);
            Assert.Equal(HttpStatusCode.PreconditionFailed, response.StatusCode);
            Assert.Equal("\"v1\"", response.Headers.ETag?.Tag);
        }

        using (HttpRequestMessage update = Authorized(
                   HttpMethod.Patch,
                   $"/api/v1/instructor/courses/{courseId:D}",
                   owner.AccessToken))
        {
            update.Headers.IfMatch.ParseAdd("\"v1\"");
            update.Content = JsonContent.Create(metadata);
            using HttpResponseMessage response = await fixture.Client.SendAsync(update, cancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("\"v2\"", response.Headers.ETag?.Tag);
        }

        using (HttpRequestMessage transfer = Authorized(
                   HttpMethod.Put,
                   $"/api/v1/instructor/courses/{courseId:D}/owner",
                   owner.AccessToken))
        {
            transfer.Headers.IfMatch.ParseAdd("\"v2\"");
            transfer.Content = JsonContent.Create(new { newOwnerUserId = otherTeacher.UserId });
            using HttpResponseMessage response = await fixture.Client.SendAsync(transfer, cancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("\"v3\"", response.Headers.ETag?.Tag);
        }

        using (HttpRequestMessage previousOwnerRequest = Authorized(
                   HttpMethod.Get,
                   $"/api/v1/instructor/courses/{courseId:D}",
                   owner.AccessToken))
        using (HttpResponseMessage hidden = await fixture.Client.SendAsync(previousOwnerRequest, cancellationToken))
        {
            Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        }

        using (HttpRequestMessage newOwnerRequest = Authorized(
                   HttpMethod.Get,
                   $"/api/v1/instructor/courses/{courseId:D}",
                   otherTeacher.AccessToken))
        using (HttpResponseMessage visible = await fixture.Client.SendAsync(newOwnerRequest, cancellationToken))
        {
            Assert.Equal(HttpStatusCode.OK, visible.StatusCode);
            Assert.Equal("\"v3\"", visible.Headers.ETag?.Tag);
        }
    }

    private async Task<SignedInTeacher> CreateApprovedTeacherAsync(string prefix, CancellationToken cancellationToken)
    {
        Guid teacherId = await CreateUserAsync(prefix, assignTeacher: false, cancellationToken: cancellationToken);
        Guid reviewerId = await CreateUserAsync($"{prefix}-reviewer", assignTeacher: false, cancellationToken: cancellationToken);
        await using (AsyncServiceScope scope = fixture.Factory.Services.CreateAsyncScope())
        {
            ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();
            Result<TeacherApplicationResponse> application = await sender.Send(
                new SubmitTeacherApplicationCommand(
                    teacherId,
                    "Approved instructor",
                    "An experienced production software engineer.",
                    "Backend systems and PostgreSQL",
                    "I want to teach reliable backend engineering."),
                cancellationToken);
            Assert.True(application.IsSuccess);
            Assert.True((await sender.Send(
                new ReviewTeacherApplicationCommand(reviewerId, application.Value.Id, "start", null),
                cancellationToken)).IsSuccess);
            Assert.True((await sender.Send(
                new ReviewTeacherApplicationCommand(reviewerId, application.Value.Id, "approve", null),
                cancellationToken)).IsSuccess);
        }
        return await SignInAsync(teacherId, prefix, cancellationToken);
    }

    private async Task<SignedInTeacher> CreateStudentAsync(string prefix, CancellationToken cancellationToken)
    {
        Guid userId = await CreateUserAsync(prefix, assignTeacher: false, cancellationToken: cancellationToken);
        return await SignInAsync(userId, prefix, cancellationToken);
    }

    private async Task<Guid> CreateUserAsync(string prefix, bool assignTeacher, CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = fixture.Factory.Services.CreateAsyncScope();
        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        string email = $"{prefix}-{Guid.CreateVersion7():N}@example.test";
        ApplicationUser user = ApplicationUser.Create(prefix, email, DateTimeOffset.UtcNow);
        user.EmailConfirmed = true;
        IdentityResult created = await userManager.CreateAsync(user, "correct horse battery staple");
        Assert.True(created.Succeeded);
        Assert.True((await userManager.AddToRoleAsync(
            user,
            Dorosak.Infrastructure.Identity.IdentityConstants.StudentRole)).Succeeded);
        if (assignTeacher)
        {
            Assert.True((await userManager.AddToRoleAsync(
                user,
                Dorosak.Infrastructure.Identity.IdentityConstants.TeacherRole)).Succeeded);
        }
        _ = cancellationToken;
        return user.Id;
    }

    private async Task<SignedInTeacher> SignInAsync(Guid userId, string prefix, CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = fixture.Factory.Services.CreateAsyncScope();
        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        ApplicationUser user = Assert.IsType<ApplicationUser>(await userManager.FindByIdAsync(userId.ToString("D")));
        ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();
        Result<SignInResponse> signedIn = await sender.Send(
            new SignInCommand(
                user.Email!,
                "correct horse battery staple",
                new IdentityRequestContext("198.51.100.20", $"Phase6 {prefix}", "en")),
            cancellationToken);
        Assert.True(signedIn.IsSuccess);
        Assert.NotNull(signedIn.Value.Session);
        return new SignedInTeacher(userId, signedIn.Value.Session!.AccessToken);
    }

    private static HttpRequestMessage Authorized(HttpMethod method, string path, string accessToken)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.TryAddWithoutValidation("X-Forwarded-For", "198.51.100.200");
        return request;
    }

    private static async Task<string> ReadProblemCodeAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return Assert.IsType<string>(document.RootElement.GetProperty("code").GetString());
    }

    private sealed record SignedInTeacher(Guid UserId, string AccessToken);
}
