namespace Dorosak.Application.Features.Administration;

public sealed record CmsPageRevisionResponse(
    int Version,
    string TitleAr,
    string TitleEn,
    string BodyAr,
    string BodyEn,
    Guid CreatedByUserId,
    DateTimeOffset CreatedAt);

public sealed record CmsPageResponse(
    Guid Id,
    string Slug,
    int CurrentVersion,
    int? PublishedVersion,
    CmsPageRevisionResponse? Draft,
    CmsPageRevisionResponse? Published,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? PublishedAt);

public sealed record PublicCmsPageResponse(
    string Slug,
    string Locale,
    string Title,
    string Body,
    int Version,
    DateTimeOffset PublishedAt);

public sealed record CmsFaqRevisionResponse(
    int Version,
    string QuestionAr,
    string QuestionEn,
    string AnswerAr,
    string AnswerEn,
    Guid CreatedByUserId,
    DateTimeOffset CreatedAt);

public sealed record CmsFaqResponse(
    Guid Id,
    int DisplayOrder,
    int CurrentVersion,
    int? PublishedVersion,
    CmsFaqRevisionResponse? Draft,
    CmsFaqRevisionResponse? Published,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? PublishedAt);

public sealed record PublicCmsFaqResponse(
    Guid Id,
    string Locale,
    string Question,
    string Answer,
    int Version,
    int DisplayOrder,
    DateTimeOffset PublishedAt);

public sealed record PortfolioSettingsResponse(
    int FeaturedCourseLimit,
    bool ShowPortfolioNotice,
    string NoticeAr,
    string NoticeEn,
    long Version,
    DateTimeOffset UpdatedAt);

public sealed record PublicPortfolioSettingsResponse(
    string Locale,
    int FeaturedCourseLimit,
    bool ShowPortfolioNotice,
    string PortfolioNotice);

public sealed record AdminCmsResponse(
    IReadOnlyList<CmsPageResponse> Pages,
    IReadOnlyList<CmsFaqResponse> Faqs);

public sealed record AuditLogResponse(
    Guid Id,
    Guid ActorUserId,
    string Action,
    string TargetType,
    Guid TargetId,
    string Result,
    string? Reason,
    DateTimeOffset OccurredAt);

public sealed record AuditLogPageResponse(
    IReadOnlyList<AuditLogResponse> Items,
    string? NextCursor,
    bool HasMore);
