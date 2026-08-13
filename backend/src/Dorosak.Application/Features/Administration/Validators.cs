using FluentValidation;

namespace Dorosak.Application.Features.Administration;

internal sealed class GetPublicCmsPageQueryValidator : AbstractValidator<GetPublicCmsPageQuery>
{
    public GetPublicCmsPageQueryValidator()
    {
        RuleFor(request => request.Slug).NotEmpty().MaximumLength(40);
        RuleFor(request => request.Locale).Must(locale => locale is "ar" or "en");
    }
}

internal sealed class GetPublicFaqsQueryValidator : AbstractValidator<GetPublicFaqsQuery>
{
    public GetPublicFaqsQueryValidator() => RuleFor(request => request.Locale).Must(locale => locale is "ar" or "en");
}

internal sealed class GetPublicSettingsQueryValidator : AbstractValidator<GetPublicSettingsQuery>
{
    public GetPublicSettingsQueryValidator() => RuleFor(request => request.Locale).Must(locale => locale is "ar" or "en");
}

internal sealed class UpsertCmsPageDraftCommandValidator : AbstractValidator<UpsertCmsPageDraftCommand>
{
    public UpsertCmsPageDraftCommandValidator()
    {
        RuleFor(request => request.ActorUserId).NotEmpty();
        RuleFor(request => request.Slug).NotEmpty().MaximumLength(40);
        RuleFor(request => request.ExpectedVersion).GreaterThanOrEqualTo(0);
        RuleFor(request => request.TitleAr).NotEmpty().MaximumLength(200);
        RuleFor(request => request.TitleEn).NotEmpty().MaximumLength(200);
        RuleFor(request => request.BodyAr).NotEmpty().MaximumLength(20000);
        RuleFor(request => request.BodyEn).NotEmpty().MaximumLength(20000);
        RuleFor(request => request.AuditReason).NotEmpty().MinimumLength(8).MaximumLength(1000);
    }
}

internal sealed class PublishCmsPageCommandValidator : AbstractValidator<PublishCmsPageCommand>
{
    public PublishCmsPageCommandValidator()
    {
        RuleFor(request => request.ActorUserId).NotEmpty();
        RuleFor(request => request.Slug).NotEmpty().MaximumLength(40);
        RuleFor(request => request.ExpectedVersion).GreaterThan(0);
        RuleFor(request => request.AuditReason).NotEmpty().MinimumLength(8).MaximumLength(1000);
    }
}

internal sealed class UpsertCmsFaqDraftCommandValidator : AbstractValidator<UpsertCmsFaqDraftCommand>
{
    public UpsertCmsFaqDraftCommandValidator()
    {
        RuleFor(request => request.ActorUserId).NotEmpty();
        RuleFor(request => request.ExpectedVersion).GreaterThanOrEqualTo(0);
        RuleFor(request => request.DisplayOrder).InclusiveBetween(0, 10000);
        RuleFor(request => request.QuestionAr).NotEmpty().MaximumLength(300);
        RuleFor(request => request.QuestionEn).NotEmpty().MaximumLength(300);
        RuleFor(request => request.AnswerAr).NotEmpty().MaximumLength(5000);
        RuleFor(request => request.AnswerEn).NotEmpty().MaximumLength(5000);
        RuleFor(request => request.AuditReason).NotEmpty().MinimumLength(8).MaximumLength(1000);
    }
}

internal sealed class PublishCmsFaqCommandValidator : AbstractValidator<PublishCmsFaqCommand>
{
    public PublishCmsFaqCommandValidator()
    {
        RuleFor(request => request.ActorUserId).NotEmpty();
        RuleFor(request => request.FaqId).NotEmpty();
        RuleFor(request => request.ExpectedVersion).GreaterThan(0);
        RuleFor(request => request.AuditReason).NotEmpty().MinimumLength(8).MaximumLength(1000);
    }
}

internal sealed class UpdatePortfolioSettingsCommandValidator : AbstractValidator<UpdatePortfolioSettingsCommand>
{
    public UpdatePortfolioSettingsCommandValidator()
    {
        RuleFor(request => request.ActorUserId).NotEmpty();
        RuleFor(request => request.FeaturedCourseLimit).InclusiveBetween(1, 12);
        RuleFor(request => request.NoticeAr).NotNull().MaximumLength(240);
        RuleFor(request => request.NoticeEn).NotNull().MaximumLength(240);
        RuleFor(request => request.ExpectedVersion).GreaterThan(0);
        RuleFor(request => request.AuditReason).NotEmpty().MinimumLength(8).MaximumLength(1000);
    }
}

internal sealed class GetAuditLogsQueryValidator : AbstractValidator<GetAuditLogsQuery>
{
    public GetAuditLogsQueryValidator()
    {
        RuleFor(request => request.ActorUserId).NotEmpty();
        RuleFor(request => request.Action).MaximumLength(200);
        RuleFor(request => request.Limit).InclusiveBetween(1, 100);
        RuleFor(request => request.AuditReason).NotEmpty().MinimumLength(8).MaximumLength(1000);
    }
}
