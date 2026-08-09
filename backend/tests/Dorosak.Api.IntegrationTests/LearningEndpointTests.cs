using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Dorosak.Application.Common.Results;
using Dorosak.Application.Features.Identity;
using Dorosak.Application.Features.Phase6;
using Dorosak.Application.Features.Publishing;
using Dorosak.Infrastructure.Identity;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Dorosak.Api.IntegrationTests;

[Collection(ApiTestGroup.Name)]
public sealed class LearningEndpointTests(ApiFixture fixture)
{
    [Fact]
    public async Task LearningManifest_HidesEnrollmentFromOtherUsersAndSuspendedLearners()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        SignedInUser teacher = await CreateApprovedTeacherAsync("learning-api-teacher", cancellationToken);
        SignedInUser reviewer = await CreateUserAsync("learning-api-reviewer", cancellationToken);
        SignedInUser learner = await CreateUserAsync("learning-api-learner", cancellationToken);
        SignedInUser otherLearner = await CreateUserAsync("learning-api-other", cancellationToken);
        Guid courseId = await CreatePublishedCourseAsync(teacher.UserId, reviewer.UserId, cancellationToken);

        using HttpRequestMessage enrollRequest = Authorized(
            HttpMethod.Post,
            "/api/v1/commerce/demo-checkout",
            learner.AccessToken);
        enrollRequest.Headers.Add("Idempotency-Key", Guid.CreateVersion7().ToString("N"));
        enrollRequest.Content = JsonContent.Create(new { courseId, outcome = "success" });
        using HttpResponseMessage enrolled = await fixture.Client.SendAsync(enrollRequest, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, enrolled.StatusCode);
        Guid enrollmentId = await ReadGuidAsync(enrolled, "enrollmentId", cancellationToken);

        using HttpRequestMessage createReviewRequest = Authorized(HttpMethod.Post, $"/api/v1/courses/{courseId:D}/reviews", learner.AccessToken);
        createReviewRequest.Headers.Add("Idempotency-Key", Guid.CreateVersion7().ToString("N"));
        createReviewRequest.Content = JsonContent.Create(new { rating = 5, text = "Strong release contract." });
        using HttpResponseMessage createdReview = await fixture.Client.SendAsync(createReviewRequest, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, createdReview.StatusCode);
        Guid reviewId = await ReadGuidAsync(createdReview, "id", cancellationToken);
        using HttpResponseMessage publicReviews = await fixture.Client.GetAsync($"/api/v1/catalog/courses/{courseId:D}/reviews", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, publicReviews.StatusCode);
        using HttpRequestMessage foreignReviewUpdate = Authorized(HttpMethod.Put, $"/api/v1/courses/{courseId:D}/reviews/{reviewId:D}", otherLearner.AccessToken);
        foreignReviewUpdate.Content = JsonContent.Create(new { rating = 1, text = "Tampered" });
        using HttpResponseMessage deniedReviewUpdate = await fixture.Client.SendAsync(foreignReviewUpdate, cancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, deniedReviewUpdate.StatusCode);

        using HttpRequestMessage learnerManifestRequest = Authorized(
            HttpMethod.Get,
            $"/api/v1/learning/enrollments/{enrollmentId:D}/manifest",
            learner.AccessToken);
        using HttpResponseMessage learnerManifest = await fixture.Client.SendAsync(learnerManifestRequest, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, learnerManifest.StatusCode);

        using HttpRequestMessage otherManifestRequest = Authorized(
            HttpMethod.Get,
            $"/api/v1/learning/enrollments/{enrollmentId:D}/manifest",
            otherLearner.AccessToken);
        using HttpResponseMessage otherManifest = await fixture.Client.SendAsync(otherManifestRequest, cancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, otherManifest.StatusCode);

        await using (var connection = new NpgsqlConnection(fixture.DatabaseConnection))
        {
            await connection.OpenAsync(cancellationToken);
            await using NpgsqlCommand command = new(
                "UPDATE learning.enrollments SET status = 'Suspended' WHERE id = @id",
                connection);
            command.Parameters.AddWithValue("id", enrollmentId);
            Assert.Equal(1, await command.ExecuteNonQueryAsync(cancellationToken));
        }

