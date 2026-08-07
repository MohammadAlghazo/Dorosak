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

namespace Dorosak.Api.IntegrationTests;

[Collection(ApiTestGroup.Name)]
public sealed class IdentityEndpointTests(ApiFixture fixture)
{
    [Fact]
    public async Task RegistrationCommand_CommitsIdentityAggregateAtomically()
    {
        await using AsyncServiceScope scope = fixture.Factory.Services.CreateAsyncScope();
        ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();
        Result<RegistrationAcceptedResponse> result = await sender.Send(
            new RegisterAccountCommand(
                "Synthetic Command Student",
                $"command-{Guid.CreateVersion7():N}@example.test",
                "correct horse battery staple",
                new IdentityRequestContext("198.51.100.10", "Dorosak integration test", "en")),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task IdentityJourney_RotatesRefreshAndRevokesReplay()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        CsrfSession csrf = await GetCsrfAsync(cancellationToken);
        string email = $"student-{Guid.CreateVersion7():N}@example.test";
        var registration = new
        {
            displayName = "Synthetic Student",
            email,
            password = "correct horse battery staple",
        };

        using HttpResponseMessage registered = await SendAuthAsync(
            HttpMethod.Post,
            "/api/v1/auth/register",
            registration,
            csrf,
            cancellationToken: cancellationToken);
        using HttpResponseMessage duplicate = await SendAuthAsync(
            HttpMethod.Post,
            "/api/v1/auth/register",
            registration,
            csrf,
            cancellationToken: cancellationToken);

        Assert.Equal(HttpStatusCode.OK, registered.StatusCode);
        Assert.Equal(HttpStatusCode.OK, duplicate.StatusCode);
        Assert.Equal(
            await registered.Content.ReadAsStringAsync(cancellationToken),
            await duplicate.Content.ReadAsStringAsync(cancellationToken));

        Guid userId;
        string confirmationToken;
        await using (AsyncServiceScope scope = fixture.Factory.Services.CreateAsyncScope())
        {
            UserManager<ApplicationUser> userManager = scope.ServiceProvider
                .GetRequiredService<UserManager<ApplicationUser>>();
            ApplicationUser user = Assert.IsType<ApplicationUser>(await userManager.FindByEmailAsync(email));
            userId = user.Id;
            confirmationToken = await userManager.GenerateEmailConfirmationTokenAsync(user);
        }

        using HttpResponseMessage confirmed = await SendAuthAsync(
            HttpMethod.Post,
            "/api/v1/auth/email-verification/confirm",
            new { userId, token = confirmationToken },
            csrf,
            cancellationToken: cancellationToken);
        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);

        using HttpResponseMessage signedIn = await SendAuthAsync(
            HttpMethod.Post,
            "/api/v1/auth/sign-in",
            new { email, password = "correct horse battery staple" },
            csrf,
            cancellationToken: cancellationToken);
        Assert.Equal(HttpStatusCode.OK, signedIn.StatusCode);

        string signInBody = await signedIn.Content.ReadAsStringAsync(cancellationToken);
        Assert.DoesNotContain("refreshToken", signInBody, StringComparison.OrdinalIgnoreCase);
        using JsonDocument signInDocument = JsonDocument.Parse(signInBody);
        JsonElement signInData = signInDocument.RootElement.GetProperty("data");
        Assert.Equal("authenticated", signInData.GetProperty("outcome").GetString());
        JsonElement session = signInData.GetProperty("session");
        string accessToken = Assert.IsType<string>(session.GetProperty("accessToken").GetString());
        Assert.Equal(email, session.GetProperty("identity").GetProperty("email").GetString());
        Assert.Contains(
            "Profile.ReadOwn",
            session.GetProperty("identity").GetProperty("permissions")
                .EnumerateArray()
                .Select(value => value.GetString()),
            StringComparer.Ordinal);

        string firstRefreshCookie = GetCookie(signedIn, "__Secure-dorosak-refresh");
        AssertCookieSecurity(signedIn);

        using var profileRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/me/profile");
        profileRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using HttpResponseMessage profile = await fixture.Client.SendAsync(profileRequest, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, profile.StatusCode);
        Assert.Contains("no-store", profile.Headers.CacheControl?.ToString(), StringComparison.OrdinalIgnoreCase);

        using HttpResponseMessage refreshed = await SendAuthAsync(
            HttpMethod.Post,
            "/api/v1/auth/refresh",
            body: null,
            csrf,
            firstRefreshCookie,
            cancellationToken: cancellationToken);
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        string secondRefreshCookie = GetCookie(refreshed, "__Secure-dorosak-refresh");
        Assert.NotEqual(firstRefreshCookie, secondRefreshCookie);

        await Task.Delay(TimeSpan.FromMilliseconds(1100), cancellationToken);
        using HttpResponseMessage replay = await SendAuthAsync(
            HttpMethod.Post,
            "/api/v1/auth/refresh",
            body: null,
            csrf,
            firstRefreshCookie,
            cancellationToken: cancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);

        using HttpResponseMessage revokedFamily = await SendAuthAsync(
            HttpMethod.Post,
            "/api/v1/auth/refresh",
            body: null,
            csrf,
            secondRefreshCookie,
            cancellationToken: cancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, revokedFamily.StatusCode);
    }

