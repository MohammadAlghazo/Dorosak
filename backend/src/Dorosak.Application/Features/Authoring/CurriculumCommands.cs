using Dorosak.Application.Common.Messaging;
using Dorosak.Application.Common.Results;
using MediatR;

namespace Dorosak.Application.Features.Authoring;

public sealed record LessonInput(
    Guid? Id,
    int Position,
    string Title,
    string LessonType,
    string Content,
    Guid? MediaAssetId = null,
    Guid? QuizVersionId = null,
    Guid? AssignmentVersionId = null);

public sealed record SectionInput(
    Guid? Id,
    int Position,
    string Title,
    IReadOnlyList<LessonInput> Lessons);

public sealed record GetCurriculumQuery(Guid UserId, Guid CourseId)
    : IQuery<CurriculumResponse>, ICourseAuthorizedRequest
{
    Guid ICourseAuthorizedRequest.UserId => UserId;
    Guid ICourseAuthorizedRequest.CourseId => CourseId;
    CourseAccess ICourseAuthorizedRequest.Access => CourseAccess.View;
}

public sealed record UpdateCurriculumCommand(
    Guid UserId,
    Guid CourseId,
    long? ExpectedVersion,
    IReadOnlyList<SectionInput> Sections) : ITransactionalCommand<CourseMutationResponse>, ICourseAuthorizedRequest
{
    Guid ICourseAuthorizedRequest.UserId => UserId;
    Guid ICourseAuthorizedRequest.CourseId => CourseId;
    CourseAccess ICourseAuthorizedRequest.Access => CourseAccess.Edit;
}

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

public interface ICurriculumService
{
    Task<Result<CourseMutationResponse>> UpdateCurriculumAsync(UpdateCurriculumCommand request, CancellationToken cancellationToken);
    Task<Result<CurriculumResponse>> GetCurriculumAsync(GetCurriculumQuery request, CancellationToken cancellationToken);
}

internal sealed class CurriculumCommandHandler(ICurriculumService service)
    : IRequestHandler<UpdateCurriculumCommand, Result<CourseMutationResponse>>
{
    public Task<Result<CourseMutationResponse>> Handle(UpdateCurriculumCommand request, CancellationToken cancellationToken) => service.UpdateCurriculumAsync(request, cancellationToken);
}

internal sealed class CurriculumQueryHandler(ICurriculumService service)
    : IRequestHandler<GetCurriculumQuery, Result<CurriculumResponse>>
{
    public Task<Result<CurriculumResponse>> Handle(GetCurriculumQuery request, CancellationToken cancellationToken) => service.GetCurriculumAsync(request, cancellationToken);
}

