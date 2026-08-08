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
    string Level,
    IReadOnlyList<string> CategoryCodes,
    IReadOnlyList<string> TagCodes,
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
    string Content,
    Guid? MediaAssetId = null,
    Guid? QuizVersionId = null,
    Guid? AssignmentVersionId = null);

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
    bool IsActive,
    IReadOnlyList<TaxonomyLocalizationResponse> Localizations);

public sealed record TagResponse(
    Guid Id,
    string Code,
    bool IsActive,
    IReadOnlyList<TaxonomyLocalizationResponse> Localizations);

public sealed record TaxonomyLocalizationResponse(string Locale, string Name);

public sealed record CatalogCourseResponse(
    Guid CourseId,
    Guid ReleaseId,
    string Slug,
    string Title,
    string Summary,
    string Language,
    string Level,
    int DurationMinutes,
    IReadOnlyList<PublicInstructorResponse> Instructors,
    IReadOnlyList<PublicTaxonomyTermResponse> Categories,
    IReadOnlyList<PublicTaxonomyTermResponse> Tags,
    PublicCoursePriceResponse? Price);

public sealed record PublicInstructorResponse(Guid Id, string DisplayName);

public sealed record PublicTaxonomyTermResponse(Guid Id, string Code, string Name);

public sealed record PublicCoursePriceResponse(string Type, string? Amount, string? Currency);

public sealed record PublicCourseLocalizationResponse(string Locale, string Slug);

public sealed record PublicCourseDetailResponse(
    Guid CourseId,
    Guid ReleaseId,
    string Slug,
    string Title,
    string Summary,
    string Language,
    string Level,
    int DurationMinutes,
    IReadOnlyList<PublicInstructorResponse> Instructors,
    IReadOnlyList<PublicTaxonomyTermResponse> Categories,
    IReadOnlyList<PublicTaxonomyTermResponse> Tags,
    PublicCoursePriceResponse? Price,
    string Locale,
    string DefaultLocale,
    string Description,
    IReadOnlyList<string> LearningOutcomes,
    IReadOnlyList<PublicCourseLocalizationResponse> Localizations);

public sealed record HighlightSegment(string Text, bool Matched);

public sealed record SearchCourseResponse(
    Guid CourseId,
    Guid ReleaseId,
    string Slug,
    string Title,
    string Summary,
    string Language,
    string Level,
    int DurationMinutes,
    IReadOnlyList<PublicInstructorResponse> Instructors,
    IReadOnlyList<PublicTaxonomyTermResponse> Categories,
    IReadOnlyList<PublicTaxonomyTermResponse> Tags,
    PublicCoursePriceResponse? Price,
    IReadOnlyList<HighlightSegment> TitleHighlight,
    IReadOnlyList<HighlightSegment> SummaryHighlight);

public sealed record SearchPageResponse(
    IReadOnlyList<SearchCourseResponse> Items,
    string? NextCursor,
    bool HasMore,
    string? Correction);

public sealed record PublicSearchSuggestionResponse(
    string? Slug,
    IReadOnlyList<HighlightSegment> Segments);

public sealed record CatalogFilterContract(
    string? CategoryCode = null,
    string? Tag = null,
    string? Language = null,
    string? Level = null,
    string? Price = null,
    string? Duration = null,
    string? Instructor = null);
