using Dorosak.Application.Common.Results;

namespace Dorosak.Application.Features.Phase6;

public interface IPhase6Service
{
    Task<Result<TeacherApplicationResponse>> SubmitTeacherApplicationAsync(
        SubmitTeacherApplicationCommand request,
        CancellationToken cancellationToken);

    Task<Result<TeacherApplicationResponse>> GetTeacherApplicationAsync(
        GetTeacherApplicationQuery request,
        CancellationToken cancellationToken);

    Task<Result<TeacherApplicationResponse>> WithdrawTeacherApplicationAsync(
        WithdrawTeacherApplicationCommand request,
        CancellationToken cancellationToken);

    Task<Result<PagedResponse<TeacherApplicationResponse>>> GetTeacherApplicationsAsync(
        GetTeacherApplicationsQuery request,
        CancellationToken cancellationToken);

    Task<Result<TeacherApplicationResponse>> ReviewTeacherApplicationAsync(
        ReviewTeacherApplicationCommand request,
        CancellationToken cancellationToken);

    Task<Result<CourseMutationResponse>> CreateCourseAsync(
        CreateCourseCommand request,
        CancellationToken cancellationToken);

    Task<Result<PagedResponse<CourseSummaryResponse>>> GetInstructorCoursesAsync(
        GetInstructorCoursesQuery request,
        CancellationToken cancellationToken);

    Task<Result<CourseDetailsResponse>> GetCourseAsync(
        GetCourseQuery request,
        CancellationToken cancellationToken);

    Task<Result<CourseMutationResponse>> UpdateCourseMetadataAsync(
        UpdateCourseMetadataCommand request,
        CancellationToken cancellationToken);

    Task<Result<CourseMutationResponse>> ArchiveCourseAsync(
        ArchiveCourseCommand request,
        CancellationToken cancellationToken);

    Task<Result<CurriculumResponse>> GetCurriculumAsync(
        GetCurriculumQuery request,
        CancellationToken cancellationToken);

    Task<Result<CourseMutationResponse>> UpdateCurriculumAsync(
        UpdateCurriculumCommand request,
        CancellationToken cancellationToken);

    Task<Result<CourseCollaboratorResponse>> AddCollaboratorAsync(
        AddCollaboratorCommand request,
        CancellationToken cancellationToken);

    Task<Result<OperationCompleted>> RemoveCollaboratorAsync(
        RemoveCollaboratorCommand request,
        CancellationToken cancellationToken);

    Task<Result<PublicationStatusResponse>> RequestPublicationAsync(
        RequestPublicationCommand request,
        CancellationToken cancellationToken);

    Task<Result<PublicationStatusResponse>> GetPublicationStatusAsync(
        GetPublicationStatusQuery request,
        CancellationToken cancellationToken);

    Task<Result<PagedResponse<PublicationReviewResponse>>> GetPublicationReviewsAsync(
        GetPublicationReviewsQuery request,
        CancellationToken cancellationToken);

    Task<Result<PublicationReviewResponse>> ReviewPublicationAsync(
        ReviewPublicationCommand request,
        CancellationToken cancellationToken);

    Task<Result<PagedResponse<CategoryResponse>>> GetCategoriesAsync(
        GetCategoriesQuery request,
        CancellationToken cancellationToken);

    Task<Result<PagedResponse<TagResponse>>> GetTagsAsync(
        GetTagsQuery request,
        CancellationToken cancellationToken);

    Task<Result<CategoryResponse>> UpsertCategoryAsync(
        UpsertCategoryCommand request,
        CancellationToken cancellationToken);

    Task<Result<TagResponse>> UpsertTagAsync(
        UpsertTagCommand request,
        CancellationToken cancellationToken);

    Task<Result<PagedResponse<CatalogCourseResponse>>> GetCatalogAsync(
        GetCatalogCoursesQuery request,
        CancellationToken cancellationToken);

    Task<Result<CatalogCourseResponse>> GetPublicCourseAsync(
        GetPublicCourseQuery request,
        CancellationToken cancellationToken);

    Task<Result<PagedResponse<SearchCourseResponse>>> SearchAsync(
        SearchCoursesQuery request,
        CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<string>>> SuggestionsAsync(
        SuggestCourseSuggestionsQuery request,
        CancellationToken cancellationToken);
}

public sealed record OperationCompleted(bool Completed);

public interface ICourseAccessReader
{
    Task<bool> CanAccessAsync(Guid courseId, Guid userId, CourseAccess access, CancellationToken cancellationToken);
}

public enum CourseAccess
{
    View,
    Edit,
    Owner,
}

public interface IPhase6AuthorizedRequest : Common.Authorization.IAuthorizedRequest
{
    Guid UserId { get; }

    Guid CourseId { get; }

    CourseAccess Access { get; }
}

public static class SearchTextNormalizer
{
    public const string ArabicVersion = "ar-v1";

    public static string Normalize(string value, string locale)
    {
        string normalized = value.Normalize(global::System.Text.NormalizationForm.FormC).Trim();
        if (!string.Equals(locale, "ar", StringComparison.OrdinalIgnoreCase))
        {
            return normalized.ToLowerInvariant();
        }

        var builder = new global::System.Text.StringBuilder(normalized.Length);
        foreach (char character in normalized)
        {
            if (character is '\u064B' or '\u064C' or '\u064D' or '\u064E' or '\u064F' or '\u0650' or
                '\u0651' or '\u0652' or '\u0653' or '\u0654' or '\u0655' or '\u0670' or '\u0640')
            {
                continue;
            }

            builder.Append(character switch
            {
                '\u0622' or '\u0623' or '\u0625' => '\u0627',
                '\u0649' => '\u064A',
                _ => character,
            });
        }

        return builder.ToString();
    }
}
