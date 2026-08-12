using System.Net;
using System.Net.Http.Headers;
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
public sealed class AdminAnalyticsEndpointTests(ApiFixture fixture)
{
    [Fact]
    public async Task Overview_RequiresAnalyticsPermissionAndReturnsAggregatesWithoutPii()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        SignedInUser learner = await CreateUserAsync("analytics-learner", admin: false, cancellationToken);
        SignedInUser admin = await CreateUserAsync("analytics-admin", admin: true, cancellationToken);

        using HttpResponseMessage anonymous = await fixture.Client.GetAsync(
            "/api/v1/admin/analytics/overview",
            cancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        using HttpRequestMessage forbiddenRequest = Authorized(learner.AccessToken);
        using HttpResponseMessage forbidden = await fixture.Client.SendAsync(forbiddenRequest, cancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        using HttpRequestMessage allowedRequest = Authorized(admin.AccessToken);
        using HttpResponseMessage allowed = await fixture.Client.SendAsync(allowedRequest, cancellationToken);

        string payload = await allowed.Content.ReadAsStringAsync(cancellationToken);
        Assert.True(allowed.StatusCode == HttpStatusCode.OK, payload);
        Assert.True(allowed.Headers.CacheControl?.NoStore);
        Assert.True(allowed.Headers.CacheControl?.NoCache);
        using JsonDocument document = JsonDocument.Parse(payload);
        JsonElement overview = document.RootElement.GetProperty("data");
        Assert.True(overview.GetProperty("totalUsers").GetInt64() >= 2);
        Assert.True(overview.GetProperty("activeUsers").GetInt64() >= 2);
        Assert.True(overview.GetProperty("publishedCourses").GetInt64() <=
            overview.GetProperty("totalCourses").GetInt64());
        Assert.True(overview.GetProperty("completedEnrollments").GetInt64() <=
            overview.GetProperty("totalEnrollments").GetInt64());
        Assert.True(overview.GetProperty("activeCertificates").GetInt64() <=
            overview.GetProperty("issuedCertificates").GetInt64());
        Assert.DoesNotContain("email", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("displayName", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("messageBody", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("payment", payload, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<SignedInUser> CreateUserAsync(
        string prefix,
        bool admin,
        CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = fixture.Factory.Services.CreateAsyncScope();
        UserManager<ApplicationUser> manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        ApplicationUser user = ApplicationUser.Create(
            prefix,
            $"{prefix}-{Guid.CreateVersion7():N}@example.test",
            DateTimeOffset.UtcNow);
        user.EmailConfirmed = true;
        Assert.True((await manager.CreateAsync(user, "correct horse battery staple")).Succeeded);
        Assert.True((await manager.AddToRoleAsync(user, DorosakIdentityConstants.StudentRole)).Succeeded);
        ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var context = new IdentityRequestContext("198.51.100.71", "analytics API test", "en");
        Result<SignInResponse> signIn = await sender.Send(
            new SignInCommand(
                user.Email!,
                "correct horse battery staple",
                context),
            cancellationToken);
        Assert.True(signIn.IsSuccess);
        AuthenticatedSessionResponse initialSession = Assert.IsType<AuthenticatedSessionResponse>(signIn.Value.Session);
        if (!admin)
        {
            return new SignedInUser(initialSession.AccessToken);
        }

        Result<MfaSetupResponse> setup = await sender.Send(
            new SetupMfaCommand(user.Id, initialSession.Identity.SessionId),
            cancellationToken);
        Assert.True(setup.IsSuccess);
        string setupCode = new Totp(Base32Encoding.ToBytes(setup.Value.Secret)).ComputeTotp();
        Result<MfaConfirmationResponse> confirmation = await sender.Send(
            new ConfirmMfaCommand(user.Id, initialSession.Identity.SessionId, setupCode),
            cancellationToken);
        Assert.True(confirmation.IsSuccess);
        Assert.True((await manager.AddToRoleAsync(user, DorosakIdentityConstants.AdminRole)).Succeeded);

        Result<SignInResponse> challenged = await sender.Send(
            new SignInCommand(user.Email!, "correct horse battery staple", context),
            cancellationToken);
        Assert.True(challenged.IsSuccess);
        Assert.Equal("mfaRequired", challenged.Value.Outcome);
        Result<AuthenticatedSessionResponse> authenticated = await sender.Send(
            new CompleteMfaRecoveryCommand(
                Assert.IsType<string>(challenged.Value.ChallengeToken),
                confirmation.Value.RecoveryCodes[0],
                context),
            cancellationToken);
        Assert.True(authenticated.IsSuccess);
        return new SignedInUser(authenticated.Value.AccessToken);
    }

    private static HttpRequestMessage Authorized(string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/analytics/overview");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return request;
    }

    private sealed record SignedInUser(string AccessToken);
}
