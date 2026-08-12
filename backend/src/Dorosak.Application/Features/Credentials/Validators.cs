using FluentValidation;

namespace Dorosak.Application.Features.Credentials;

internal sealed class GetMyCertificatesQueryValidator : AbstractValidator<GetMyCertificatesQuery>
{
    public GetMyCertificatesQueryValidator() => RuleFor(request => request.UserId).NotEmpty();
}

internal sealed class GetMyCertificateQueryValidator : AbstractValidator<GetMyCertificateQuery>
{
    public GetMyCertificateQueryValidator()
    {
        RuleFor(request => request.UserId).NotEmpty();
        RuleFor(request => request.CertificateId).NotEmpty();
    }
}

internal sealed class VerifyCertificateQueryValidator : AbstractValidator<VerifyCertificateQuery>
{
    public VerifyCertificateQueryValidator() =>
        RuleFor(request => request.VerificationCode).NotEmpty().MaximumLength(64)
            .Matches("^[A-Za-z0-9_-]+$");
}

internal sealed class IssueCertificateFromCompletionCommandValidator
    : AbstractValidator<IssueCertificateFromCompletionCommand>
{
    public IssueCertificateFromCompletionCommandValidator() =>
        RuleFor(request => request.CompletionEnrollmentId).NotEmpty();
}

internal sealed class RevokeCertificateCommandValidator : AbstractValidator<RevokeCertificateCommand>
{
    public RevokeCertificateCommandValidator()
    {
        RuleFor(request => request.ActorUserId).NotEmpty();
        RuleFor(request => request.CertificateId).NotEmpty();
        RuleFor(request => request.Reason).NotEmpty().MinimumLength(8).MaximumLength(1000);
    }
}
