using FluentValidation;

namespace Dorosak.Application.Features.Profiles.TeacherApplications;

internal static class ValidationHelpers
{
    public static bool IsPlainText(string value) => !value.Contains('<') && !value.Contains('>');
}

internal sealed class SubmitTeacherApplicationCommandValidator : AbstractValidator<SubmitTeacherApplicationCommand>
{
    public SubmitTeacherApplicationCommandValidator()
    {
        RuleFor(request => request.Headline).NotEmpty().MinimumLength(2).MaximumLength(160).Must(ValidationHelpers.IsPlainText);
        RuleFor(request => request.Biography).NotEmpty().MaximumLength(4000).Must(ValidationHelpers.IsPlainText);
        RuleFor(request => request.Expertise).NotEmpty().MaximumLength(1000).Must(ValidationHelpers.IsPlainText);
        RuleFor(request => request.Motivation).NotEmpty().MaximumLength(4000).Must(ValidationHelpers.IsPlainText);
    }
}

internal sealed class ReviewTeacherApplicationCommandValidator : AbstractValidator<ReviewTeacherApplicationCommand>
{
    public ReviewTeacherApplicationCommandValidator()
    {
        RuleFor(request => request.ApplicationId).NotEmpty();
        RuleFor(request => request.Decision).Must(value => value is "start" or "approve" or "reject");
        RuleFor(request => request.Reason)
            .MaximumLength(2000)
            .Must((request, reason) => request.Decision != "reject" || !string.IsNullOrWhiteSpace(reason))
            .WithMessage("A rejection reason is required.");
    }
}

internal sealed class GetTeacherApplicationsQueryValidator : AbstractValidator<GetTeacherApplicationsQuery>
{
    public GetTeacherApplicationsQueryValidator() => RuleFor(request => request.Limit).InclusiveBetween(1, 100);
}
