using Dorosak.Domain.Common;

namespace Dorosak.Domain.Catalog;

public enum CourseReleaseState
{
    Draft,
    Active,
    Superseded,
    Unpublished,
}

// Manifest fields are write-once. The lifecycle state is the only mutable release
// field and is constrained to the transitions used by activation and unpublish.
public sealed class CourseRelease
{
    private CourseRelease()
    {
    }

    private CourseRelease(
        Guid id,
        Guid courseId,
        Guid sourceDraftId,
        long sourceDraftVersion,
        int releaseNumber,
        string defaultLocale,
        string manifestHash,
        Guid publishedByUserId,
        DateTimeOffset publishedAt)
    {
        Id = id;
        CourseId = courseId;
        SourceDraftId = sourceDraftId;
        SourceDraftVersion = sourceDraftVersion;
        ReleaseNumber = releaseNumber;
        DefaultLocale = defaultLocale;
        ManifestHash = manifestHash;
        PublishedByUserId = publishedByUserId;
        PublishedAt = publishedAt;
        State = CourseReleaseState.Active;
    }

    public Guid Id { get; private set; }

    public Guid CourseId { get; private set; }

    public Guid SourceDraftId { get; private set; }

    public long SourceDraftVersion { get; private set; }

    public int ReleaseNumber { get; private set; }

    public string DefaultLocale { get; private set; } = string.Empty;

    public string ManifestHash { get; private set; } = string.Empty;

    public Guid PublishedByUserId { get; private set; }

    public DateTimeOffset PublishedAt { get; private set; }

    // The stored state is write-once. Superseded and unpublished are effective
    // states derived from the course active-release pointer at read time.
    public CourseReleaseState State { get; private set; }

    public static CourseRelease Create(
        Guid courseId,
        Guid sourceDraftId,
        long sourceDraftVersion,
        int releaseNumber,
        string defaultLocale,
        string manifestHash,
        Guid publishedByUserId,
        DateTimeOffset publishedAt) => new(
            Guid.CreateVersion7(),
            courseId,
            sourceDraftId,
            sourceDraftVersion,
            releaseNumber,
            NormalizeLocale(defaultLocale),
            NormalizeHash(manifestHash),
            publishedByUserId,
            publishedAt);

    public void Activate()
    {
        if (State is not (CourseReleaseState.Active or CourseReleaseState.Superseded or CourseReleaseState.Unpublished))
        {
            throw new DomainRuleException("RELEASE.INVALID_TRANSITION", "The release cannot become active from its current state.");
        }

        State = CourseReleaseState.Active;
    }

    public void Supersede()
    {
        if (State != CourseReleaseState.Active)
        {
            throw new DomainRuleException("RELEASE.INVALID_TRANSITION", "Only an active release can be superseded.");
        }

        State = CourseReleaseState.Superseded;
    }

    public void Unpublish()
    {
        if (State != CourseReleaseState.Active)
        {
            throw new DomainRuleException("RELEASE.INVALID_TRANSITION", "Only an active release can be unpublished.");
        }

        State = CourseReleaseState.Unpublished;
    }

    private static string NormalizeHash(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        string hash = value.Trim().ToLowerInvariant();
        if (hash.Length != 64 || hash.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new DomainRuleException("RELEASE.MANIFEST_HASH_INVALID", "A SHA-256 manifest hash is required.");
        }

        return hash;
    }

    private static string NormalizeLocale(string value) => value.Trim().ToLowerInvariant() switch
    {
        "ar" => "ar",
        "en" => "en",
        _ => throw new ArgumentOutOfRangeException(nameof(value), "Only ar and en are supported."),
    };
}

public sealed class CourseReleaseSection
{
    private CourseReleaseSection()
    {
    }

    private CourseReleaseSection(Guid id, Guid releaseId, Guid sourceSectionId, Guid sourceRevisionId, int position, string title)
    {
        Id = id;
        ReleaseId = releaseId;
        SourceSectionId = sourceSectionId;
        SourceRevisionId = sourceRevisionId;
        Position = position;
        Title = title;
    }

    public Guid Id { get; private set; }
    public Guid ReleaseId { get; private set; }
    public Guid SourceSectionId { get; private set; }
    public Guid SourceRevisionId { get; private set; }
    public int Position { get; private set; }
    public string Title { get; private set; } = string.Empty;

    public static CourseReleaseSection Create(Guid releaseId, Guid sourceSectionId, Guid sourceRevisionId, int position, string title) =>
        new(Guid.CreateVersion7(), releaseId, sourceSectionId, sourceRevisionId, position, title.Trim());
}

public sealed class CourseReleaseLesson
{
    private CourseReleaseLesson()
    {
    }

