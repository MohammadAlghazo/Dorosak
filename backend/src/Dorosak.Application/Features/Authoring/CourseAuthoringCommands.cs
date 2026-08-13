using Dorosak.Application.Common.Messaging;
using Dorosak.Application.Common.Results;
using MediatR;

namespace Dorosak.Application.Features.Authoring;

public interface ICourseAuthorizedRequest
{
    Guid UserId { get; }
    Guid CourseId { get; }
    CourseAccess Access { get; }
}

public enum CourseAccess
{
    View,
    Edit,
    Owner
}

public sealed record CourseLocalizationInput(
    string Locale,
    string Title,
    string Subtitle,
    string Description,
    string? Slug = null);

public sealed record CreateCourseCommand(
    Guid UserId,
    string DefaultLocale,
    string Level,
    IReadOnlyList<CourseLocalizationInput> Localizations,
    IReadOnlyList<string> CategoryCodes,
    IReadOnlyList<string> TagCodes) : ITransactionalCommand<CourseMutationResponse>;

public sealed record GetInstructorCoursesQuery(Guid UserId, int Limit, string? Cursor)
    : IQuery<PagedResponse<CourseSummaryResponse>>;

public sealed record GetCourseQuery(Guid UserId, Guid CourseId)
    : IQuery<CourseDetailsResponse>, ICourseAuthorizedRequest
{
    Guid ICourseAuthorizedRequest.UserId => UserId;
    Guid ICourseAuthorizedRequest.CourseId => CourseId;
    CourseAccess ICourseAuthorizedRequest.Access => CourseAccess.View;
}

public sealed record UpdateCourseMetadataCommand(
    Guid UserId,
    Guid CourseId,
    long? ExpectedVersion,
    string DefaultLocale,
    string Level,
    IReadOnlyList<CourseLocalizationInput> Localizations,
    IReadOnlyList<string> CategoryCodes,
    IReadOnlyList<string> TagCodes) : ITransactionalCommand<CourseMutationResponse>, ICourseAuthorizedRequest
{
    Guid ICourseAuthorizedRequest.UserId => UserId;
    Guid ICourseAuthorizedRequest.CourseId => CourseId;
    CourseAccess ICourseAuthorizedRequest.Access => CourseAccess.Edit;
}

public sealed record ArchiveCourseCommand(
    Guid UserId,
    Guid CourseId,
    string Reason) : ITransactionalCommand<CourseMutationResponse>, ICourseAuthorizedRequest
{
    Guid ICourseAuthorizedRequest.UserId => UserId;
    Guid ICourseAuthorizedRequest.CourseId => CourseId;
    CourseAccess ICourseAuthorizedRequest.Access => CourseAccess.Owner;
}

public sealed record StartNewDraftCommand(Guid UserId, Guid CourseId)
    : ITransactionalCommand<CourseMutationResponse>, ICourseAuthorizedRequest
{
    Guid ICourseAuthorizedRequest.UserId => UserId;
    Guid ICourseAuthorizedRequest.CourseId => CourseId;
    CourseAccess ICourseAuthorizedRequest.Access => CourseAccess.Owner;
}

public sealed record AddCollaboratorCommand(
    Guid UserId,
    Guid CourseId,
    Guid CollaboratorUserId,
    string Role) : ITransactionalCommand<CourseCollaboratorResponse>, ICourseAuthorizedRequest
{
    Guid ICourseAuthorizedRequest.UserId => UserId;
    Guid ICourseAuthorizedRequest.CourseId => CourseId;
    CourseAccess ICourseAuthorizedRequest.Access => CourseAccess.Owner;
}

public sealed record RemoveCollaboratorCommand(
    Guid UserId,
    Guid CourseId,
    Guid CollaboratorUserId) : ITransactionalCommand<OperationCompleted>, ICourseAuthorizedRequest
{
    Guid ICourseAuthorizedRequest.UserId => UserId;
    Guid ICourseAuthorizedRequest.CourseId => CourseId;
    CourseAccess ICourseAuthorizedRequest.Access => CourseAccess.Owner;
}

public sealed record TransferCourseOwnershipCommand(
    Guid UserId,
    Guid CourseId,
    Guid NewOwnerUserId,
    long? ExpectedVersion) : ITransactionalCommand<CourseMutationResponse>, ICourseAuthorizedRequest
{
    Guid ICourseAuthorizedRequest.UserId => UserId;
    Guid ICourseAuthorizedRequest.CourseId => CourseId;
    CourseAccess ICourseAuthorizedRequest.Access => CourseAccess.Owner;
}

internal sealed class CourseAuthoringCommandHandler(IAuthoringService service)
    : IRequestHandler<CreateCourseCommand, Result<CourseMutationResponse>>,
      IRequestHandler<UpdateCourseMetadataCommand, Result<CourseMutationResponse>>,
      IRequestHandler<ArchiveCourseCommand, Result<CourseMutationResponse>>,
      IRequestHandler<StartNewDraftCommand, Result<CourseMutationResponse>>,
      IRequestHandler<AddCollaboratorCommand, Result<CourseCollaboratorResponse>>,
      IRequestHandler<RemoveCollaboratorCommand, Result<OperationCompleted>>,
      IRequestHandler<TransferCourseOwnershipCommand, Result<CourseMutationResponse>>
{
    public Task<Result<CourseMutationResponse>> Handle(CreateCourseCommand request, CancellationToken cancellationToken) => service.CreateCourseAsync(request, cancellationToken);
    public Task<Result<CourseMutationResponse>> Handle(UpdateCourseMetadataCommand request, CancellationToken cancellationToken) => service.UpdateCourseMetadataAsync(request, cancellationToken);
    public Task<Result<CourseMutationResponse>> Handle(ArchiveCourseCommand request, CancellationToken cancellationToken) => service.ArchiveCourseAsync(request, cancellationToken);
    public Task<Result<CourseMutationResponse>> Handle(StartNewDraftCommand request, CancellationToken cancellationToken) => service.StartNewDraftAsync(request, cancellationToken);
    public Task<Result<CourseCollaboratorResponse>> Handle(AddCollaboratorCommand request, CancellationToken cancellationToken) => service.AddCollaboratorAsync(request, cancellationToken);
    public Task<Result<OperationCompleted>> Handle(RemoveCollaboratorCommand request, CancellationToken cancellationToken) => service.RemoveCollaboratorAsync(request, cancellationToken);
    public Task<Result<CourseMutationResponse>> Handle(TransferCourseOwnershipCommand request, CancellationToken cancellationToken) => service.TransferCourseOwnershipAsync(request, cancellationToken);
}

internal sealed class CourseAuthoringQueryHandler(IAuthoringService service)
    : IRequestHandler<GetCourseQuery, Result<CourseDetailsResponse>>,
      IRequestHandler<GetInstructorCoursesQuery, Result<PagedResponse<CourseSummaryResponse>>>
{
    public Task<Result<CourseDetailsResponse>> Handle(GetCourseQuery request, CancellationToken cancellationToken) => service.GetCourseAsync(request, cancellationToken);
    public Task<Result<PagedResponse<CourseSummaryResponse>>> Handle(GetInstructorCoursesQuery request, CancellationToken cancellationToken) => service.GetInstructorCoursesAsync(request, cancellationToken);
}

