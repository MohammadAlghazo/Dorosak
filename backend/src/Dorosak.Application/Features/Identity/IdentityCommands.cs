using Dorosak.Application.Common.Identity;
using Dorosak.Application.Common.Messaging;
using Dorosak.Application.Common.Results;
using MediatR;

namespace Dorosak.Application.Features.Identity;

public sealed record RegisterAccountCommand(
    string DisplayName,
    string Email,
    string Password,
    IdentityRequestContext Context) : ITransactionalCommand<RegistrationAcceptedResponse>;

public sealed record SignInCommand(
    string Email,
    string Password,
    IdentityRequestContext Context) : ITransactionalCommand<SignInResponse>;

public sealed record CompleteMfaChallengeCommand(
    string ChallengeToken,
    string Code,
    IdentityRequestContext Context) : ITransactionalCommand<AuthenticatedSessionResponse>;

public sealed record CompleteMfaRecoveryCommand(
    string ChallengeToken,
    string RecoveryCode,
    IdentityRequestContext Context) : ITransactionalCommand<AuthenticatedSessionResponse>;

public sealed record RefreshSessionCommand(
    string RefreshToken,
    IdentityRequestContext Context) : ITransactionalCommand<AuthenticatedSessionResponse>;

public sealed record SignOutCommand(Guid UserId, Guid SessionId) : ITransactionalCommand<OperationCompletedResponse>;

public sealed record SignOutAllSessionsCommand(Guid UserId) : ITransactionalCommand<OperationCompletedResponse>;

public sealed record RevokeSessionCommand(
    Guid UserId,
    Guid CurrentSessionId,
    Guid SessionId) : ITransactionalCommand<OperationCompletedResponse>;

public sealed record SendEmailVerificationCommand(
    string Email,
    string Locale,
    IdentityRequestContext Context) : ITransactionalCommand<NeutralAcceptedResponse>;

public sealed record ConfirmEmailCommand(Guid UserId, string Token) : ITransactionalCommand<OperationCompletedResponse>;

public sealed record ForgotPasswordCommand(
    string Email,
    string Locale,
    IdentityRequestContext Context) : ITransactionalCommand<NeutralAcceptedResponse>;

public sealed record ResetPasswordCommand(
    Guid UserId,
    string Token,
    string NewPassword) : ITransactionalCommand<OperationCompletedResponse>;

public sealed record ChangePasswordCommand(
    Guid UserId,
    Guid SessionId,
    string CurrentPassword,
    string NewPassword) : ITransactionalCommand<OperationCompletedResponse>;

public sealed record SetupMfaCommand(Guid UserId, Guid SessionId) : ITransactionalCommand<MfaSetupResponse>;

public sealed record ConfirmMfaCommand(
    Guid UserId,
    Guid SessionId,
    string Code) : ITransactionalCommand<MfaConfirmationResponse>;

public sealed record DisableMfaCommand(
    Guid UserId,
    Guid SessionId,
    string CurrentPassword) : ITransactionalCommand<OperationCompletedResponse>;

public sealed record GetSessionsQuery(Guid UserId, Guid CurrentSessionId) : IQuery<SessionsResponse>;

public sealed record GetCurrentProfileQuery(Guid UserId, Guid SessionId) : IQuery<IdentitySnapshotResponse>;

internal sealed class RegisterAccountCommandHandler(IIdentityService identityService)
    : IRequestHandler<RegisterAccountCommand, Result<RegistrationAcceptedResponse>>
{
    public Task<Result<RegistrationAcceptedResponse>> Handle(
        RegisterAccountCommand request,
        CancellationToken cancellationToken) => identityService.RegisterAsync(request, cancellationToken);
}

internal sealed class SignInCommandHandler(IIdentityService identityService)
    : IRequestHandler<SignInCommand, Result<SignInResponse>>
{
    public Task<Result<SignInResponse>> Handle(SignInCommand request, CancellationToken cancellationToken) =>
        identityService.SignInAsync(request, cancellationToken);
}

internal sealed class CompleteMfaChallengeCommandHandler(IIdentityService identityService)
    : IRequestHandler<CompleteMfaChallengeCommand, Result<AuthenticatedSessionResponse>>
{
    public Task<Result<AuthenticatedSessionResponse>> Handle(
        CompleteMfaChallengeCommand request,
        CancellationToken cancellationToken) => identityService.CompleteMfaAsync(request, cancellationToken);
}

internal sealed class CompleteMfaRecoveryCommandHandler(IIdentityService identityService)
    : IRequestHandler<CompleteMfaRecoveryCommand, Result<AuthenticatedSessionResponse>>
{
    public Task<Result<AuthenticatedSessionResponse>> Handle(
        CompleteMfaRecoveryCommand request,
        CancellationToken cancellationToken) => identityService.CompleteMfaRecoveryAsync(request, cancellationToken);
}

internal sealed class RefreshSessionCommandHandler(IIdentityService identityService)
    : IRequestHandler<RefreshSessionCommand, Result<AuthenticatedSessionResponse>>
{
    public Task<Result<AuthenticatedSessionResponse>> Handle(
        RefreshSessionCommand request,
        CancellationToken cancellationToken) => identityService.RefreshAsync(request, cancellationToken);
}