        using HttpRequestMessage suspendedManifestRequest = Authorized(
            HttpMethod.Get,
            $"/api/v1/learning/enrollments/{enrollmentId:D}/manifest",
            learner.AccessToken);
        using HttpResponseMessage suspendedManifest = await fixture.Client.SendAsync(suspendedManifestRequest, cancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, suspendedManifest.StatusCode);
    }

    [Fact]
    public async Task AdminPublish_RejectsTeacherEvenWithAuditAndIdempotencyHeaders()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        SignedInUser teacher = await CreateApprovedTeacherAsync("publish-security-teacher", cancellationToken);

        using HttpRequestMessage request = Authorized(
            HttpMethod.Post,
            $"/api/v1/admin/courses/{Guid.CreateVersion7():D}/publish",
            teacher.AccessToken);
        request.Headers.Add("Idempotency-Key", Guid.CreateVersion7().ToString("N"));
        request.Headers.Add("X-Audit-Reason", "Security integration check");
        using HttpResponseMessage response = await fixture.Client.SendAsync(request, cancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private async Task<Guid> CreatePublishedCourseAsync(
        Guid teacherId,
        Guid reviewerId,
        CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = fixture.Factory.Services.CreateAsyncScope();
        ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();
        Result<CourseMutationResponse> created = await sender.Send(
            new CreateCourseCommand(
                teacherId,
                "en",
                "Beginner",
                [new CourseLocalizationInput(
                    "en",
                    "API Learning Course",
                    "A private learning API fixture",
                    "A complete article course used to test resource authorization.")],
                [],
                []),
            cancellationToken);
        Assert.True(created.IsSuccess);
        Result<CourseMutationResponse> curriculum = await sender.Send(
            new UpdateCurriculumCommand(
                teacherId,
                created.Value.CourseId,
                1,
                [new SectionInput(
                    null,
                    0,
                    "First section",
                    [new LessonInput(null, 0, "First lesson", "Article", "Safe article content.")])]),
            cancellationToken);
        Assert.True(curriculum.IsSuccess);
        Result<PublicationStatusResponse> submitted = await sender.Send(
            new RequestPublicationCommand(teacherId, created.Value.CourseId),
            cancellationToken);
        Assert.True(submitted.IsSuccess);
        Result<PublicationReviewResponse> approved = await sender.Send(
            new ReviewPublicationCommand(reviewerId, submitted.Value.ReviewId!.Value, "approve", null),
            cancellationToken);
        Assert.True(approved.IsSuccess);
        Result<CourseReleaseResponse> published = await sender.Send(
            new PublishCourseCommand(
                reviewerId,
                created.Value.CourseId,
                Guid.CreateVersion7().ToString("N"),
                "API learning fixture approved"),
            cancellationToken);
        Assert.True(published.IsSuccess);
        return created.Value.CourseId;
    }

    private async Task<SignedInUser> CreateApprovedTeacherAsync(string prefix, CancellationToken cancellationToken)
    {
        SignedInUser teacher = await CreateUserAsync(prefix, cancellationToken);
        SignedInUser reviewer = await CreateUserAsync($"{prefix}-reviewer", cancellationToken);
        await using AsyncServiceScope scope = fixture.Factory.Services.CreateAsyncScope();
        ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();
        Result<TeacherApplicationResponse> application = await sender.Send(
            new SubmitTeacherApplicationCommand(
                teacher.UserId,
                "API test instructor",
                "A verified API integration test instructor.",
                "Learning security",
                "Test resource authorization safely."),
            cancellationToken);
        Assert.True(application.IsSuccess);
        Assert.True((await sender.Send(
            new ReviewTeacherApplicationCommand(reviewer.UserId, application.Value.Id, "start", null),
            cancellationToken)).IsSuccess);
        Assert.True((await sender.Send(
            new ReviewTeacherApplicationCommand(reviewer.UserId, application.Value.Id, "approve", null),
            cancellationToken)).IsSuccess);
        return await SignInAsync(teacher.UserId, prefix, cancellationToken);
    }

    private async Task<SignedInUser> CreateUserAsync(string prefix, CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = fixture.Factory.Services.CreateAsyncScope();
        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        ApplicationUser user = ApplicationUser.Create(
            prefix,
            $"{prefix}-{Guid.CreateVersion7():N}@example.test",
            DateTimeOffset.UtcNow);
        user.EmailConfirmed = true;
        Assert.True((await userManager.CreateAsync(user, "correct horse battery staple")).Succeeded);
        Assert.True((await userManager.AddToRoleAsync(
            user,
            Dorosak.Infrastructure.Identity.IdentityConstants.StudentRole)).Succeeded);
        return await SignInAsync(user.Id, prefix, cancellationToken);
    }

    private async Task<SignedInUser> SignInAsync(Guid userId, string prefix, CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = fixture.Factory.Services.CreateAsyncScope();
        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        ApplicationUser user = Assert.IsType<ApplicationUser>(await userManager.FindByIdAsync(userId.ToString("D")));
        Result<SignInResponse> signedIn = await scope.ServiceProvider.GetRequiredService<ISender>().Send(
            new SignInCommand(
                user.Email!,
                "correct horse battery staple",
                new IdentityRequestContext("198.51.100.41", $"Learning API {prefix}", "en")),
            cancellationToken);
        Assert.True(signedIn.IsSuccess);
        Assert.NotNull(signedIn.Value.Session);
        return new SignedInUser(user.Id, signedIn.Value.Session!.AccessToken);
    }

    private static HttpRequestMessage Authorized(HttpMethod method, string path, string accessToken)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return request;
    }

    private static async Task<Guid> ReadGuidAsync(HttpResponseMessage response, string property, CancellationToken cancellationToken)
    {
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return document.RootElement.GetProperty("data").GetProperty(property).GetGuid();
    }

    private sealed record SignedInUser(Guid UserId, string AccessToken);
}
