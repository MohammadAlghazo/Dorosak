using Dorosak.Application.Common.Results;
using Dorosak.Application.Features.Identity;

namespace Dorosak.Application.Common.Identity;

public interface IIdentityService
{
    Task<Result<RegistrationAcceptedResponse>> RegisterAsync(
        RegisterAccountCommand request,
        CancellationToken cancellationToken);

    Task<Result<SignInResponse>> SignInAsync(
        SignInCommand request,
        CancellationToken cancellationToken);

    Task<Result<AuthenticatedSessionResponse>> CompleteMfaAsync(
        CompleteMfaChallengeCommand request,
        CancellationToken cancellationToken);

    Task<Result<AuthenticatedSessionResponse>> CompleteMfaRecoveryAsync(
        CompleteMfaRecoveryCommand request,
        CancellationToken cancellationToken);

    Task<Result<AuthenticatedSessionResponse>> RefreshAsync(
        RefreshSessionCommand request,
        CancellationToken cancellationToken);

    Task<Result<OperationCompletedResponse>> SignOutAsync(
        SignOutCommand request,
        CancellationToken cancellationToken);

    Task<Result<OperationCompletedResponse>> SignOutAllAsync(
        SignOutAllSessionsCommand request,
        CancellationToken cancellationToken);

    Task<Result<SessionsResponse>> GetSessionsAsync(
        GetSessionsQuery request,
        CancellationToken cancellationToken);

    Task<Result<OperationCompletedResponse>> RevokeSessionAsync(
        RevokeSessionCommand request,
        CancellationToken cancellationToken);

    Task<Result<NeutralAcceptedResponse>> SendEmailVerificationAsync(
        SendEmailVerificationCommand request,
        CancellationToken cancellationToken);

    Task<Result<OperationCompletedResponse>> ConfirmEmailAsync(
        ConfirmEmailCommand request,
        CancellationToken cancellationToken);

    Task<Result<NeutralAcceptedResponse>> RequestEmailChangeAsync(
        RequestEmailChangeCommand request,
        CancellationToken cancellationToken);

    Task<Result<OperationCompletedResponse>> ConfirmEmailChangeAsync(
        ConfirmEmailChangeCommand request,
        CancellationToken cancellationToken);

    Task<Result<NeutralAcceptedResponse>> ForgotPasswordAsync(
        ForgotPasswordCommand request,
        CancellationToken cancellationToken);

    Task<Result<OperationCompletedResponse>> ResetPasswordAsync(
        ResetPasswordCommand request,
        CancellationToken cancellationToken);

    Task<Result<OperationCompletedResponse>> ChangePasswordAsync(
        ChangePasswordCommand request,
        CancellationToken cancellationToken);

    Task<Result<MfaSetupResponse>> SetupMfaAsync(
        SetupMfaCommand request,
        CancellationToken cancellationToken);

    Task<Result<MfaConfirmationResponse>> ConfirmMfaAsync(
        ConfirmMfaCommand request,
        CancellationToken cancellationToken);

    Task<Result<OperationCompletedResponse>> DisableMfaAsync(
        DisableMfaCommand request,
        CancellationToken cancellationToken);

    Task<Result<IdentitySnapshotResponse>> GetProfileAsync(
        GetCurrentProfileQuery request,
        CancellationToken cancellationToken);
}
