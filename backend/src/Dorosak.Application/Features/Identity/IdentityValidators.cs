using FluentValidation;

namespace Dorosak.Application.Features.Identity;

internal sealed class RegisterAccountCommandValidator : AbstractValidator<RegisterAccountCommand>
{
    public RegisterAccountCommandValidator()
    {
        RuleFor(request => request.DisplayName).NotEmpty().MinimumLength(2).MaximumLength(100);
        RuleFor(request => request.Email).NotEmpty().EmailAddress().MaximumLength(320);
        RuleFor(request => request.Password).NotEmpty().MinimumLength(12).MaximumLength(64);
    }
}

internal sealed class SignInCommandValidator : AbstractValidator<SignInCommand>
{
    public SignInCommandValidator()
    {
        RuleFor(request => request.Email).NotEmpty().EmailAddress().MaximumLength(320);
        RuleFor(request => request.Password).NotEmpty().MaximumLength(256);
    }
}

internal sealed class CompleteMfaChallengeCommandValidator : AbstractValidator<CompleteMfaChallengeCommand>
{
    public CompleteMfaChallengeCommandValidator()
    {
        RuleFor(request => request.ChallengeToken).NotEmpty().MaximumLength(500);
        RuleFor(request => request.Code).NotEmpty().Matches("^[0-9]{6}$");
    }
}

internal sealed class CompleteMfaRecoveryCommandValidator : AbstractValidator<CompleteMfaRecoveryCommand>
{
    public CompleteMfaRecoveryCommandValidator()
    {
        RuleFor(request => request.ChallengeToken).NotEmpty().MaximumLength(500);
        RuleFor(request => request.RecoveryCode).NotEmpty().MaximumLength(100);
    }
}

internal sealed class RefreshSessionCommandValidator : AbstractValidator<RefreshSessionCommand>
{
    public RefreshSessionCommandValidator() =>
        RuleFor(request => request.RefreshToken).NotEmpty().MaximumLength(500);
}

internal sealed class SendEmailVerificationCommandValidator : AbstractValidator<SendEmailVerificationCommand>
{
    public SendEmailVerificationCommandValidator() =>
        RuleFor(request => request.Email).NotEmpty().EmailAddress().MaximumLength(320);
}

internal sealed class ConfirmEmailCommandValidator : AbstractValidator<ConfirmEmailCommand>
{
    public ConfirmEmailCommandValidator() => RuleFor(request => request.Token).NotEmpty().MaximumLength(4000);
}

internal sealed class RequestEmailChangeCommandValidator : AbstractValidator<RequestEmailChangeCommand>
{
    public RequestEmailChangeCommandValidator()
    {
        RuleFor(request => request.CurrentPassword).NotEmpty().MaximumLength(256);
        RuleFor(request => request.NewEmail).NotEmpty().EmailAddress().MaximumLength(320);
    }
}

internal sealed class ConfirmEmailChangeCommandValidator : AbstractValidator<ConfirmEmailChangeCommand>
{
    public ConfirmEmailChangeCommandValidator() =>
        RuleFor(request => request.Token).NotEmpty().MaximumLength(4000);
}

internal sealed class ForgotPasswordCommandValidator : AbstractValidator<ForgotPasswordCommand>
{
    public ForgotPasswordCommandValidator() =>
        RuleFor(request => request.Email).NotEmpty().EmailAddress().MaximumLength(320);
}

internal sealed class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator()
    {
        RuleFor(request => request.Token).NotEmpty().MaximumLength(4000);
        RuleFor(request => request.NewPassword).NotEmpty().MinimumLength(12).MaximumLength(64);
    }
}

internal sealed class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        RuleFor(request => request.CurrentPassword).NotEmpty().MaximumLength(256);
        RuleFor(request => request.NewPassword).NotEmpty().MinimumLength(12).MaximumLength(64);
    }
}

internal sealed class ConfirmMfaCommandValidator : AbstractValidator<ConfirmMfaCommand>
{
    public ConfirmMfaCommandValidator() => RuleFor(request => request.Code).NotEmpty().Matches("^[0-9]{6}$");
}

internal sealed class DisableMfaCommandValidator : AbstractValidator<DisableMfaCommand>
{
    public DisableMfaCommandValidator() => RuleFor(request => request.CurrentPassword).NotEmpty().MaximumLength(256);
}
