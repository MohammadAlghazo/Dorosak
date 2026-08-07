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

        using var adminRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/access");
        adminRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using HttpResponseMessage adminAccess = await fixture.Client.SendAsync(adminRequest, cancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, adminAccess.StatusCode);
        Assert.Equal("AUTH.FORBIDDEN", await ReadProblemCodeAsync(adminAccess, cancellationToken));

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

        await Task.Delay(TimeSpan.FromMilliseconds(2500), cancellationToken);
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

    [Fact]
    public async Task Registration_FailsClosedWhenSecurityRedisIsUnavailable()
    {
        await using var factory = new DorosakApiFactory(
            fixture.DatabaseConnection,
            "127.0.0.1:1,abortConnect=false,connectTimeout=100,asyncTimeout=100");
        using HttpClient client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://app.dorosak.test"),
            HandleCookies = false,
        });
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using HttpResponseMessage csrfResponse = await client.GetAsync("/api/v1/auth/csrf", cancellationToken);
        string[] cookies = csrfResponse.Headers.GetValues("Set-Cookie")
            .Select(value => value.Split(';', 2)[0])
            .ToArray();
        string requestTokenCookie = cookies.Single(cookie =>
            cookie.StartsWith("XSRF-TOKEN=", StringComparison.Ordinal));
        string requestToken = requestTokenCookie[(requestTokenCookie.IndexOf('=', StringComparison.Ordinal) + 1)..];

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/register")
        {
            Content = JsonContent.Create(new
            {
                displayName = "Unavailable Redis Student",
                email = $"unavailable-{Guid.CreateVersion7():N}@example.test",
                password = "correct horse battery staple",
            }),
        };
        request.Headers.Add("Origin", "https://app.dorosak.test");
        request.Headers.Add("X-XSRF-TOKEN", requestToken);
        request.Headers.TryAddWithoutValidation("Cookie", string.Join("; ", cookies));
        using HttpResponseMessage response = await client.SendAsync(request, cancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("SECURITY.RATE_LIMIT_UNAVAILABLE", await ReadProblemCodeAsync(response, cancellationToken));
        Assert.True(response.Headers.RetryAfter is not null);
    }

    [Fact]
    public async Task InvalidSignIn_PersistsAccountLockoutState()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        ConfirmedAccount account = await CreateConfirmedAccountAsync(cancellationToken);
        CsrfSession csrf = await GetCsrfAsync(cancellationToken);

        for (int attempt = 0; attempt < 5; attempt++)
        {
            using HttpResponseMessage response = await SendAuthAsync(
                HttpMethod.Post,
                "/api/v1/auth/sign-in",
                new { email = account.Email, password = "wrong password value" },
                csrf,
                cancellationToken: cancellationToken);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Equal("AUTH.INVALID_CREDENTIALS", await ReadProblemCodeAsync(response, cancellationToken));
        }

        await using AsyncServiceScope scope = fixture.Factory.Services.CreateAsyncScope();
        UserManager<ApplicationUser> userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();
        ApplicationUser user = Assert.IsType<ApplicationUser>(
            await userManager.FindByIdAsync(account.UserId.ToString("D")));
        Assert.True(user.LockoutEnd > DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task MfaRecovery_IsRequiredAndSingleUse()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        CsrfSession csrf = await GetCsrfAsync(cancellationToken);
        ConfirmedAccount account = await CreateConfirmedAccountAsync(cancellationToken);
        SignedInSession firstSession = await SignInAsync(account.Email, account.Password, csrf, cancellationToken);
        CsrfSession authenticatedCsrf = await GetCsrfAsync(cancellationToken, firstSession.AccessToken);

        using HttpResponseMessage setupResponse = await SendAuthAsync(
            HttpMethod.Post,
            "/api/v1/auth/mfa/setup",
            body: null,
            authenticatedCsrf,
            firstSession.RefreshCookie,
            firstSession.AccessToken,
            cancellationToken: cancellationToken);
        Assert.Equal(HttpStatusCode.OK, setupResponse.StatusCode);
        using JsonDocument setupDocument = JsonDocument.Parse(
            await setupResponse.Content.ReadAsStringAsync(cancellationToken));
        string secret = Assert.IsType<string>(
            setupDocument.RootElement.GetProperty("data").GetProperty("secret").GetString());
        string totpCode = new OtpNet.Totp(OtpNet.Base32Encoding.ToBytes(secret)).ComputeTotp();

        using HttpResponseMessage confirmation = await SendAuthAsync(
            HttpMethod.Post,
            "/api/v1/auth/mfa/confirm",
            new { code = totpCode },
            authenticatedCsrf,
            firstSession.RefreshCookie,
            firstSession.AccessToken,
            cancellationToken: cancellationToken);
        Assert.Equal(HttpStatusCode.OK, confirmation.StatusCode);
        using JsonDocument confirmationDocument = JsonDocument.Parse(
            await confirmation.Content.ReadAsStringAsync(cancellationToken));
        string recoveryCode = Assert.IsType<string>(confirmationDocument.RootElement
            .GetProperty("data")
            .GetProperty("recoveryCodes")[0]
            .GetString());

        await using (AsyncServiceScope scope = fixture.Factory.Services.CreateAsyncScope())
        {
            UserManager<ApplicationUser> userManager = scope.ServiceProvider
                .GetRequiredService<UserManager<ApplicationUser>>();
            ApplicationUser user = Assert.IsType<ApplicationUser>(
                await userManager.FindByIdAsync(account.UserId.ToString("D")));
            Assert.NotNull(user.ProtectedMfaSecret);
            Assert.DoesNotContain(secret, user.ProtectedMfaSecret, StringComparison.Ordinal);
        }

        using HttpResponseMessage signedOut = await SendAuthAsync(
            HttpMethod.Post,
            "/api/v1/auth/sign-out",
            body: null,
            authenticatedCsrf,
            firstSession.RefreshCookie,
            firstSession.AccessToken,
            cancellationToken: cancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, signedOut.StatusCode);

        CsrfSession anonymousCsrf = await GetCsrfAsync(cancellationToken);
        MfaChallenge challenge = await SignInForMfaAsync(
            account.Email,
            account.Password,
            anonymousCsrf,
            cancellationToken);
        using HttpResponseMessage recovered = await SendAuthAsync(
            HttpMethod.Post,
            "/api/v1/auth/mfa/recovery",
            new { challengeToken = challenge.Token, recoveryCode },
            anonymousCsrf,
            cancellationToken: cancellationToken);
        Assert.Equal(HttpStatusCode.OK, recovered.StatusCode);
        string recoveredAccessToken = await ReadAccessTokenAsync(recovered, cancellationToken);
        string recoveredRefreshCookie = GetCookie(recovered, "__Secure-dorosak-refresh");
        CsrfSession recoveredCsrf = await GetCsrfAsync(cancellationToken, recoveredAccessToken);

        using HttpResponseMessage secondSignOut = await SendAuthAsync(
            HttpMethod.Post,
            "/api/v1/auth/sign-out",
            body: null,
            recoveredCsrf,
            recoveredRefreshCookie,
            recoveredAccessToken,
            cancellationToken: cancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, secondSignOut.StatusCode);

        CsrfSession secondAnonymousCsrf = await GetCsrfAsync(cancellationToken);
        MfaChallenge secondChallenge = await SignInForMfaAsync(
            account.Email,
            account.Password,
            secondAnonymousCsrf,
            cancellationToken);
        using HttpResponseMessage reusedRecovery = await SendAuthAsync(
            HttpMethod.Post,
            "/api/v1/auth/mfa/recovery",
            new { challengeToken = secondChallenge.Token, recoveryCode },
            secondAnonymousCsrf,
            cancellationToken: cancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, reusedRecovery.StatusCode);
        Assert.Equal("MFA.INVALID_RECOVERY_CODE", await ReadProblemCodeAsync(reusedRecovery, cancellationToken));
    }

    [Fact]
    public async Task PasswordReset_RevokesExistingSessions()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        ConfirmedAccount account = await CreateConfirmedAccountAsync(cancellationToken);
        CsrfSession anonymousCsrf = await GetCsrfAsync(cancellationToken);
        SignedInSession session = await SignInAsync(
            account.Email,
            account.Password,
            anonymousCsrf,
            cancellationToken);

        string resetToken;
        await using (AsyncServiceScope scope = fixture.Factory.Services.CreateAsyncScope())
        {
            UserManager<ApplicationUser> userManager = scope.ServiceProvider
                .GetRequiredService<UserManager<ApplicationUser>>();
            ApplicationUser user = Assert.IsType<ApplicationUser>(
                await userManager.FindByIdAsync(account.UserId.ToString("D")));
            resetToken = await userManager.GeneratePasswordResetTokenAsync(user);
        }

        const string newPassword = "a new correct horse battery staple";
        using HttpResponseMessage reset = await SendAuthAsync(
            HttpMethod.Post,
            "/api/v1/auth/password/reset",
            new { userId = account.UserId, token = resetToken, newPassword },
            anonymousCsrf,
            cancellationToken: cancellationToken);
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);

        using HttpResponseMessage revokedRefresh = await SendAuthAsync(
            HttpMethod.Post,
            "/api/v1/auth/refresh",
            body: null,
            anonymousCsrf,
            session.RefreshCookie,
            cancellationToken: cancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, revokedRefresh.StatusCode);

        using HttpResponseMessage oldPassword = await SendAuthAsync(
            HttpMethod.Post,
            "/api/v1/auth/sign-in",
            new { email = account.Email, password = account.Password },
            anonymousCsrf,
            cancellationToken: cancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, oldPassword.StatusCode);

        using HttpResponseMessage newPasswordSignIn = await SendAuthAsync(
            HttpMethod.Post,
            "/api/v1/auth/sign-in",
            new { email = account.Email, password = newPassword },
            anonymousCsrf,
            cancellationToken: cancellationToken);
        Assert.Equal(HttpStatusCode.OK, newPasswordSignIn.StatusCode);
    }

    [Fact]
    public async Task SessionManagement_ListsAndRevokesOwnedDevice()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        ConfirmedAccount account = await CreateConfirmedAccountAsync(cancellationToken);
        CsrfSession anonymousCsrf = await GetCsrfAsync(cancellationToken);
        SignedInSession first = await SignInAsync(
            account.Email,
            account.Password,
            anonymousCsrf,
            cancellationToken);
        SignedInSession second = await SignInAsync(
            account.Email,
            account.Password,
            anonymousCsrf,
            cancellationToken);

        using var listRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/me/sessions");
        listRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", first.AccessToken);
        using HttpResponseMessage listed = await fixture.Client.SendAsync(listRequest, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, listed.StatusCode);
        using JsonDocument sessionsDocument = JsonDocument.Parse(
            await listed.Content.ReadAsStringAsync(cancellationToken));
        JsonElement sessions = sessionsDocument.RootElement.GetProperty("data").GetProperty("sessions");
        Assert.Equal(2, sessions.GetArrayLength());
        Assert.Single(sessions.EnumerateArray(), item => item.GetProperty("isCurrent").GetBoolean());

        CsrfSession authenticatedCsrf = await GetCsrfAsync(cancellationToken, first.AccessToken);
        using HttpResponseMessage revoked = await SendAuthAsync(
            HttpMethod.Delete,
            $"/api/v1/me/sessions/{second.SessionId:D}",
            body: null,
            authenticatedCsrf,
            accessToken: first.AccessToken,
            cancellationToken: cancellationToken);
        Assert.Equal(HttpStatusCode.OK, revoked.StatusCode);

        CsrfSession freshAnonymousCsrf = await GetCsrfAsync(cancellationToken);
        using HttpResponseMessage revokedRefresh = await SendAuthAsync(
            HttpMethod.Post,
            "/api/v1/auth/refresh",
            body: null,
            freshAnonymousCsrf,
            second.RefreshCookie,
            cancellationToken: cancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, revokedRefresh.StatusCode);
    }

    [Fact]
    public async Task EmailChange_KeepsCurrentAddressUntilVerificationAndRevokesSessions()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        ConfirmedAccount account = await CreateConfirmedAccountAsync(cancellationToken);
        CsrfSession anonymousCsrf = await GetCsrfAsync(cancellationToken);
        SignedInSession session = await SignInAsync(
            account.Email,
            account.Password,
            anonymousCsrf,
            cancellationToken);
        CsrfSession authenticatedCsrf = await GetCsrfAsync(cancellationToken, session.AccessToken);
        string newEmail = $"changed-{Guid.CreateVersion7():N}@example.test";

        using HttpResponseMessage requested = await SendAuthAsync(
            HttpMethod.Post,
            "/api/v1/auth/email/change/request",
            new { newEmail, currentPassword = account.Password, locale = "en" },
            authenticatedCsrf,
            session.RefreshCookie,
            session.AccessToken,
            cancellationToken: cancellationToken);
        Assert.Equal(HttpStatusCode.OK, requested.StatusCode);

        string changeToken;
        await using (AsyncServiceScope scope = fixture.Factory.Services.CreateAsyncScope())
        {
            UserManager<ApplicationUser> userManager = scope.ServiceProvider
                .GetRequiredService<UserManager<ApplicationUser>>();
            ApplicationUser user = Assert.IsType<ApplicationUser>(
                await userManager.FindByIdAsync(account.UserId.ToString("D")));
            Assert.Equal(account.Email, user.Email);
            Assert.Equal(newEmail, user.PendingEmail);
            changeToken = await userManager.GenerateChangeEmailTokenAsync(user, newEmail);
        }

        CsrfSession confirmationCsrf = await GetCsrfAsync(cancellationToken);
        using HttpResponseMessage confirmed = await SendAuthAsync(
            HttpMethod.Post,
            "/api/v1/auth/email/change/confirm",
            new { userId = account.UserId, token = changeToken },
            confirmationCsrf,
            cancellationToken: cancellationToken);
        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);

        await using (AsyncServiceScope scope = fixture.Factory.Services.CreateAsyncScope())
        {
            UserManager<ApplicationUser> userManager = scope.ServiceProvider
                .GetRequiredService<UserManager<ApplicationUser>>();
            ApplicationUser user = Assert.IsType<ApplicationUser>(
                await userManager.FindByIdAsync(account.UserId.ToString("D")));
            Assert.Equal(newEmail, user.Email);
            Assert.Null(user.PendingEmail);
        }

        using HttpResponseMessage revokedRefresh = await SendAuthAsync(
            HttpMethod.Post,
            "/api/v1/auth/refresh",
            body: null,
            confirmationCsrf,
            session.RefreshCookie,
            cancellationToken: cancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, revokedRefresh.StatusCode);

        using HttpResponseMessage signedInWithNewEmail = await SendAuthAsync(
            HttpMethod.Post,
            "/api/v1/auth/sign-in",
            new { email = newEmail, password = account.Password },
            confirmationCsrf,
            cancellationToken: cancellationToken);
        Assert.Equal(HttpStatusCode.OK, signedInWithNewEmail.StatusCode);
    }

    private async Task<CsrfSession> GetCsrfAsync(
        CancellationToken cancellationToken,
        string? accessToken = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/csrf");
        if (accessToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }
        using HttpResponseMessage response = await fixture.Client.SendAsync(request, cancellationToken);
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
        string? accessToken = null,
        string origin = "https://app.dorosak.test",
        CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add("Origin", origin);
        if (accessToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }
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

    private async Task<ConfirmedAccount> CreateConfirmedAccountAsync(CancellationToken cancellationToken)
    {
        string email = $"mfa-{Guid.CreateVersion7():N}@example.test";
        const string password = "correct horse battery staple";
        await using AsyncServiceScope scope = fixture.Factory.Services.CreateAsyncScope();
        ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();
        Result<RegistrationAcceptedResponse> registration = await sender.Send(
            new RegisterAccountCommand(
                "Synthetic MFA Student",
                email,
                password,
                new IdentityRequestContext(Guid.CreateVersion7().ToString("D"), "Integration test", "en")),
            cancellationToken);
        Assert.True(registration.IsSuccess);

        UserManager<ApplicationUser> userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();
        ApplicationUser user = Assert.IsType<ApplicationUser>(await userManager.FindByEmailAsync(email));
        string token = await userManager.GenerateEmailConfirmationTokenAsync(user);
        IdentityResult confirmed = await userManager.ConfirmEmailAsync(user, token);
        Assert.True(confirmed.Succeeded);
        return new ConfirmedAccount(user.Id, email, password);
    }

    private async Task<SignedInSession> SignInAsync(
        string email,
        string password,
        CsrfSession csrf,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await SendAuthAsync(
            HttpMethod.Post,
            "/api/v1/auth/sign-in",
            new { email, password },
            csrf,
            cancellationToken: cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        JsonElement session = document.RootElement.GetProperty("data").GetProperty("session");
        return new SignedInSession(
            Assert.IsType<string>(session.GetProperty("accessToken").GetString()),
            GetCookie(response, "__Secure-dorosak-refresh"),
            session.GetProperty("identity").GetProperty("sessionId").GetGuid());
    }

    private async Task<MfaChallenge> SignInForMfaAsync(
        string email,
        string password,
        CsrfSession csrf,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await SendAuthAsync(
            HttpMethod.Post,
            "/api/v1/auth/sign-in",
            new { email, password },
            csrf,
            cancellationToken: cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        JsonElement data = document.RootElement.GetProperty("data");
        Assert.Equal("mfaRequired", data.GetProperty("outcome").GetString());
        Assert.Equal(JsonValueKind.Null, data.GetProperty("session").ValueKind);
        Assert.False(response.Headers.TryGetValues("Set-Cookie", out IEnumerable<string>? cookies) &&
            cookies.Any(value => value.StartsWith("__Secure-dorosak-refresh=", StringComparison.Ordinal)));
        return new MfaChallenge(
            Assert.IsType<string>(data.GetProperty("challengeToken").GetString()));
    }

    private static async Task<string> ReadAccessTokenAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        JsonElement data = document.RootElement.GetProperty("data");
        JsonElement session = data.TryGetProperty("session", out JsonElement nestedSession)
            ? nestedSession
            : data;
        return Assert.IsType<string>(session.GetProperty("accessToken").GetString());
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

    private sealed record ConfirmedAccount(Guid UserId, string Email, string Password);

    private sealed record SignedInSession(string AccessToken, string RefreshCookie, Guid SessionId);

    private sealed record MfaChallenge(string Token);
}
