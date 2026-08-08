using FluentValidation;

namespace Dorosak.Application.Features.Publishing;

internal sealed class PublishCourseCommandValidator : AbstractValidator<PublishCourseCommand>
{
    public PublishCourseCommandValidator()
    {
        RuleFor(request => request.ActorUserId).NotEmpty();
        RuleFor(request => request.CourseId).NotEmpty();
        RuleFor(request => request.IdempotencyKey).NotEmpty().MaximumLength(200);
        RuleFor(request => request.AuditReason).NotEmpty().MinimumLength(8).MaximumLength(1000);
    }
}

internal sealed class UnpublishCourseCommandValidator : AbstractValidator<UnpublishCourseCommand>
{
    public UnpublishCourseCommandValidator()
    {
        RuleFor(request => request.ActorUserId).NotEmpty();
        RuleFor(request => request.CourseId).NotEmpty();
        RuleFor(request => request.IdempotencyKey).NotEmpty().MaximumLength(200);
        RuleFor(request => request.AuditReason).NotEmpty().MinimumLength(8).MaximumLength(1000);
    }
}

internal sealed class ActivateCourseReleaseCommandValidator : AbstractValidator<ActivateCourseReleaseCommand>
{
    public ActivateCourseReleaseCommandValidator()
    {
        RuleFor(request => request.ActorUserId).NotEmpty();
        RuleFor(request => request.CourseId).NotEmpty();
        RuleFor(request => request.AuditReason).NotEmpty().MinimumLength(8).MaximumLength(1000);
        RuleFor(request => request.Manifest).NotNull();
        RuleFor(request => request.Manifest.CourseId)
            .Equal(request => request.CourseId)
            .When(request => request.Manifest is not null);
    }
}

internal sealed class ResolvePublicCourseQueryValidator : AbstractValidator<ResolvePublicCourseQuery>
{
    public ResolvePublicCourseQueryValidator()
    {
        RuleFor(request => request.Locale).Must(locale => locale is "ar" or "en");
        RuleFor(request => request.Slug).NotEmpty().MaximumLength(160).Matches("^[a-z0-9]+(?:-[a-z0-9]+)*$");
    }
}