    private CourseReleaseLesson(
        Guid id,
        Guid releaseId,
        Guid sectionId,
        Guid sourceLessonId,
        Guid sourceRevisionId,
        int position,
        string title,
        string lessonType,
        string content,
        Guid? mediaAssetId,
        decimal completionRequirement)
    {
        Id = id;
        ReleaseId = releaseId;
        SectionId = sectionId;
        SourceLessonId = sourceLessonId;
        SourceRevisionId = sourceRevisionId;
        Position = position;
        Title = title;
        LessonType = lessonType;
        Content = content;
        MediaAssetId = mediaAssetId;
        CompletionRequirement = completionRequirement;
    }

    public Guid Id { get; private set; }
    public Guid ReleaseId { get; private set; }
    public Guid SectionId { get; private set; }
    public Guid SourceLessonId { get; private set; }
    public Guid SourceRevisionId { get; private set; }
    public int Position { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string LessonType { get; private set; } = string.Empty;
    public string Content { get; private set; } = string.Empty;
    public Guid? MediaAssetId { get; private set; }
    public decimal CompletionRequirement { get; private set; }

    public static CourseReleaseLesson Create(
        Guid releaseId,
        Guid sectionId,
        Guid sourceLessonId,
        Guid sourceRevisionId,
        int position,
        string title,
        string lessonType,
        string content,
        Guid? mediaAssetId,
        decimal completionRequirement) => new(
            Guid.CreateVersion7(),
            releaseId,
            sectionId,
            sourceLessonId,
            sourceRevisionId,
            position,
            title.Trim(),
            lessonType.Trim(),
            content.Trim(),
            mediaAssetId,
            completionRequirement);
}

public enum ReleaseAssessmentType
{
    Quiz,
    Assignment,
}

public sealed class CourseReleaseAssessment
{
    private CourseReleaseAssessment()
    {
    }

    private CourseReleaseAssessment(
        Guid id,
        Guid releaseId,
        Guid lessonId,
        ReleaseAssessmentType type,
        Guid versionId,
        int position)
    {
        Id = id;
        ReleaseId = releaseId;
        LessonId = lessonId;
        Type = type;
        QuizVersionId = type == ReleaseAssessmentType.Quiz ? versionId : null;
        AssignmentVersionId = type == ReleaseAssessmentType.Assignment ? versionId : null;
        Position = position;
    }

    public Guid Id { get; private set; }
    public Guid ReleaseId { get; private set; }
    public Guid LessonId { get; private set; }
    public ReleaseAssessmentType Type { get; private set; }
    public Guid? QuizVersionId { get; private set; }
    public Guid? AssignmentVersionId { get; private set; }
    public int Position { get; private set; }

    public Guid VersionId => QuizVersionId ?? AssignmentVersionId ?? Guid.Empty;

    public static CourseReleaseAssessment Create(Guid releaseId, Guid lessonId, ReleaseAssessmentType type, Guid versionId, int position) =>
        new(Guid.CreateVersion7(), releaseId, lessonId, type, versionId, position);
}

public sealed class CourseReleaseMediaVariant
{
    private CourseReleaseMediaVariant()
    {
    }

    private CourseReleaseMediaVariant(Guid id, Guid releaseId, Guid lessonId, Guid assetId, Guid variantId, string kind, string contentType, long bytes, int? width, int? height, decimal? durationSeconds)
    {
        Id = id;
        ReleaseId = releaseId;
        LessonId = lessonId;
        AssetId = assetId;
        VariantId = variantId;
        Kind = kind;
        ContentType = contentType;
        Bytes = bytes;
        Width = width;
        Height = height;
        DurationSeconds = durationSeconds;
    }

    public Guid Id { get; private set; }
    public Guid ReleaseId { get; private set; }
    public Guid LessonId { get; private set; }
    public Guid AssetId { get; private set; }
    public Guid VariantId { get; private set; }
    public string Kind { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long Bytes { get; private set; }
    public int? Width { get; private set; }
    public int? Height { get; private set; }
    public decimal? DurationSeconds { get; private set; }

    public static CourseReleaseMediaVariant Create(Guid releaseId, Guid lessonId, Guid assetId, Guid variantId, string kind, string contentType, long bytes, int? width, int? height, decimal? durationSeconds) =>
        new(Guid.CreateVersion7(), releaseId, lessonId, assetId, variantId, kind.Trim(), contentType.Trim(), bytes, width, height, durationSeconds);
}

public sealed class CourseReleaseCaption
{
    private CourseReleaseCaption()
    {
    }

    private CourseReleaseCaption(Guid id, Guid releaseId, Guid lessonId, Guid assetId, Guid captionId, string locale, string label)
    {
        Id = id;
        ReleaseId = releaseId;
        LessonId = lessonId;
        AssetId = assetId;
        CaptionId = captionId;
        Locale = locale;
        Label = label;
    }

