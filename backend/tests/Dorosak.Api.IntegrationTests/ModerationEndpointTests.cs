using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Dorosak.Application.Common.Results;
using Dorosak.Application.Features.Identity;
using Dorosak.Infrastructure.Identity;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using OtpNet;
using DorosakIdentityConstants = Dorosak.Infrastructure.Identity.IdentityConstants;

namespace Dorosak.Api.IntegrationTests;

[Collection(ApiTestGroup.Name)]
public sealed class ModerationEndpointTests(ApiFixture fixture)
{
    [Fact]
    public async Task ReportsAndModeration_EnforceAuthenticationHeadersAndHighRiskPolicy()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using HttpResponseMessage anonymous = await fixture.Client.PostAsJsonAsync(
            "/api/v1/reports",
            new { courseId = Guid.CreateVersion7(), reason = "Spam" },
            cancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        SignedInUser student = await CreateUserAsync("moderation-http-student", cancellationToken);
        using HttpRequestMessage missingKey = Authorized(HttpMethod.Post, "/api/v1/reports", student.AccessToken);
        missingKey.Content = JsonContent.Create(new { courseId = Guid.CreateVersion7(), reason = "Spam" });
        using HttpResponseMessage precondition = await fixture.Client.SendAsync(missingKey, cancellationToken);
        Assert.Equal((HttpStatusCode)428, precondition.StatusCode);
        Assert.Equal("IDEMPOTENCY.KEY_REQUIRED", await ReadProblemCodeAsync(precondition, cancellationToken));

        using HttpRequestMessage numericReason = Authorized(HttpMethod.Post, "/api/v1/reports", student.AccessToken);
        numericReason.Headers.Add("Idempotency-Key", Guid.CreateVersion7().ToString("N"));
        numericReason.Content = JsonContent.Create(new { courseId = Guid.CreateVersion7(), reason = "999" });
        using HttpResponseMessage invalidReason = await fixture.Client.SendAsync(numericReason, cancellationToken);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, invalidReason.StatusCode);

