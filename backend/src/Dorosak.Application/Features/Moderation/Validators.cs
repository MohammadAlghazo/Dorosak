using Dorosak.Domain.Engagement;
using FluentValidation;

namespace Dorosak.Application.Features.Moderation;

internal sealed class CreateContentReportCommandValidator : AbstractValidator<CreateContentReportCommand>
{
    public CreateContentReportCommandValidator()
    {
        RuleFor(request => request.UserId).NotEmpty();
        RuleFor(request => request).Must(request =>
            new[] { request.CourseId, request.ReviewId, request.CommentId, request.ReportedUserId }
                .Count(target => target is not null) == 1)
            .WithMessage("Exactly one report target is required.");
        RuleFor(request => request.Reason).NotEmpty().Must(ModerationContractValues.IsDefined<ContentReportReason>);
        RuleFor(request => request.Details).MaximumLength(2000);
        RuleFor(request => request.Details)
            .Must(value => value?.Trim().Length >= 10)
            .When(request => string.Equals(request.Reason, nameof(ContentReportReason.Other), StringComparison.OrdinalIgnoreCase));
        RuleFor(request => request.ContextCommentId)
            .NotNull()
            .NotEqual(Guid.Empty)
            .When(request => request.ReportedUserId is not null);
        RuleFor(request => request.ContextCommentId)
            .Null()
            .When(request => request.ReportedUserId is null);
        RuleFor(request => request.IdempotencyKey).NotEmpty().MaximumLength(200);
    }
}

internal sealed class GetMyContentReportQueryValidator : AbstractValidator<GetMyContentReportQuery>
{
    public GetMyContentReportQueryValidator()
    {
        RuleFor(request => request.UserId).NotEmpty();
        RuleFor(request => request.ReportId).NotEmpty();
    }
}

internal sealed class GetAdminContentReportsQueryValidator : AbstractValidator<GetAdminContentReportsQuery>
{
    public GetAdminContentReportsQueryValidator()
    {
        RuleFor(request => request.ActorUserId).NotEmpty();
        RuleFor(request => request.Status)
            .Must(value => value is null || ModerationContractValues.IsDefined<ContentReportStatus>(value));
        RuleFor(request => request.TargetKind)
            .Must(value => value is null || value is "Course" or "Review" or "Comment" or "ReportedUser");
        RuleFor(request => request.Limit).InclusiveBetween(1, 100);
        RuleFor(request => request.Cursor).MaximumLength(1000);
    }
}

internal sealed class GetModerationCasesQueryValidator : AbstractValidator<GetModerationCasesQuery>
{
    public GetModerationCasesQueryValidator()
    {
        RuleFor(request => request.ActorUserId).NotEmpty();
        RuleFor(request => request.Status)
            .Must(value => value is null || ModerationContractValues.IsDefined<ModerationCaseStatus>(value));
        RuleFor(request => request.Limit).InclusiveBetween(1, 100);
        RuleFor(request => request.Cursor).MaximumLength(1000);
    }
}

internal sealed class GetModerationCaseQueryValidator : AbstractValidator<GetModerationCaseQuery>
{
    public GetModerationCaseQueryValidator()
    {
        RuleFor(request => request.ActorUserId).NotEmpty();
        RuleFor(request => request.CaseId).NotEmpty();
    }
}

internal sealed class ApplyModerationActionCommandValidator : AbstractValidator<ApplyModerationActionCommand>
{
    public ApplyModerationActionCommandValidator()
    {
        RuleFor(request => request.ActorUserId).NotEmpty();
        RuleFor(request => request.CaseId).NotEmpty();
        RuleFor(request => request.Action).NotEmpty()
            .Must(ModerationContractValues.IsDefined<ModerationActionType>);
        RuleFor(request => request.Reason).NotEmpty().Must(ModerationContractValues.HasValidReasonLength);
        RuleFor(request => request.ExpectedVersion).GreaterThan(0);
        RuleFor(request => request.AuditReason).NotEmpty().Must(ModerationContractValues.HasValidReasonLength);
        RuleFor(request => request.IdempotencyKey).NotEmpty().MaximumLength(200);
    }
}

internal static class ModerationContractValues
{
    public static bool IsDefined<TEnum>(string value)
        where TEnum : struct, Enum => Enum.GetNames<TEnum>().Contains(value.Trim(), StringComparer.OrdinalIgnoreCase);

    public static bool HasValidReasonLength(string value) => value.Trim().Length is >= 8 and <= 1000;
}