internal sealed class SignOutCommandHandler(IIdentityService identityService)
    : IRequestHandler<SignOutCommand, Result<OperationCompletedResponse>>
{
    public Task<Result<OperationCompletedResponse>> Handle(
        SignOutCommand request,
        CancellationToken cancellationToken) => identityService.SignOutAsync(request, cancellationToken);
}

internal sealed class SignOutAllSessionsCommandHandler(IIdentityService identityService)
    : IRequestHandler<SignOutAllSessionsCommand, Result<OperationCompletedResponse>>
{
    public Task<Result<OperationCompletedResponse>> Handle(
        SignOutAllSessionsCommand request,
        CancellationToken cancellationToken) => identityService.SignOutAllAsync(request, cancellationToken);
}

internal sealed class RevokeSessionCommandHandler(IIdentityService identityService)
    : IRequestHandler<RevokeSessionCommand, Result<OperationCompletedResponse>>
{
    public Task<Result<OperationCompletedResponse>> Handle(
        RevokeSessionCommand request,
        CancellationToken cancellationToken) => identityService.RevokeSessionAsync(request, cancellationToken);
}

internal sealed class SendEmailVerificationCommandHandler(IIdentityService identityService)
    : IRequestHandler<SendEmailVerificationCommand, Result<NeutralAcceptedResponse>>
{
    public Task<Result<NeutralAcceptedResponse>> Handle(
        SendEmailVerificationCommand request,
        CancellationToken cancellationToken) => identityService.SendEmailVerificationAsync(request, cancellationToken);
}

internal sealed class ConfirmEmailCommandHandler(IIdentityService identityService)
    : IRequestHandler<ConfirmEmailCommand, Result<OperationCompletedResponse>>
{
    public Task<Result<OperationCompletedResponse>> Handle(
        ConfirmEmailCommand request,
        CancellationToken cancellationToken) => identityService.ConfirmEmailAsync(request, cancellationToken);
}

internal sealed class ForgotPasswordCommandHandler(IIdentityService identityService)
    : IRequestHandler<ForgotPasswordCommand, Result<NeutralAcceptedResponse>>
{
    public Task<Result<NeutralAcceptedResponse>> Handle(
        ForgotPasswordCommand request,
        CancellationToken cancellationToken) => identityService.ForgotPasswordAsync(request, cancellationToken);
}

internal sealed class ResetPasswordCommandHandler(IIdentityService identityService)
    : IRequestHandler<ResetPasswordCommand, Result<OperationCompletedResponse>>
{
    public Task<Result<OperationCompletedResponse>> Handle(
        ResetPasswordCommand request,
        CancellationToken cancellationToken) => identityService.ResetPasswordAsync(request, cancellationToken);
}

internal sealed class ChangePasswordCommandHandler(IIdentityService identityService)
    : IRequestHandler<ChangePasswordCommand, Result<OperationCompletedResponse>>
{
    public Task<Result<OperationCompletedResponse>> Handle(
        ChangePasswordCommand request,
        CancellationToken cancellationToken) => identityService.ChangePasswordAsync(request, cancellationToken);
}

internal sealed class SetupMfaCommandHandler(IIdentityService identityService)
    : IRequestHandler<SetupMfaCommand, Result<MfaSetupResponse>>
{
    public Task<Result<MfaSetupResponse>> Handle(
        SetupMfaCommand request,
        CancellationToken cancellationToken) => identityService.SetupMfaAsync(request, cancellationToken);
}

internal sealed class ConfirmMfaCommandHandler(IIdentityService identityService)
    : IRequestHandler<ConfirmMfaCommand, Result<MfaConfirmationResponse>>
{
    public Task<Result<MfaConfirmationResponse>> Handle(
        ConfirmMfaCommand request,
        CancellationToken cancellationToken) => identityService.ConfirmMfaAsync(request, cancellationToken);
}

internal sealed class DisableMfaCommandHandler(IIdentityService identityService)
    : IRequestHandler<DisableMfaCommand, Result<OperationCompletedResponse>>
{
    public Task<Result<OperationCompletedResponse>> Handle(
        DisableMfaCommand request,
        CancellationToken cancellationToken) => identityService.DisableMfaAsync(request, cancellationToken);
}

internal sealed class GetSessionsQueryHandler(IIdentityService identityService)
    : IRequestHandler<GetSessionsQuery, Result<SessionsResponse>>
{
    public Task<Result<SessionsResponse>> Handle(GetSessionsQuery request, CancellationToken cancellationToken) =>
        identityService.GetSessionsAsync(request, cancellationToken);
}

internal sealed class GetCurrentProfileQueryHandler(IIdentityService identityService)
    : IRequestHandler<GetCurrentProfileQuery, Result<IdentitySnapshotResponse>>
{
    public Task<Result<IdentitySnapshotResponse>> Handle(
        GetCurrentProfileQuery request,
        CancellationToken cancellationToken) => identityService.GetProfileAsync(request, cancellationToken);
}
