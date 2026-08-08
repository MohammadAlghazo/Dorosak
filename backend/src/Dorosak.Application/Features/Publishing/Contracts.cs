using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dorosak.Application.Features.Phase6;

namespace Dorosak.Application.Features.Publishing;

public sealed record PublishingFailure(string Code, string Description);

public sealed record ReleaseLocalizationSnapshot(
    string Locale,
    string Slug,
    string Title,
    string Subtitle,
    string Description);

public sealed record ReleaseLessonSnapshot(
    Guid SourceLessonId,
    Guid SourceRevisionId,
    int Position,
    string Title,
    string LessonType,
    string Content,
    Guid? MediaAssetId,
    Guid? QuizVersionId,
    Guid? AssignmentVersionId,
    decimal CompletionRequirement);

public sealed record ReleaseSectionSnapshot(
    Guid SourceSectionId,
    Guid SourceRevisionId,
    int Position,
    string Title,
    IReadOnlyList<ReleaseLessonSnapshot> Lessons);

public sealed record ReleaseAssessmentSnapshot(
    Guid SourceLessonId,
    ReleaseAssessmentKind Type,
    Guid VersionId,
    int Position);

public enum ReleaseAssessmentKind
{
    Quiz,
    Assignment,
}

public sealed record ReleaseMediaVariantSnapshot(
    Guid SourceLessonId,
    Guid AssetId,
    Guid VariantId,
    string Kind,
    string ContentType,
    long Bytes,
    int? Width,
    int? Height,
    decimal? DurationSeconds);

public sealed record ReleaseCaptionSnapshot(
    Guid SourceLessonId,
    Guid AssetId,
    Guid CaptionId,
    string Locale,
    string Label);

public sealed record ReleaseInstructorSnapshot(Guid UserId, string DisplayName, int Position);

public sealed record ReleaseTaxonomySnapshot(Guid TermId, string Code, string Name, bool IsCategory);

public sealed record AuthoringPublicationSnapshot(
    Guid CourseId,
    Guid DraftId,
    long DraftVersion,
    string DefaultLocale,
    string Level,
    IReadOnlyList<ReleaseLocalizationSnapshot> Localizations,
    IReadOnlyList<ReleaseSectionSnapshot> Sections,
    IReadOnlyList<ReleaseInstructorSnapshot> Instructors,
    IReadOnlyList<ReleaseTaxonomySnapshot> Taxonomy,
    PublishingFailure? Failure)
{
    public bool Ready => Failure is null;

    public IReadOnlyList<MediaAssetReference> MediaReferences => Sections
        .SelectMany(section => section.Lessons)
        .Where(lesson => lesson.MediaAssetId.HasValue)
        .Select(lesson => new MediaAssetReference(lesson.SourceLessonId, lesson.MediaAssetId!.Value))
        .ToArray();

    public IReadOnlyList<Guid> QuizVersionIds => Sections
        .SelectMany(section => section.Lessons)
        .Where(lesson => lesson.QuizVersionId.HasValue)
        .Select(lesson => lesson.QuizVersionId!.Value)
        .Distinct()
        .ToArray();

    public IReadOnlyList<Guid> AssignmentVersionIds => Sections
        .SelectMany(section => section.Lessons)
        .Where(lesson => lesson.AssignmentVersionId.HasValue)
        .Select(lesson => lesson.AssignmentVersionId!.Value)
        .Distinct()
        .ToArray();
}

public sealed record MediaAssetReference(Guid SourceLessonId, Guid AssetId);

public sealed record MediaPublicationSnapshot(
    IReadOnlyList<ReleaseMediaVariantSnapshot> Variants,
    IReadOnlyList<ReleaseCaptionSnapshot> Captions,
    PublishingFailure? Failure)
{
    public bool Ready => Failure is null;
}

public sealed record AssessmentPublicationSnapshot(
    PublishingFailure? Failure)
{
    public bool Ready => Failure is null;
}

public sealed record PublicationManifest(
    Guid CourseId,
    Guid SourceDraftId,
    long SourceDraftVersion,
    string DefaultLocale,
    string Level,
    IReadOnlyList<ReleaseLocalizationSnapshot> Localizations,
    IReadOnlyList<ReleaseSectionSnapshot> Sections,
    IReadOnlyList<ReleaseAssessmentSnapshot> Assessments,
    IReadOnlyList<ReleaseMediaVariantSnapshot> MediaVariants,
    IReadOnlyList<ReleaseCaptionSnapshot> Captions,
    IReadOnlyList<ReleaseInstructorSnapshot> Instructors,
    IReadOnlyList<ReleaseTaxonomySnapshot> Taxonomy,
    int DurationMinutes)
{
    public string ManifestHash => CanonicalManifestHasher.Compute(this);
}

public static class CanonicalManifestHasher
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
    };

    public static string Compute(PublicationManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        var canonical = new
        {
            manifest.CourseId,
            manifest.SourceDraftId,
            manifest.SourceDraftVersion,
            manifest.DefaultLocale,
            manifest.Level,
            Localizations = manifest.Localizations
                .OrderBy(item => item.Locale, StringComparer.Ordinal)
                .ThenBy(item => item.Slug, StringComparer.Ordinal),
            Sections = manifest.Sections
                .OrderBy(item => item.Position)
                .ThenBy(item => item.SourceSectionId)
                .Select(section => new
                {
                    section.SourceSectionId,
                    section.SourceRevisionId,
                    section.Position,
                    section.Title,
                    Lessons = section.Lessons
                        .OrderBy(item => item.Position)
                        .ThenBy(item => item.SourceLessonId),
                }),
            Assessments = manifest.Assessments
                .OrderBy(item => item.SourceLessonId)
                .ThenBy(item => item.Type)
                .ThenBy(item => item.VersionId),
            MediaVariants = manifest.MediaVariants
                .OrderBy(item => item.SourceLessonId)
                .ThenBy(item => item.VariantId),
            Captions = manifest.Captions
                .OrderBy(item => item.SourceLessonId)
                .ThenBy(item => item.Locale, StringComparer.Ordinal)
                .ThenBy(item => item.CaptionId),
            Instructors = manifest.Instructors.OrderBy(item => item.Position).ThenBy(item => item.UserId),
            Taxonomy = manifest.Taxonomy
                .OrderBy(item => item.IsCategory ? 0 : 1)
                .ThenBy(item => item.Code, StringComparer.Ordinal)
                .ThenBy(item => item.TermId),
            manifest.DurationMinutes,
        };

        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(canonical, Options);
        return Convert.ToHexStringLower(SHA256.HashData(bytes));
    }
}

public sealed record CourseReleaseResponse(
    Guid CourseId,
    Guid ReleaseId,
    int ReleaseNumber,
    string ManifestHash,
    string State,
    DateTimeOffset PublishedAt,
    long ProjectionGeneration);

public sealed record PublicCourseLookupResponse(
    PublicCourseDetailResponse? Course,
    string? RedirectSlug);
