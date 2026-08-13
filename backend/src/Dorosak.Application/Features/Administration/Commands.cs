using Dorosak.Application.Common.Messaging;

namespace Dorosak.Application.Features.Administration;

public sealed record GetAdminCmsQuery : IQuery<AdminCmsResponse>;

public sealed record GetPublicCmsPageQuery(string Slug, string Locale) : IQuery<PublicCmsPageResponse>;

public sealed record GetPublicFaqsQuery(string Locale) : IQuery<IReadOnlyList<PublicCmsFaqResponse>>;

public sealed record GetAdminSettingsQuery : IQuery<PortfolioSettingsResponse>;

public sealed record GetPublicSettingsQuery(string Locale) : IQuery<PublicPortfolioSettingsResponse>;

public sealed record UpsertCmsPageDraftCommand(
    Guid ActorUserId,
    string Slug,
    int ExpectedVersion,
    string TitleAr,
    string TitleEn,
    string BodyAr,
    string BodyEn,
    string AuditReason) : ITransactionalCommand<CmsPageResponse>;

public sealed record PublishCmsPageCommand(
    Guid ActorUserId,
    string Slug,
    int ExpectedVersion,
    string AuditReason) : ITransactionalCommand<CmsPageResponse>;

public sealed record UpsertCmsFaqDraftCommand(
    Guid ActorUserId,
    Guid? FaqId,
    int ExpectedVersion,
    int DisplayOrder,
    string QuestionAr,
    string QuestionEn,
    string AnswerAr,
    string AnswerEn,
    string AuditReason) : ITransactionalCommand<CmsFaqResponse>;

public sealed record PublishCmsFaqCommand(
    Guid ActorUserId,
    Guid FaqId,
    int ExpectedVersion,
    string AuditReason) : ITransactionalCommand<CmsFaqResponse>;

public sealed record UpdatePortfolioSettingsCommand(
    Guid ActorUserId,
    int FeaturedCourseLimit,
    bool ShowPortfolioNotice,
    string NoticeAr,
    string NoticeEn,
    long ExpectedVersion,
    string AuditReason) : ITransactionalCommand<PortfolioSettingsResponse>;

public sealed record GetAuditLogsQuery(
    Guid ActorUserId,
    string? Action,
    int Limit,
    string? Cursor,
    string AuditReason) : IQuery<AuditLogPageResponse>;
