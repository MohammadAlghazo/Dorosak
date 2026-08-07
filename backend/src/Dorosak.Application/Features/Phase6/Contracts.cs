namespace Dorosak.Application.Features.Phase6;

public sealed record PagedResponse<T>(IReadOnlyList<T> Items, string? NextCursor, bool HasMore);

public sealed record TeacherApplicationResponse(
    Guid Id,
    Guid UserId,
    string Headline,
    string Biography,
    string Expertise,
    string Motivation,
    string Status,
    string? ReviewerReason,
    DateTimeOffset SubmittedAt,
    DateTimeOffset UpdatedAt);

public sealed record TeacherProfileResponse(
    Guid UserId,
    string Headline,
    string Biography,
    string Expertise,
    DateTimeOffset ApprovedAt);

public sealed record CourseSummaryResponse(
    Guid Id,
    string DefaultLocale,
    string Status,
    long DraftVersion,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string? Title,
    string? Slug);

public sealed record CourseDetailsResponse(
    Guid Id,
    Guid OwnerUserId,
    string DefaultLocale,
    string Status,
    long DraftVersion,
    IReadOnlyList<CourseLocalizationResponse> Localizations,
    IReadOnlyList<CourseCollaboratorResponse> Collaborators,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record CourseLocalizationResponse(
    string Locale,
    string Title,
    string Subtitle,
    string Description,
    string Slug);

public sealed record CourseCollaboratorResponse(Guid UserId, string Role, DateTimeOffset AddedAt);

public sealed record CourseMutationResponse(Guid CourseId, string Status, long DraftVersion);

public sealed record CurriculumResponse(
    long DraftVersion,
    IReadOnlyList<SectionResponse> Sections);

public sealed record SectionResponse(
    Guid Id,
    int Position,
    string Title,
    IReadOnlyList<LessonResponse> Lessons);

public sealed record LessonResponse(
    Guid Id,
    int Position,
    string Title,
    string LessonType,
    string Content);

public sealed record PublicationStatusResponse(
    Guid CourseId,
    string CourseStatus,
    Guid? ReviewId,
    string? ReviewStatus,
    string? ReviewerReason,
    long DraftVersion);

public sealed record PublicationReviewResponse(
    Guid Id,
    Guid CourseId,
    Guid DraftId,
    long DraftVersion,
    Guid RequestedByUserId,
    string Status,
    string? ReviewerReason,
    DateTimeOffset RequestedAt,
    DateTimeOffset UpdatedAt);

public sealed record CategoryResponse(
    Guid Id,
    string Code,
    Guid? ParentId,
    int DisplayOrder,
    IReadOnlyList<TaxonomyLocalizationResponse> Localizations);

public sealed record TagResponse(
    Guid Id,
    string Code,
    bool IsActive,
    IReadOnlyList<TaxonomyLocalizationResponse> Localizations);

public sealed record TaxonomyLocalizationResponse(string Locale, string Name);

public sealed record CatalogCourseResponse(
    Guid Id,
    string Locale,
    string Slug,
    string Title,
    string Subtitle,
    string Description);

public sealed record HighlightSegment(string Text, bool Matched);

public sealed record SearchCourseResponse(
    Guid Id,
    string Locale,
    string Slug,
    string Title,
    IReadOnlyList<HighlightSegment> Highlights);

public sealed record CatalogFilterContract(
    string? CategoryCode = null,
    string? Language = null,
    string? Level = null);