    public Guid Id { get; private set; }
    public Guid ReleaseId { get; private set; }
    public Guid LessonId { get; private set; }
    public Guid AssetId { get; private set; }
    public Guid CaptionId { get; private set; }
    public string Locale { get; private set; } = string.Empty;
    public string Label { get; private set; } = string.Empty;

    public static CourseReleaseCaption Create(Guid releaseId, Guid lessonId, Guid assetId, Guid captionId, string locale, string label) =>
        new(Guid.CreateVersion7(), releaseId, lessonId, assetId, captionId, locale.Trim().ToLowerInvariant(), label.Trim());
}

public sealed class CourseReleaseLocalization
{
    private CourseReleaseLocalization()
    {
    }

    private CourseReleaseLocalization(Guid id, Guid releaseId, string locale, string slug, string title, string subtitle, string description)
    {
        Id = id;
        ReleaseId = releaseId;
        Locale = locale;
        Slug = slug;
        Title = title;
        Subtitle = subtitle;
        Description = description;
    }

    public Guid Id { get; private set; }
    public Guid ReleaseId { get; private set; }
    public string Locale { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string Subtitle { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;

    public static CourseReleaseLocalization Create(Guid releaseId, string locale, string slug, string title, string subtitle, string description) =>
        new(Guid.CreateVersion7(), releaseId, locale.Trim().ToLowerInvariant(), slug.Trim(), title.Trim(), subtitle.Trim(), description.Trim());
}

public sealed class CourseReleaseInstructor
{
    private CourseReleaseInstructor()
    {
    }

    private CourseReleaseInstructor(Guid releaseId, Guid userId, string displayName, int position)
    {
        ReleaseId = releaseId;
        UserId = userId;
        DisplayName = displayName;
        Position = position;
    }

    public Guid ReleaseId { get; private set; }
    public Guid UserId { get; private set; }
    public string DisplayName { get; private set; } = string.Empty;
    public int Position { get; private set; }

    public static CourseReleaseInstructor Create(Guid releaseId, Guid userId, string displayName, int position) =>
        new(releaseId, userId, displayName.Trim(), position);
}

public sealed class CourseReleaseTaxonomy
{
    private CourseReleaseTaxonomy()
    {
    }

    private CourseReleaseTaxonomy(Guid releaseId, Guid termId, string code, string name, bool category)
    {
        ReleaseId = releaseId;
        TermId = termId;
        Code = code;
        Name = name;
        IsCategory = category;
    }

    public Guid ReleaseId { get; private set; }
    public Guid TermId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public bool IsCategory { get; private set; }

    public static CourseReleaseTaxonomy Create(Guid releaseId, Guid termId, string code, string name, bool category) =>
        new(releaseId, termId, code.Trim(), name.Trim(), category);
}

public sealed class CatalogDocument
{
    private CatalogDocument()
    {
    }

    private CatalogDocument(Guid releaseId, Guid courseId, string locale, string slug, string title, string summary, string description, string language, string level, int durationMinutes, string searchText, string normalizedArabicText, DateTimeOffset publishedAt, long generation)
    {
        ReleaseId = releaseId;
        CourseId = courseId;
        Locale = locale;
        Slug = slug;
        Title = title;
        Summary = summary;
        Description = description;
        Language = language;
        Level = level;
        DurationMinutes = durationMinutes;
        SearchText = searchText;
        NormalizedArabicText = normalizedArabicText;
        PublishedAt = publishedAt;
        ProjectionGeneration = generation;
    }

    public Guid ReleaseId { get; private set; }
    public Guid CourseId { get; private set; }
    public string Locale { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string Summary { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string Language { get; private set; } = string.Empty;
    public string Level { get; private set; } = string.Empty;
    public int DurationMinutes { get; private set; }
    public string SearchText { get; private set; } = string.Empty;
    public string NormalizedArabicText { get; private set; } = string.Empty;
    public DateTimeOffset PublishedAt { get; private set; }
    public long ProjectionGeneration { get; private set; }

    public static CatalogDocument Create(Guid releaseId, Guid courseId, string locale, string slug, string title, string summary, string description, string language, string level, int durationMinutes, string searchText, string normalizedArabicText, DateTimeOffset publishedAt, long generation) =>
        new(releaseId, courseId, locale.Trim().ToLowerInvariant(), slug.Trim(), title.Trim(), summary.Trim(), description.Trim(), language.Trim(), level.Trim(), durationMinutes, searchText, normalizedArabicText, publishedAt, generation);
}

public sealed class CatalogProjectionState
{
    private CatalogProjectionState()
    {
    }

    private CatalogProjectionState(bool singleton)
    {
        Singleton = singleton;
    }

    public bool Singleton { get; private set; }

    public long Generation { get; private set; }

    public static CatalogProjectionState Create() => new(true);

    public long Advance() => ++Generation;
}