    [Fact]
    public async Task Register_RejectsMissingCsrfAndUntrustedOrigin()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        CsrfSession csrf = await GetCsrfAsync(cancellationToken);
        var registration = new
        {
            displayName = "Synthetic Student",
            email = $"csrf-{Guid.CreateVersion7():N}@example.test",
            password = "correct horse battery staple",
        };

        using HttpResponseMessage missingCsrf = await SendAuthAsync(
            HttpMethod.Post,
            "/api/v1/auth/register",
            registration,
            csrf: null,
            origin: "https://app.dorosak.test",
            cancellationToken: cancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, missingCsrf.StatusCode);
        Assert.Equal("SECURITY.ANTIFORGERY_INVALID", await ReadProblemCodeAsync(missingCsrf, cancellationToken));

        using HttpResponseMessage untrustedOrigin = await SendAuthAsync(
            HttpMethod.Post,
            "/api/v1/auth/register",
            registration,
            csrf,
            origin: "https://attacker.example",
            cancellationToken: cancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, untrustedOrigin.StatusCode);
        Assert.Equal("SECURITY.ORIGIN_REJECTED", await ReadProblemCodeAsync(untrustedOrigin, cancellationToken));
    }

    private async Task<CsrfSession> GetCsrfAsync(CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await fixture.Client.GetAsync("/api/v1/auth/csrf", cancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        string[] cookies = response.Headers.GetValues("Set-Cookie")
            .Select(value => value.Split(';', 2)[0])
            .ToArray();
        string requestTokenCookie = cookies.Single(cookie =>
            cookie.StartsWith("XSRF-TOKEN=", StringComparison.Ordinal));
        string token = requestTokenCookie[(requestTokenCookie.IndexOf('=', StringComparison.Ordinal) + 1)..];
        return new CsrfSession(string.Join("; ", cookies), token);
    }

    private async Task<HttpResponseMessage> SendAuthAsync(
        HttpMethod method,
        string path,
        object? body,
        CsrfSession? csrf,
        string? refreshCookie = null,
        string origin = "https://app.dorosak.test",
        CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add("Origin", origin);
        if (csrf is not null)
        {
            request.Headers.Add("X-XSRF-TOKEN", csrf.Token);
            string cookie = refreshCookie is null ? csrf.Cookies : $"{csrf.Cookies}; {refreshCookie}";
            request.Headers.TryAddWithoutValidation("Cookie", cookie);
        }
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await fixture.Client.SendAsync(request, cancellationToken);
    }

    private static string GetCookie(HttpResponseMessage response, string name)
    {
        string prefix = $"{name}=";
        string header = response.Headers.GetValues("Set-Cookie")
            .Single(value => value.StartsWith(prefix, StringComparison.Ordinal));
        return header.Split(';', 2)[0];
    }

    private static void AssertCookieSecurity(HttpResponseMessage response)
    {
        string header = response.Headers.GetValues("Set-Cookie")
            .Single(value => value.StartsWith("__Secure-dorosak-refresh=", StringComparison.Ordinal));
        Assert.Contains("httponly", header, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", header, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", header, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/api/v1/auth", header, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("domain=", header, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<string> ReadProblemCodeAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return Assert.IsType<string>(document.RootElement.GetProperty("code").GetString());
    }

    private sealed record CsrfSession(string Cookies, string Token);
}
