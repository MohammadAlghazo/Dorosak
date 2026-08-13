using Dorosak.Application.Common.Results;

namespace Dorosak.Application.Features.Authoring;

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

public sealed record OperationCompleted(bool Completed = true);

public interface IAuthoringService
{
    Task<Result<CourseMutationResponse>> CreateCourseAsync(CreateCourseCommand request, CancellationToken cancellationToken);
    Task<Result<CourseMutationResponse>> UpdateCourseMetadataAsync(UpdateCourseMetadataCommand request, CancellationToken cancellationToken);
    Task<Result<CourseMutationResponse>> ArchiveCourseAsync(ArchiveCourseCommand request, CancellationToken cancellationToken);
    Task<Result<CourseMutationResponse>> StartNewDraftAsync(StartNewDraftCommand request, CancellationToken cancellationToken);
    Task<Result<CourseCollaboratorResponse>> AddCollaboratorAsync(AddCollaboratorCommand request, CancellationToken cancellationToken);
    Task<Result<OperationCompleted>> RemoveCollaboratorAsync(RemoveCollaboratorCommand request, CancellationToken cancellationToken);
    Task<Result<CourseMutationResponse>> TransferCourseOwnershipAsync(TransferCourseOwnershipCommand request, CancellationToken cancellationToken);
    Task<Result<CourseDetailsResponse>> GetCourseAsync(GetCourseQuery request, CancellationToken cancellationToken);
    Task<Result<PagedResponse<CourseSummaryResponse>>> GetInstructorCoursesAsync(GetInstructorCoursesQuery request, CancellationToken cancellationToken);
}



