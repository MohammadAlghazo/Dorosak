using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dorosak.Application.Common.Errors;
using Dorosak.Application.Common.Identity;
using Dorosak.Application.Common.Results;
using Dorosak.Application.Features.Identity;
using Dorosak.Domain.Identity;
using Dorosak.Domain.Operations;
using Dorosak.Domain.Profiles;
using Dorosak.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OtpNet;

namespace Dorosak.Infrastructure.Identity;

internal sealed class IdentityService(
    DorosakDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    JwtTokenIssuer jwtTokenIssuer,
    SecurityRateLimiter rateLimiter,
    BreachedPasswordService breachedPasswordService,
    IDataProtectionProvider dataProtectionProvider,
    IOptions<IdentitySecurityOptions> securityOptions,
    TimeProvider timeProvider) : IIdentityService
{
    private const string VerificationEmailEvent = "identity.email-verification-requested";
    private const string PasswordResetEmailEvent = "identity.password-reset-requested";

    private static readonly ResultError InvalidCredentials = ResultError.Unauthorized(
        "AUTH.INVALID_CREDENTIALS",
        "The credentials or account state did not allow sign-in.");

    private static readonly ResultError InvalidSession = ResultError.Unauthorized(
        "SESSION.INVALID",
        "The session is missing, expired, or revoked.");

    private static readonly ResultError InvalidMfaChallenge = ResultError.Unauthorized(
        "MFA.INVALID_CHALLENGE",
        "The MFA challenge is invalid or expired.");

    private readonly IdentitySecurityOptions _securityOptions = securityOptions.Value;
    private readonly IDataProtector _mfaProtector =
        dataProtectionProvider.CreateProtector("Dorosak.Identity.Mfa.v1");

    public async Task<Result<RegistrationAcceptedResponse>> RegisterAsync(
        RegisterAccountCommand request,
        CancellationToken cancellationToken)
    {
        ResultError? rateError = await CheckRegistrationRateAsync(request.Context);
        if (rateError is not null)
        {
            return Result.Failure<RegistrationAcceptedResponse>(rateError);
        }

        ResultError? passwordError = await CheckPasswordAsync(request.Password, cancellationToken);
        if (passwordError is not null)
        {
            return Result.Failure<RegistrationAcceptedResponse>(passwordError);
        }

        string email = request.Email.Trim();
        ApplicationUser? existingUser = await userManager.FindByEmailAsync(email);
        if (existingUser is not null)
        {
            if (!existingUser.EmailConfirmed)
            {
                QueueIdentityEmail(existingUser.Id, VerificationEmailEvent, request.Context.Locale);
            }

            return Result.Success(new RegistrationAcceptedResponse(true));
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        ApplicationUser user = ApplicationUser.Create(request.DisplayName, email, now);
        IdentityResult created = await userManager.CreateAsync(user, request.Password);
        if (!created.Succeeded)
        {
            if (created.Errors.Any(error =>
                    error.Code is "DuplicateEmail" or "DuplicateUserName"))
            {
                return Result.Success(new RegistrationAcceptedResponse(true));
            }

            return Result.Failure<RegistrationAcceptedResponse>(MapIdentityFailure(
                created,
                "AUTH.REGISTRATION_REJECTED",
                "The account could not be registered."));
        }

        IdentityResult roleAssigned = await userManager.AddToRoleAsync(user, IdentityConstants.StudentRole);
        if (!roleAssigned.Succeeded)
        {
            return Result.Failure<RegistrationAcceptedResponse>(MapIdentityFailure(
                roleAssigned,
                "AUTH.ROLE_ASSIGNMENT_FAILED",
                "The account could not be registered."));
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        dbContext.UserProfiles.Add(UserProfile.Create(user.Id, request.DisplayName, now));
        dbContext.SecurityEvents.Add(SecurityEvent.Create(
            user.Id,
            null,
            "account.registered",
            now,
            HashValue(request.Context.IpAddress)));
        QueueIdentityEmail(user.Id, VerificationEmailEvent, request.Context.Locale);
        return Result.Success(new RegistrationAcceptedResponse(true));
    }

    public async Task<Result<SignInResponse>> SignInAsync(
        SignInCommand request,
        CancellationToken cancellationToken)
    {
        ResultError? rateError = await CheckSignInRateAsync(request.Email, request.Context);
        if (rateError is not null)
        {
            return Result.Failure<SignInResponse>(rateError);
        }

        ApplicationUser? user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null || !user.IsActive || await userManager.IsLockedOutAsync(user))
        {
            return Result.Failure<SignInResponse>(InvalidCredentials);
        }

        bool passwordValid = await userManager.CheckPasswordAsync(user, request.Password);
        if (!passwordValid)
        {
            await userManager.AccessFailedAsync(user);
            return Result.Failure<SignInResponse>(InvalidCredentials);
        }

        await userManager.ResetAccessFailedCountAsync(user);
        if (!await userManager.IsEmailConfirmedAsync(user))
        {
            return Result.Failure<SignInResponse>(InvalidCredentials);
        }

        IReadOnlyList<string> roles = await GetRolesAsync(user);
        bool requiresMfa = user.TwoFactorEnabled || roles.Contains(IdentityConstants.AdminRole, StringComparer.Ordinal);
        if (requiresMfa)
        {
            if (string.IsNullOrWhiteSpace(user.ProtectedMfaSecret))
            {
                return Result.Failure<SignInResponse>(InvalidCredentials);
            }

            (string rawChallenge, MfaChallenge challenge) = CreateMfaChallenge(user.Id);
            dbContext.MfaChallenges.Add(challenge);
            return Result.Success(new SignInResponse(
                "mfaRequired",
                null,
                rawChallenge,
                challenge.ExpiresAt));
        }

        AuthenticatedSessionResponse session = await CreateSessionAsync(
            user,
            request.Context,
            ["pwd"],
            cancellationToken);
        return Result.Success(new SignInResponse("authenticated", session, null, null));
    }

    public async Task<Result<AuthenticatedSessionResponse>> CompleteMfaAsync(
        CompleteMfaChallengeCommand request,
        CancellationToken cancellationToken)
    {
        string challengeHash = HashValue(request.ChallengeToken);
        MfaChallenge? challenge = await FindMfaChallengeForUpdateAsync(challengeHash, cancellationToken);
        DateTimeOffset now = timeProvider.GetUtcNow();
        if (challenge is null || !challenge.IsActive(now, _securityOptions.MfaMaximumAttempts))
        {
            return Result.Failure<AuthenticatedSessionResponse>(InvalidMfaChallenge);
        }

        ApplicationUser? user = await userManager.FindByIdAsync(challenge.UserId.ToString("D"));
        if (user is null || !user.IsActive || !user.TwoFactorEnabled ||
            string.IsNullOrWhiteSpace(user.ProtectedMfaSecret))
        {
            challenge.RegisterFailure();
            return Result.Failure<AuthenticatedSessionResponse>(InvalidMfaChallenge);
        }

        string secret;
        try
        {
            secret = _mfaProtector.Unprotect(user.ProtectedMfaSecret);
        }
        catch (CryptographicException)
        {
            challenge.RegisterFailure();
            return Result.Failure<AuthenticatedSessionResponse>(InvalidMfaChallenge);
        }

        var totp = new Totp(Base32Encoding.ToBytes(secret));
        bool validCode = totp.VerifyTotp(
            request.Code,
            out long matchedTimeStep,
            VerificationWindow.RfcSpecifiedNetworkDelay);
        if (!validCode || user.LastMfaTimeStep is { } lastTimeStep && matchedTimeStep <= lastTimeStep)
        {
            challenge.RegisterFailure();
            return Result.Failure<AuthenticatedSessionResponse>(ResultError.Unauthorized(
                "MFA.INVALID_CODE",
                "The MFA code is invalid or has already been used."));
        }

        challenge.Consume(now);
        user.LastMfaTimeStep = matchedTimeStep;
        user.UpdatedAt = now;
        IdentityResult updated = await userManager.UpdateAsync(user);
        if (!updated.Succeeded)
        {
            return Result.Failure<AuthenticatedSessionResponse>(MapIdentityFailure(
                updated,
                "MFA.UPDATE_FAILED",
                "The MFA challenge could not be completed."));
        }

        return Result.Success(await CreateSessionAsync(user, request.Context, ["pwd", "otp"], cancellationToken));
    }

    public async Task<Result<AuthenticatedSessionResponse>> CompleteMfaRecoveryAsync(
        CompleteMfaRecoveryCommand request,
        CancellationToken cancellationToken)
    {
        string challengeHash = HashValue(request.ChallengeToken);
        MfaChallenge? challenge = await FindMfaChallengeForUpdateAsync(challengeHash, cancellationToken);
        DateTimeOffset now = timeProvider.GetUtcNow();
        if (challenge is null || !challenge.IsActive(now, _securityOptions.MfaMaximumAttempts))
        {
            return Result.Failure<AuthenticatedSessionResponse>(InvalidMfaChallenge);
        }

        ApplicationUser? user = await userManager.FindByIdAsync(challenge.UserId.ToString("D"));
        if (user is null || !user.IsActive || !user.TwoFactorEnabled)
        {
            challenge.RegisterFailure();
            return Result.Failure<AuthenticatedSessionResponse>(InvalidMfaChallenge);
        }

        string codeHash = HashValue(NormalizeRecoveryCode(request.RecoveryCode));
        MfaRecoveryCode? recoveryCode = await dbContext.MfaRecoveryCodes
            .SingleOrDefaultAsync(
                code => code.UserId == user.Id && code.CodeHash == codeHash && code.UsedAt == null,
                cancellationToken);
        if (recoveryCode is null)
        {
            challenge.RegisterFailure();
            return Result.Failure<AuthenticatedSessionResponse>(ResultError.Unauthorized(
                "MFA.INVALID_RECOVERY_CODE",
                "The recovery code is invalid or has already been used."));
        }

        challenge.Consume(now);
        recoveryCode.Use(now);
        return Result.Success(await CreateSessionAsync(user, request.Context, ["pwd", "recovery"], cancellationToken));
    }

    public async Task<Result<AuthenticatedSessionResponse>> RefreshAsync(
        RefreshSessionCommand request,
        CancellationToken cancellationToken)
    {
        ResultError? rateError = await CheckRefreshRateAsync(request.RefreshToken);
        if (rateError is not null)
        {
            return Result.Failure<AuthenticatedSessionResponse>(rateError);
        }

        string tokenHash = HashValue(request.RefreshToken);
        RefreshToken? token = await dbContext.RefreshTokens
            .FromSqlInterpolated($"SELECT * FROM identity.refresh_tokens WHERE token_hash = {tokenHash} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (token is null)
        {
            return Result.Failure<AuthenticatedSessionResponse>(InvalidSession);
        }

        RefreshSession? session = await dbContext.RefreshSessions
            .FromSqlInterpolated($"SELECT * FROM identity.refresh_sessions WHERE id = {token.SessionId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        DateTimeOffset now = timeProvider.GetUtcNow();
        if (session is null)
        {
            return Result.Failure<AuthenticatedSessionResponse>(InvalidSession);
        }

        if (token.ConsumedAt is not null)
        {
            if (token.WasConsumedRecently(now, TimeSpan.FromSeconds(_securityOptions.RefreshRaceWindowSeconds)))
            {
                return Result.Failure<AuthenticatedSessionResponse>(ResultError.Unauthorized(
                    "SESSION.REFRESH_RACE",
                    "The session was refreshed by another request."));
            }

            session.Revoke(now, "refresh-token-reuse");
            dbContext.SecurityEvents.Add(SecurityEvent.Create(
                session.UserId,
                session.Id,
                "session.refresh-reuse-detected",
                now,
                HashValue(request.Context.IpAddress)));
            return Result.Failure<AuthenticatedSessionResponse>(InvalidSession);
        }

        if (!token.IsActive(now) || !session.IsActive(now))
        {
            session.Revoke(now, "expired");
            return Result.Failure<AuthenticatedSessionResponse>(InvalidSession);
        }

        ApplicationUser? user = await userManager.FindByIdAsync(session.UserId.ToString("D"));
        if (user is null || !user.IsActive || !user.EmailConfirmed ||
            user.AuthorizationVersion != session.AuthorizationVersion)
        {
            session.Revoke(now, "account-state-changed");
            return Result.Failure<AuthenticatedSessionResponse>(InvalidSession);
        }

        string replacementRawToken = GenerateOpaqueToken(64);
        var replacement = RefreshToken.Create(
            session.Id,
            session.FamilyId,
            HashValue(replacementRawToken),
            now,
            session.AbsoluteExpiresAt);
        token.Consume(now, replacement.Id);
        session.Touch(now, TimeSpan.FromDays(_securityOptions.RefreshIdleDays));
        dbContext.RefreshTokens.Add(replacement);

        IdentitySnapshotResponse identity = await CreateIdentitySnapshotAsync(user, session, cancellationToken);
        (string accessToken, DateTimeOffset accessExpiresAt) = jwtTokenIssuer.Issue(
            user.Id,
            session.Id,
            session.AuthenticatedAt,
            user.AuthorizationVersion,
            ParseAuthenticationMethods(session.AuthenticationMethods),
            now);
        return Result.Success(new AuthenticatedSessionResponse(
            accessToken,
            accessExpiresAt,
            replacementRawToken,
            identity));
    }

    public async Task<Result<OperationCompletedResponse>> SignOutAsync(
        SignOutCommand request,
        CancellationToken cancellationToken)
    {
        RefreshSession? session = await dbContext.RefreshSessions.SingleOrDefaultAsync(
            candidate => candidate.Id == request.SessionId && candidate.UserId == request.UserId,
            cancellationToken);
        if (session is not null)
        {
            DateTimeOffset now = timeProvider.GetUtcNow();
            session.Revoke(now, "user-sign-out");
            dbContext.SecurityEvents.Add(SecurityEvent.Create(
                request.UserId,
                request.SessionId,
                "session.signed-out",
                now));
        }

        return Result.Success(new OperationCompletedResponse(true));
    }

    public async Task<Result<OperationCompletedResponse>> SignOutAllAsync(
        SignOutAllSessionsCommand request,
        CancellationToken cancellationToken)
    {
        ApplicationUser? user = await userManager.FindByIdAsync(request.UserId.ToString("D"));
        if (user is null)
        {
            return Result.Failure<OperationCompletedResponse>(InvalidSession);
        }

        await RevokeAllSessionsAsync(user, "user-sign-out-all", cancellationToken);
        return Result.Success(new OperationCompletedResponse(true));
    }

    public async Task<Result<SessionsResponse>> GetSessionsAsync(
        GetSessionsQuery request,
        CancellationToken cancellationToken)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        List<RefreshSession> sessions = await dbContext.RefreshSessions
            .AsNoTracking()
            .Where(session => session.UserId == request.UserId &&
                session.RevokedAt == null &&
                session.IdleExpiresAt > now &&
                session.AbsoluteExpiresAt > now)
            .OrderByDescending(session => session.LastUsedAt)
            .ToListAsync(cancellationToken);
        return Result.Success(new SessionsResponse(sessions
            .Select(session => new SessionSummaryResponse(
                session.Id,
                session.Id == request.CurrentSessionId,
                session.DeviceName,
                session.CreatedAt,
                session.LastUsedAt,
                session.IdleExpiresAt,
                session.AbsoluteExpiresAt))
            .ToArray()));
    }

    public async Task<Result<OperationCompletedResponse>> RevokeSessionAsync(
        RevokeSessionCommand request,
        CancellationToken cancellationToken)
    {
        RefreshSession? session = await dbContext.RefreshSessions.SingleOrDefaultAsync(
            candidate => candidate.Id == request.SessionId && candidate.UserId == request.UserId,
            cancellationToken);
        if (session is null)
        {
            return Result.Failure<OperationCompletedResponse>(ResultError.NotFound(
                "SESSION.NOT_FOUND",
                "The session was not found."));
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        session.Revoke(now, "user-revoked-session");
        dbContext.SecurityEvents.Add(SecurityEvent.Create(
            request.UserId,
            request.SessionId,
            "session.revoked",
            now));
        return Result.Success(new OperationCompletedResponse(true));
    }

    public async Task<Result<NeutralAcceptedResponse>> SendEmailVerificationAsync(
        SendEmailVerificationCommand request,
        CancellationToken cancellationToken)
    {
        ResultError? rateError = await CheckAccountActionRateAsync(
            "verification",
            request.Email,
            request.Context,
            3,
            TimeSpan.FromHours(1));
        if (rateError is not null)
        {
            return Result.Failure<NeutralAcceptedResponse>(rateError);
        }

        ApplicationUser? user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is not null && user.IsActive && !user.EmailConfirmed)
        {
            QueueIdentityEmail(user.Id, VerificationEmailEvent, request.Locale);
        }

        return Result.Success(new NeutralAcceptedResponse(true));
    }

    public async Task<Result<OperationCompletedResponse>> ConfirmEmailAsync(
        ConfirmEmailCommand request,
        CancellationToken cancellationToken)
    {
        ApplicationUser? user = await userManager.FindByIdAsync(request.UserId.ToString("D"));
        if (user is null || !user.IsActive)
        {
            return Result.Failure<OperationCompletedResponse>(ResultError.BusinessRule(
                "AUTH.EMAIL_VERIFICATION_INVALID",
                "The email verification link is invalid or expired."));
        }

        if (user.EmailConfirmed)
        {
            return Result.Success(new OperationCompletedResponse(true));
        }

        IdentityResult confirmed = await userManager.ConfirmEmailAsync(user, request.Token);
        if (!confirmed.Succeeded)
        {
            return Result.Failure<OperationCompletedResponse>(ResultError.BusinessRule(
                "AUTH.EMAIL_VERIFICATION_INVALID",
                "The email verification link is invalid or expired."));
        }

        dbContext.SecurityEvents.Add(SecurityEvent.Create(
            user.Id,
            null,
            "account.email-confirmed",
            timeProvider.GetUtcNow()));
        return Result.Success(new OperationCompletedResponse(true));
    }

    public async Task<Result<NeutralAcceptedResponse>> ForgotPasswordAsync(
        ForgotPasswordCommand request,
        CancellationToken cancellationToken)
    {
        ResultError? rateError = await CheckAccountActionRateAsync(
            "password-reset",
            request.Email,
            request.Context,
            3,
            TimeSpan.FromHours(1),
            10);
        if (rateError is not null)
        {
            return Result.Failure<NeutralAcceptedResponse>(rateError);
        }

        ApplicationUser? user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is not null && user.IsActive && user.EmailConfirmed)
        {
            QueueIdentityEmail(user.Id, PasswordResetEmailEvent, request.Locale);
        }

        return Result.Success(new NeutralAcceptedResponse(true));
    }

    public async Task<Result<OperationCompletedResponse>> ResetPasswordAsync(
        ResetPasswordCommand request,
        CancellationToken cancellationToken)
    {
        ResultError? passwordError = await CheckPasswordAsync(request.NewPassword, cancellationToken);
        if (passwordError is not null)
        {
            return Result.Failure<OperationCompletedResponse>(passwordError);
        }

        ApplicationUser? user = await userManager.FindByIdAsync(request.UserId.ToString("D"));
        if (user is null || !user.IsActive)
        {
            return Result.Failure<OperationCompletedResponse>(ResultError.BusinessRule(
                "AUTH.PASSWORD_RESET_INVALID",
                "The password reset link is invalid or expired."));
        }

        IdentityResult reset = await userManager.ResetPasswordAsync(user, request.Token, request.NewPassword);
        if (!reset.Succeeded)
        {
            return Result.Failure<OperationCompletedResponse>(ResultError.BusinessRule(
                "AUTH.PASSWORD_RESET_INVALID",
                "The password reset link is invalid or expired."));
        }

        await RevokeAllSessionsAsync(user, "password-reset", cancellationToken);
        dbContext.SecurityEvents.Add(SecurityEvent.Create(
            user.Id,
            null,
            "account.password-reset",
            timeProvider.GetUtcNow()));
        return Result.Success(new OperationCompletedResponse(true));
    }

    public async Task<Result<OperationCompletedResponse>> ChangePasswordAsync(
        ChangePasswordCommand request,
        CancellationToken cancellationToken)
    {
        ApplicationUser? user = await userManager.FindByIdAsync(request.UserId.ToString("D"));
        RefreshSession? session = await GetActiveOwnedSessionAsync(
            request.UserId,
            request.SessionId,
            cancellationToken);
        if (user is null || session is null)
        {
            return Result.Failure<OperationCompletedResponse>(InvalidSession);
        }

        ResultError? passwordError = await CheckPasswordAsync(request.NewPassword, cancellationToken);
        if (passwordError is not null)
        {
            return Result.Failure<OperationCompletedResponse>(passwordError);
        }

        IdentityResult changed = await userManager.ChangePasswordAsync(
            user,
            request.CurrentPassword,
            request.NewPassword);
        if (!changed.Succeeded)
        {
            return Result.Failure<OperationCompletedResponse>(ResultError.Unauthorized(
                "AUTH.CURRENT_PASSWORD_INVALID",
                "The current password is invalid."));
        }

        await RevokeAllSessionsAsync(user, "password-changed", cancellationToken);
        dbContext.SecurityEvents.Add(SecurityEvent.Create(
            user.Id,
            request.SessionId,
            "account.password-changed",
            timeProvider.GetUtcNow()));
        return Result.Success(new OperationCompletedResponse(true));
    }

    public async Task<Result<MfaSetupResponse>> SetupMfaAsync(
        SetupMfaCommand request,
        CancellationToken cancellationToken)
    {
        ApplicationUser? user = await userManager.FindByIdAsync(request.UserId.ToString("D"));
        RefreshSession? session = await GetActiveOwnedSessionAsync(
            request.UserId,
            request.SessionId,
            cancellationToken);
        if (user is null || session is null)
        {
            return Result.Failure<MfaSetupResponse>(InvalidSession);
        }
        if (!HasRecentAuthentication(session))
        {
            return Result.Failure<MfaSetupResponse>(RecentAuthenticationRequired());
        }

        string secret = Base32Encoding.ToString(RandomNumberGenerator.GetBytes(20));
        user.ProtectedPendingMfaSecret = _mfaProtector.Protect(secret);
        user.UpdatedAt = timeProvider.GetUtcNow();
        IdentityResult updated = await userManager.UpdateAsync(user);
        if (!updated.Succeeded)
        {
            return Result.Failure<MfaSetupResponse>(MapIdentityFailure(
                updated,
                "MFA.SETUP_FAILED",
                "MFA setup could not be started."));
        }

        string label = Uri.EscapeDataString($"Dorosak:{user.Email}");
        string uri = $"otpauth://totp/{label}?secret={secret}&issuer=Dorosak&algorithm=SHA1&digits=6&period=30";
        return Result.Success(new MfaSetupResponse(secret, uri));
    }

    public async Task<Result<MfaConfirmationResponse>> ConfirmMfaAsync(
        ConfirmMfaCommand request,
        CancellationToken cancellationToken)
    {
        ApplicationUser? user = await userManager.FindByIdAsync(request.UserId.ToString("D"));
        RefreshSession? session = await GetActiveOwnedSessionAsync(
            request.UserId,
            request.SessionId,
            cancellationToken);
        if (user is null || session is null || string.IsNullOrWhiteSpace(user.ProtectedPendingMfaSecret))
        {
            return Result.Failure<MfaConfirmationResponse>(ResultError.BusinessRule(
                "MFA.SETUP_NOT_STARTED",
                "MFA setup has not been started."));
        }
        if (!HasRecentAuthentication(session))
        {
            return Result.Failure<MfaConfirmationResponse>(RecentAuthenticationRequired());
        }

        string secret;
        try
        {
            secret = _mfaProtector.Unprotect(user.ProtectedPendingMfaSecret);
        }
        catch (CryptographicException)
        {
            return Result.Failure<MfaConfirmationResponse>(ResultError.BusinessRule(
                "MFA.SETUP_INVALID",
                "MFA setup is invalid and must be restarted."));
        }

        var totp = new Totp(Base32Encoding.ToBytes(secret));
        if (!totp.VerifyTotp(request.Code, out long matchedTimeStep, VerificationWindow.RfcSpecifiedNetworkDelay))
        {
            return Result.Failure<MfaConfirmationResponse>(ResultError.BusinessRule(
                "MFA.INVALID_CODE",
                "The MFA code is invalid."));
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        user.ProtectedMfaSecret = user.ProtectedPendingMfaSecret;
        user.ProtectedPendingMfaSecret = null;
        user.TwoFactorEnabled = true;
        user.LastMfaTimeStep = matchedTimeStep;
        user.SecurityVersion++;
        user.UpdatedAt = now;
        await dbContext.MfaRecoveryCodes
            .Where(code => code.UserId == user.Id)
            .ExecuteDeleteAsync(cancellationToken);

        string[] recoveryCodes = Enumerable.Range(0, _securityOptions.RecoveryCodeCount)
            .Select(_ => GenerateRecoveryCode())
            .ToArray();
        dbContext.MfaRecoveryCodes.AddRange(recoveryCodes.Select(code =>
            MfaRecoveryCode.Create(user.Id, HashValue(NormalizeRecoveryCode(code)), now)));
        IdentityResult updated = await userManager.UpdateAsync(user);
        if (!updated.Succeeded)
        {
            return Result.Failure<MfaConfirmationResponse>(MapIdentityFailure(
                updated,
                "MFA.CONFIRMATION_FAILED",
                "MFA could not be enabled."));
        }

        dbContext.SecurityEvents.Add(SecurityEvent.Create(user.Id, request.SessionId, "mfa.enabled", now));
        return Result.Success(new MfaConfirmationResponse(recoveryCodes));
    }

    public async Task<Result<OperationCompletedResponse>> DisableMfaAsync(
        DisableMfaCommand request,
        CancellationToken cancellationToken)
    {
        ApplicationUser? user = await userManager.FindByIdAsync(request.UserId.ToString("D"));
        RefreshSession? session = await GetActiveOwnedSessionAsync(
            request.UserId,
            request.SessionId,
            cancellationToken);
        if (user is null || session is null)
        {
            return Result.Failure<OperationCompletedResponse>(InvalidSession);
        }
        if (!HasRecentAuthentication(session))
        {
            return Result.Failure<OperationCompletedResponse>(RecentAuthenticationRequired());
        }
        if (await userManager.IsInRoleAsync(user, IdentityConstants.AdminRole))
        {
            return Result.Failure<OperationCompletedResponse>(ResultError.Forbidden(
                "MFA.ADMIN_REQUIRED",
                "MFA is mandatory for administrators."));
        }
        if (!await userManager.CheckPasswordAsync(user, request.CurrentPassword))
        {
            return Result.Failure<OperationCompletedResponse>(ResultError.Unauthorized(
                "AUTH.CURRENT_PASSWORD_INVALID",
                "The current password is invalid."));
        }

        user.ProtectedMfaSecret = null;
        user.ProtectedPendingMfaSecret = null;
        user.LastMfaTimeStep = null;
        user.TwoFactorEnabled = false;
        user.SecurityVersion++;
        user.UpdatedAt = timeProvider.GetUtcNow();
        await dbContext.MfaRecoveryCodes
            .Where(code => code.UserId == user.Id)
            .ExecuteDeleteAsync(cancellationToken);
        await RevokeAllSessionsAsync(user, "mfa-disabled", cancellationToken);
        dbContext.SecurityEvents.Add(SecurityEvent.Create(
            user.Id,
            request.SessionId,
            "mfa.disabled",
            timeProvider.GetUtcNow()));
        return Result.Success(new OperationCompletedResponse(true));
    }

    public async Task<Result<IdentitySnapshotResponse>> GetProfileAsync(
        GetCurrentProfileQuery request,
        CancellationToken cancellationToken)
    {
        ApplicationUser? user = await userManager.FindByIdAsync(request.UserId.ToString("D"));
        RefreshSession? session = await GetActiveOwnedSessionAsync(
            request.UserId,
            request.SessionId,
            cancellationToken);
        if (user is null || session is null)
        {
            return Result.Failure<IdentitySnapshotResponse>(InvalidSession);
        }

        return Result.Success(await CreateIdentitySnapshotAsync(user, session, cancellationToken));
    }

    private async Task<AuthenticatedSessionResponse> CreateSessionAsync(
        ApplicationUser user,
        IdentityRequestContext context,
        IReadOnlyList<string> authenticationMethods,
        CancellationToken cancellationToken)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        var session = RefreshSession.Create(
            user.Id,
            now,
            TimeSpan.FromDays(_securityOptions.RefreshIdleDays),
            TimeSpan.FromDays(_securityOptions.RefreshAbsoluteDays),
            NormalizeDeviceName(context.UserAgent),
            HashValue(context.IpAddress),
            string.Join(' ', authenticationMethods),
            user.AuthorizationVersion);
        string rawRefreshToken = GenerateOpaqueToken(64);
        var refreshToken = RefreshToken.Create(
            session.Id,
            session.FamilyId,
            HashValue(rawRefreshToken),
            now,
            session.AbsoluteExpiresAt);
        dbContext.RefreshSessions.Add(session);
        dbContext.RefreshTokens.Add(refreshToken);
        dbContext.SecurityEvents.Add(SecurityEvent.Create(
            user.Id,
            session.Id,
            "session.created",
            now,
            HashValue(context.IpAddress)));

        IdentitySnapshotResponse identity = await CreateIdentitySnapshotAsync(user, session, cancellationToken);
        (string accessToken, DateTimeOffset accessExpiresAt) = jwtTokenIssuer.Issue(
            user.Id,
            session.Id,
            session.AuthenticatedAt,
            user.AuthorizationVersion,
            authenticationMethods,
            now);
        return new AuthenticatedSessionResponse(accessToken, accessExpiresAt, rawRefreshToken, identity);
    }

    private async Task<IdentitySnapshotResponse> CreateIdentitySnapshotAsync(
        ApplicationUser user,
        RefreshSession session,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<string> roles = await GetRolesAsync(user);
        string[] normalizedRoles = roles.Select(role => role.ToUpperInvariant()).ToArray();
        string[] permissions = await dbContext.RoleClaims
            .AsNoTracking()
            .Where(claim => claim.ClaimType == IdentityConstants.PermissionClaimType &&
                dbContext.Roles.Any(role =>
                    normalizedRoles.Contains(role.NormalizedName!) && role.Id == claim.RoleId))
            .Select(claim => claim.ClaimValue!)
            .Distinct()
            .OrderBy(permission => permission)
            .ToArrayAsync(cancellationToken);

        return new IdentitySnapshotResponse(
            user.Id,
            session.Id,
            user.DisplayName,
            user.Email ?? string.Empty,
            user.EmailConfirmed,
            user.TwoFactorEnabled,
            session.AuthenticatedAt,
            session.AuthenticatedAt.AddMinutes(_securityOptions.RecentAuthenticationMinutes),
            user.AuthorizationVersion,
            roles,
            permissions,
            ParseAuthenticationMethods(session.AuthenticationMethods));
    }

    private async Task<IReadOnlyList<string>> GetRolesAsync(ApplicationUser user) =>
        (await userManager.GetRolesAsync(user)).OrderBy(role => role, StringComparer.Ordinal).ToArray();

    private async Task<RefreshSession?> GetActiveOwnedSessionAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        RefreshSession? session = await dbContext.RefreshSessions.SingleOrDefaultAsync(
            candidate => candidate.Id == sessionId && candidate.UserId == userId,
            cancellationToken);
        return session is not null && session.IsActive(timeProvider.GetUtcNow()) ? session : null;
    }

    private async Task RevokeAllSessionsAsync(
        ApplicationUser user,
        string reason,
        CancellationToken cancellationToken)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        List<RefreshSession> sessions = await dbContext.RefreshSessions
            .Where(session => session.UserId == user.Id && session.RevokedAt == null)
            .ToListAsync(cancellationToken);
        foreach (RefreshSession session in sessions)
        {
            session.Revoke(now, reason);
        }

        user.SecurityVersion++;
        user.AuthorizationVersion++;
        user.UpdatedAt = now;
        IdentityResult updated = await userManager.UpdateAsync(user);
        if (!updated.Succeeded)
        {
            throw new InvalidOperationException("The account security version could not be updated.");
        }
    }

    private async Task<MfaChallenge?> FindMfaChallengeForUpdateAsync(
        string tokenHash,
        CancellationToken cancellationToken) =>
        await dbContext.MfaChallenges
            .FromSqlInterpolated($"SELECT * FROM identity.mfa_challenges WHERE token_hash = {tokenHash} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);

    private (string RawToken, MfaChallenge Challenge) CreateMfaChallenge(Guid userId)
    {
        string rawToken = GenerateOpaqueToken(48);
        DateTimeOffset now = timeProvider.GetUtcNow();
        return (
            rawToken,
            MfaChallenge.Create(
                userId,
                HashValue(rawToken),
                now,
                TimeSpan.FromMinutes(_securityOptions.MfaChallengeMinutes)));
    }

    private void QueueIdentityEmail(Guid userId, string eventType, string locale)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        string payload = JsonSerializer.Serialize(new IdentityEmailRequested(userId, NormalizeLocale(locale)));
        dbContext.OutboxMessages.Add(OutboxMessage.Create(eventType, 1, payload, "{}", now));
    }

    private async Task<ResultError?> CheckRegistrationRateAsync(IdentityRequestContext context) =>
        ConvertRateResult(await rateLimiter.CheckAsync(
            "register:ip",
            context.IpAddress,
            5,
            TimeSpan.FromHours(1)));

    private async Task<ResultError?> CheckSignInRateAsync(string email, IdentityRequestContext context)
    {
        SecurityRateLimitResult account = await rateLimiter.CheckAsync(
            "sign-in:account",
            email,
            5,
            TimeSpan.FromMinutes(5));
        ResultError? accountError = ConvertRateResult(account);
        if (accountError is not null)
        {
            return accountError;
        }

        return ConvertRateResult(await rateLimiter.CheckAsync(
            "sign-in:ip",
            context.IpAddress,
            20,
            TimeSpan.FromMinutes(5)));
    }

    private async Task<ResultError?> CheckRefreshRateAsync(string refreshToken) =>
        ConvertRateResult(await rateLimiter.CheckAsync(
            "refresh:session",
            HashValue(refreshToken),
            30,
            TimeSpan.FromMinutes(5)));

    private async Task<ResultError?> CheckAccountActionRateAsync(
        string operation,
        string email,
        IdentityRequestContext context,
        int accountLimit,
        TimeSpan window,
        int ipLimit = 10)
    {
        SecurityRateLimitResult account = await rateLimiter.CheckAsync(
            $"{operation}:account",
            email,
            accountLimit,
            window);
        ResultError? accountError = ConvertRateResult(account);
        if (accountError is not null)
        {
            return accountError;
        }

        return ConvertRateResult(await rateLimiter.CheckAsync(
            $"{operation}:ip",
            context.IpAddress,
            ipLimit,
            window));
    }

    private static ResultError? ConvertRateResult(SecurityRateLimitResult result)
    {
        if (result.IsAllowed)
        {
            return null;
        }

        TimeSpan retryAfter = result.RetryAfter ?? TimeSpan.FromSeconds(30);
        return result.IsAvailable
            ? ResultError.RateLimited(
                "RATE_LIMIT.EXCEEDED",
                "The request limit was exceeded. Try again later.",
                retryAfter)
            : ResultError.ServiceUnavailable(
                "SECURITY.RATE_LIMIT_UNAVAILABLE",
                "The security service is temporarily unavailable.",
                retryAfter);
    }

    private async Task<ResultError?> CheckPasswordAsync(string password, CancellationToken cancellationToken)
    {
        PasswordBreachResult breach = await breachedPasswordService.CheckAsync(password, cancellationToken);
        if (!breach.IsAvailable)
        {
            return ResultError.ServiceUnavailable(
                "SECURITY.PASSWORD_CHECK_UNAVAILABLE",
                "Password safety verification is temporarily unavailable.",
                TimeSpan.FromSeconds(30));
        }

        return breach.IsBreached
            ? ResultError.BusinessRule(
                "AUTH.PASSWORD_COMPROMISED",
                "Choose a password that has not appeared in known data breaches.")
            : null;
    }

    private bool HasRecentAuthentication(RefreshSession session) =>
        session.AuthenticatedAt.AddMinutes(_securityOptions.RecentAuthenticationMinutes) >= timeProvider.GetUtcNow();

    private static ResultError RecentAuthenticationRequired() => ResultError.Forbidden(
        "AUTH.RECENT_AUTHENTICATION_REQUIRED",
        "Recent authentication is required for this operation.");

    private static ResultError MapIdentityFailure(IdentityResult result, string code, string description)
    {
        string[] messages = result.Errors.Select(error => error.Description).Distinct(StringComparer.Ordinal).ToArray();
        return messages.Length == 0
            ? ResultError.BusinessRule(code, description)
            : new ResultError(
                code,
                description,
                ErrorType.Validation,
                new Dictionary<string, string[]>(StringComparer.Ordinal) { ["identity"] = messages });
    }

    private static string GenerateOpaqueToken(int byteCount) =>
        WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(byteCount));

    private static string GenerateRecoveryCode()
    {
        string value = Base32Encoding.ToString(RandomNumberGenerator.GetBytes(10));
        return $"{value[..5]}-{value[5..10]}-{value[10..]}";
    }

    private static string NormalizeRecoveryCode(string value) =>
        value.Replace("-", string.Empty, StringComparison.Ordinal).Trim().ToUpperInvariant();

    private static string HashValue(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static string NormalizeDeviceName(string userAgent)
    {
        string value = string.IsNullOrWhiteSpace(userAgent) ? "Unknown device" : userAgent.Trim();
        return value.Length <= 300 ? value : value[..300];
    }

    private static string NormalizeLocale(string locale) =>
        string.Equals(locale, "en", StringComparison.OrdinalIgnoreCase) ? "en" : "ar";

    private static string[] ParseAuthenticationMethods(string value) =>
        value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private sealed record IdentityEmailRequested(Guid UserId, string Locale);
}