        using HttpRequestMessage missingContext = Authorized(HttpMethod.Post, "/api/v1/reports", student.AccessToken);
        missingContext.Headers.Add("Idempotency-Key", Guid.CreateVersion7().ToString("N"));
        missingContext.Content = JsonContent.Create(new
        {
            reportedUserId = Guid.CreateVersion7(),
            reason = "Harassment",
            details = "Synthetic report context is intentionally absent.",
        });
        using HttpResponseMessage invalidContext = await fixture.Client.SendAsync(missingContext, cancellationToken);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, invalidContext.StatusCode);

        using HttpRequestMessage queue = Authorized(HttpMethod.Get, "/api/v1/admin/reports", student.AccessToken);
        using HttpResponseMessage deniedQueue = await fixture.Client.SendAsync(queue, cancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, deniedQueue.StatusCode);
        Assert.Equal("AUTH.FORBIDDEN", await ReadProblemCodeAsync(deniedQueue, cancellationToken));

        using HttpRequestMessage action = Authorized(
            HttpMethod.Post,
            $"/api/v1/admin/moderation-cases/{Guid.CreateVersion7():D}/actions",
            student.AccessToken);
        action.Headers.Add("Idempotency-Key", Guid.CreateVersion7().ToString("N"));
        action.Headers.Add("X-Audit-Reason", "Synthetic high-risk policy verification.");
        action.Content = JsonContent.Create(new
        {
            action = "StartReview",
            reason = "Synthetic high-risk action verification.",
            expectedVersion = 1,
        });
        using HttpResponseMessage deniedAction = await fixture.Client.SendAsync(action, cancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, deniedAction.StatusCode);
        Assert.Equal("AUTH.FORBIDDEN", await ReadProblemCodeAsync(deniedAction, cancellationToken));
    }

    [Fact]
    public async Task ModerationActions_RequireHeadersVersionAndARecentMfaAdminSession()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        SignedInUser admin = await CreateMfaAdminAsync("moderation-http-admin", cancellationToken);
        string path = $"/api/v1/admin/moderation-cases/{Guid.CreateVersion7():D}/actions";

        using HttpRequestMessage missingAudit = Authorized(HttpMethod.Post, path, admin.AccessToken);
        missingAudit.Headers.Add("Idempotency-Key", Guid.CreateVersion7().ToString("N"));
        missingAudit.Content = JsonContent.Create(new
        {
            action = "StartReview",
            reason = "Synthetic high-risk action verification.",
            expectedVersion = 1,
        });
        using HttpResponseMessage deniedAudit = await fixture.Client.SendAsync(missingAudit, cancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, deniedAudit.StatusCode);

        using HttpRequestMessage missingKey = Authorized(HttpMethod.Post, path, admin.AccessToken);
        missingKey.Headers.Add("X-Audit-Reason", "Synthetic high-risk policy verification.");
        missingKey.Content = JsonContent.Create(new
        {
            action = "StartReview",
            reason = "Synthetic high-risk action verification.",
            expectedVersion = 1,
        });
        using HttpResponseMessage precondition = await fixture.Client.SendAsync(missingKey, cancellationToken);
        Assert.Equal((HttpStatusCode)428, precondition.StatusCode);
        Assert.Equal("IDEMPOTENCY.KEY_REQUIRED", await ReadProblemCodeAsync(precondition, cancellationToken));

        using HttpRequestMessage invalidVersion = Authorized(HttpMethod.Post, path, admin.AccessToken);
        invalidVersion.Headers.Add("Idempotency-Key", Guid.CreateVersion7().ToString("N"));
        invalidVersion.Headers.Add("X-Audit-Reason", "Synthetic high-risk policy verification.");
        invalidVersion.Content = JsonContent.Create(new
        {
            action = "StartReview",
            reason = "Synthetic high-risk action verification.",
            expectedVersion = 0,
        });
        using HttpResponseMessage validation = await fixture.Client.SendAsync(invalidVersion, cancellationToken);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, validation.StatusCode);

        using HttpRequestMessage validPolicy = Authorized(HttpMethod.Post, path, admin.AccessToken);
        validPolicy.Headers.Add("Idempotency-Key", Guid.CreateVersion7().ToString("N"));
        validPolicy.Headers.Add("X-Audit-Reason", "Synthetic high-risk policy verification.");
        validPolicy.Content = JsonContent.Create(new
        {
            action = "StartReview",
            reason = "Synthetic high-risk action verification.",
            expectedVersion = 1,
        });
        using HttpResponseMessage notFound = await fixture.Client.SendAsync(validPolicy, cancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, notFound.StatusCode);
        Assert.Equal("MODERATION.NOT_FOUND", await ReadProblemCodeAsync(notFound, cancellationToken));
    }

    private async Task<SignedInUser> CreateUserAsync(string prefix, CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = fixture.Factory.Services.CreateAsyncScope();
        UserManager<ApplicationUser> manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        ApplicationUser user = ApplicationUser.Create(
            prefix,
            $"{prefix}-{Guid.CreateVersion7():N}@example.test",
            DateTimeOffset.UtcNow);
        user.EmailConfirmed = true;
        Assert.True((await manager.CreateAsync(user, "correct horse battery staple")).Succeeded);
        Assert.True((await manager.AddToRoleAsync(
            user,
            Dorosak.Infrastructure.Identity.IdentityConstants.StudentRole)).Succeeded);
        Result<SignInResponse> signIn = await scope.ServiceProvider.GetRequiredService<ISender>().Send(
            new SignInCommand(
                user.Email!,
                "correct horse battery staple",
                new IdentityRequestContext("198.51.100.61", "moderation API test", "en")),
            cancellationToken);
        Assert.True(signIn.IsSuccess);
        return new SignedInUser(user.Id, signIn.Value.Session!.AccessToken);
    }

    private async Task<SignedInUser> CreateMfaAdminAsync(
        string prefix,
        CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = fixture.Factory.Services.CreateAsyncScope();
        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        RoleManager<ApplicationRole> roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();
        string email = $"{prefix}-{Guid.CreateVersion7():N}@example.test";
        var context = new IdentityRequestContext("198.51.100.62", "moderation MFA API test", "en");
        ApplicationUser user = ApplicationUser.Create(prefix, email, DateTimeOffset.UtcNow);
        user.EmailConfirmed = true;
        Assert.True((await userManager.CreateAsync(user, "correct horse battery staple")).Succeeded);
        Assert.True((await userManager.AddToRoleAsync(user, DorosakIdentityConstants.StudentRole)).Succeeded);

        Result<SignInResponse> initialSignIn = await sender.Send(
            new SignInCommand(email, "correct horse battery staple", context),
            cancellationToken);
        Assert.True(initialSignIn.IsSuccess);
        AuthenticatedSessionResponse initialSession = Assert.IsType<AuthenticatedSessionResponse>(
            initialSignIn.Value.Session);
        Result<MfaSetupResponse> setup = await sender.Send(
            new SetupMfaCommand(user.Id, initialSession.Identity.SessionId),
            cancellationToken);
        Assert.True(setup.IsSuccess);
        string setupCode = new Totp(Base32Encoding.ToBytes(setup.Value.Secret)).ComputeTotp();
        Result<MfaConfirmationResponse> confirmation = await sender.Send(
            new ConfirmMfaCommand(user.Id, initialSession.Identity.SessionId, setupCode),
            cancellationToken);
        Assert.True(confirmation.IsSuccess);

        Assert.True((await userManager.AddToRoleAsync(user, DorosakIdentityConstants.AdminRole)).Succeeded);
        ApplicationRole adminRole = Assert.IsType<ApplicationRole>(
            await roleManager.FindByNameAsync(DorosakIdentityConstants.AdminRole));
        IList<System.Security.Claims.Claim> adminClaims = await roleManager.GetClaimsAsync(adminRole);
        Assert.Contains(adminClaims, claim =>
            claim.Type == DorosakIdentityConstants.PermissionClaimType &&
            claim.Value == Permissions.ModerationReviewAny);

        Result<SignInResponse> challenged = await sender.Send(
            new SignInCommand(email, "correct horse battery staple", context),
            cancellationToken);
        Assert.True(challenged.IsSuccess);
        Assert.Equal("mfaRequired", challenged.Value.Outcome);
        string challengeToken = Assert.IsType<string>(challenged.Value.ChallengeToken);
        Result<AuthenticatedSessionResponse> authenticated = await sender.Send(
            new CompleteMfaRecoveryCommand(
                challengeToken,
                confirmation.Value.RecoveryCodes[0],
                context),
            cancellationToken);
        Assert.True(authenticated.IsSuccess);
        return new SignedInUser(user.Id, authenticated.Value.AccessToken);
    }

    private static HttpRequestMessage Authorized(HttpMethod method, string path, string token)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private static async Task<string> ReadProblemCodeAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return Assert.IsType<string>(document.RootElement.GetProperty("code").GetString());
    }

    private sealed record SignedInUser(Guid UserId, string AccessToken);
}
