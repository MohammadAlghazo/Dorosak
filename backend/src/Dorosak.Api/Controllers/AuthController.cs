using System.Security.Claims;
using Asp.Versioning;
using Dorosak.Api.Extensions;
using Dorosak.Application.Common.Results;
using Dorosak.Application.Features.Identity;
using Dorosak.Infrastructure.Identity;
using MediatR;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Dorosak.Api.Controllers;

[ApiController]
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/auth")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AuthController(
    ISender sender,
    IAntiforgery antiforgery,
    IOptions<IdentitySecurityOptions> securityOptions) : ControllerBase
{
    private readonly IdentitySecurityOptions _securityOptions = securityOptions.Value;

    [AllowAnonymous]
    [HttpGet("csrf")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public IActionResult GetCsrf()
    {
        AntiforgeryTokenSet tokens = antiforgery.GetAndStoreTokens(HttpContext);
        if (string.IsNullOrWhiteSpace(tokens.RequestToken))
        {
            throw new InvalidOperationException("The antiforgery request token was not generated.");
        }

        return NoContent();
    }

    [AllowAnonymous]
    [HttpPost("register")]
    [ProducesResponseType<ApiResponse<RegistrationAcceptedResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Register(
        RegisterRequest request,
        CancellationToken cancellationToken)
    {
        await antiforgery.ValidateRequestAsync(HttpContext);
        Result<RegistrationAcceptedResponse> result = await sender.Send(
            new RegisterAccountCommand(
                request.DisplayName,
                request.Email,
                request.Password,
                CreateContext()),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [AllowAnonymous]
    [HttpPost("sign-in")]
    [ProducesResponseType<ApiResponse<SignInPublicResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> SignIn(SignInRequest request, CancellationToken cancellationToken)
    {
        await antiforgery.ValidateRequestAsync(HttpContext);
        Result<SignInResponse> result = await sender.Send(
            new SignInCommand(request.Email, request.Password, CreateContext()),
            cancellationToken);
        if (!result.IsSuccess)
        {
            return this.ToActionResult(result);
        }

        SignInResponse signIn = result.Value;
        if (signIn.Session is { } session)
        {
            SetRefreshCookie(session.RefreshToken);
        }

        var response = new SignInPublicResponse(
            signIn.Outcome,
            signIn.Session is null ? null : ToPublicSession(signIn.Session),
            signIn.ChallengeToken,
            signIn.ChallengeExpiresAt);
        return Ok(new ApiResponse<SignInPublicResponse>(response));
    }

    [AllowAnonymous]
    [HttpPost("mfa/challenge")]
    [ProducesResponseType<ApiResponse<AuthenticatedSessionPublicResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> CompleteMfa(
        MfaChallengeRequest request,
        CancellationToken cancellationToken)
    {
        await antiforgery.ValidateRequestAsync(HttpContext);
        Result<AuthenticatedSessionResponse> result = await sender.Send(
            new CompleteMfaChallengeCommand(request.ChallengeToken, request.Code, CreateContext()),
            cancellationToken);
        return CompleteSession(result);
    }

    [AllowAnonymous]
    [HttpPost("mfa/recovery")]
    [ProducesResponseType<ApiResponse<AuthenticatedSessionPublicResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> CompleteMfaRecovery(
        MfaRecoveryRequest request,
        CancellationToken cancellationToken)
    {
        await antiforgery.ValidateRequestAsync(HttpContext);
        Result<AuthenticatedSessionResponse> result = await sender.Send(
            new CompleteMfaRecoveryCommand(request.ChallengeToken, request.RecoveryCode, CreateContext()),
            cancellationToken);
        return CompleteSession(result);
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    [ProducesResponseType<ApiResponse<AuthenticatedSessionPublicResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Refresh(CancellationToken cancellationToken)
    {
        await antiforgery.ValidateRequestAsync(HttpContext);
        if (!Request.Cookies.TryGetValue(_securityOptions.RefreshCookieName, out string? refreshToken) ||
            string.IsNullOrWhiteSpace(refreshToken))
        {
            return UnauthorizedProblem("SESSION.INVALID", "The session is missing, expired, or revoked.");
        }

        Result<AuthenticatedSessionResponse> result = await sender.Send(
            new RefreshSessionCommand(refreshToken, CreateContext()),
            cancellationToken);
        if (!result.IsSuccess && result.Failure.Code != "SESSION.REFRESH_RACE")
        {
            DeleteRefreshCookie();
        }

        return CompleteSession(result);
    }

    [Authorize]
    [HttpPost("sign-out")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SignOut(CancellationToken cancellationToken)
    {
        await antiforgery.ValidateRequestAsync(HttpContext);
        if (!TryGetCurrentIdentity(out Guid userId, out Guid sessionId))
        {
            return UnauthorizedProblem("SESSION.INVALID", "The session is missing, expired, or revoked.");
        }

        Result<OperationCompletedResponse> result = await sender.Send(
            new SignOutCommand(userId, sessionId),
            cancellationToken);
        if (!result.IsSuccess)
        {
            return this.ToActionResult(result);
        }

        DeleteRefreshCookie();
        return NoContent();
    }

    [AllowAnonymous]
    [HttpPost("email-verification/send")]
    public async Task<IActionResult> SendEmailVerification(
        EmailRequest request,
        CancellationToken cancellationToken)
    {
        await antiforgery.ValidateRequestAsync(HttpContext);
        Result<NeutralAcceptedResponse> result = await sender.Send(
            new SendEmailVerificationCommand(request.Email, request.Locale, CreateContext()),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [AllowAnonymous]
    [HttpPost("email-verification/confirm")]
    public async Task<IActionResult> ConfirmEmail(
        TokenRequest request,
        CancellationToken cancellationToken)
    {
        await antiforgery.ValidateRequestAsync(HttpContext);
        Result<OperationCompletedResponse> result = await sender.Send(
            new ConfirmEmailCommand(request.UserId, request.Token),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [AllowAnonymous]
    [HttpPost("password/forgot")]
    public async Task<IActionResult> ForgotPassword(
        EmailRequest request,
        CancellationToken cancellationToken)
    {
        await antiforgery.ValidateRequestAsync(HttpContext);
        Result<NeutralAcceptedResponse> result = await sender.Send(
            new ForgotPasswordCommand(request.Email, request.Locale, CreateContext()),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [AllowAnonymous]
    [HttpPost("password/reset")]
    public async Task<IActionResult> ResetPassword(
        ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        await antiforgery.ValidateRequestAsync(HttpContext);
        Result<OperationCompletedResponse> result = await sender.Send(
            new ResetPasswordCommand(request.UserId, request.Token, request.NewPassword),
            cancellationToken);
        if (result.IsSuccess)
        {
            DeleteRefreshCookie();
        }
        return this.ToActionResult(result);
    }

    [Authorize]
    [HttpPost("password/change")]
    public async Task<IActionResult> ChangePassword(
        ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        await antiforgery.ValidateRequestAsync(HttpContext);
        if (!TryGetCurrentIdentity(out Guid userId, out Guid sessionId))
        {
            return UnauthorizedProblem("SESSION.INVALID", "The session is missing, expired, or revoked.");
        }

        Result<OperationCompletedResponse> result = await sender.Send(
            new ChangePasswordCommand(userId, sessionId, request.CurrentPassword, request.NewPassword),
            cancellationToken);
        if (result.IsSuccess)
        {
            DeleteRefreshCookie();
        }
        return this.ToActionResult(result);
    }

    [Authorize]
    [HttpPost("mfa/setup")]
    public async Task<IActionResult> SetupMfa(CancellationToken cancellationToken)
    {
        await antiforgery.ValidateRequestAsync(HttpContext);
        if (!TryGetCurrentIdentity(out Guid userId, out Guid sessionId))
        {
            return UnauthorizedProblem("SESSION.INVALID", "The session is missing, expired, or revoked.");
        }

        Result<MfaSetupResponse> result = await sender.Send(
            new SetupMfaCommand(userId, sessionId),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [Authorize]
    [HttpPost("mfa/confirm")]
    public async Task<IActionResult> ConfirmMfa(
        CodeRequest request,
        CancellationToken cancellationToken)
    {
        await antiforgery.ValidateRequestAsync(HttpContext);
        if (!TryGetCurrentIdentity(out Guid userId, out Guid sessionId))
        {
            return UnauthorizedProblem("SESSION.INVALID", "The session is missing, expired, or revoked.");
        }

        Result<MfaConfirmationResponse> result = await sender.Send(
            new ConfirmMfaCommand(userId, sessionId, request.Code),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [Authorize]
    [HttpDelete("mfa")]
    public async Task<IActionResult> DisableMfa(
        DisableMfaRequest request,
        CancellationToken cancellationToken)
    {
        await antiforgery.ValidateRequestAsync(HttpContext);
        if (!TryGetCurrentIdentity(out Guid userId, out Guid sessionId))
        {
            return UnauthorizedProblem("SESSION.INVALID", "The session is missing, expired, or revoked.");
        }

        Result<OperationCompletedResponse> result = await sender.Send(
            new DisableMfaCommand(userId, sessionId, request.CurrentPassword),
            cancellationToken);
        if (result.IsSuccess)
        {
            DeleteRefreshCookie();
        }
        return this.ToActionResult(result);
    }

    private IActionResult CompleteSession(Result<AuthenticatedSessionResponse> result)
    {
        if (!result.IsSuccess)
        {
            return this.ToActionResult(result);
        }

        SetRefreshCookie(result.Value.RefreshToken);
        return Ok(new ApiResponse<AuthenticatedSessionPublicResponse>(ToPublicSession(result.Value)));
    }

    private static AuthenticatedSessionPublicResponse ToPublicSession(AuthenticatedSessionResponse session) =>
        new(session.AccessToken, session.AccessTokenExpiresAt, session.Identity);

    private IdentityRequestContext CreateContext() => new(
        HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        Request.Headers.UserAgent.ToString(),
        Request.Headers.AcceptLanguage.FirstOrDefault()?.Split(',')[0].Split('-')[0] ?? "ar");

    private bool TryGetCurrentIdentity(out Guid userId, out Guid sessionId)
    {
        bool hasUser = Guid.TryParse(User.FindFirstValue("sub"), out userId);
        bool hasSession = Guid.TryParse(User.FindFirstValue("sid"), out sessionId);
        return hasUser && hasSession;
    }

    private void SetRefreshCookie(string value) => Response.Cookies.Append(
        _securityOptions.RefreshCookieName,
        value,
        CreateRefreshCookieOptions(DateTimeOffset.UtcNow.AddDays(_securityOptions.RefreshAbsoluteDays)));

    private void DeleteRefreshCookie() => Response.Cookies.Delete(
        _securityOptions.RefreshCookieName,
        CreateRefreshCookieOptions(null));

    private static CookieOptions CreateRefreshCookieOptions(DateTimeOffset? expires) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Lax,
        Path = "/api/v1/auth",
        IsEssential = true,
        Expires = expires,
    };

    private static ObjectResult UnauthorizedProblem(string code, string detail) => new(new ProblemDetails
    {
        Status = StatusCodes.Status401Unauthorized,
        Title = "Unauthorized",
        Type = $"https://dorosak.com/problems/{code.ToLowerInvariant().Replace('.', '-')}",
        Detail = detail,
        Extensions = { ["code"] = code },
    })
    {
        StatusCode = StatusCodes.Status401Unauthorized,
    };
}

public sealed record RegisterRequest(string DisplayName, string Email, string Password);

public sealed record SignInRequest(string Email, string Password);

public sealed record MfaChallengeRequest(string ChallengeToken, string Code);

public sealed record MfaRecoveryRequest(string ChallengeToken, string RecoveryCode);

public sealed record EmailRequest(string Email, string Locale = "ar");

public sealed record TokenRequest(Guid UserId, string Token);

public sealed record ResetPasswordRequest(Guid UserId, string Token, string NewPassword);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public sealed record CodeRequest(string Code);

public sealed record DisableMfaRequest(string CurrentPassword);

public sealed record SignInPublicResponse(
    string Outcome,
    AuthenticatedSessionPublicResponse? Session,
    string? ChallengeToken,
    DateTimeOffset? ChallengeExpiresAt);

public sealed record AuthenticatedSessionPublicResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    IdentitySnapshotResponse Identity);
