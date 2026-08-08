using Dorosak.Application.Common.Results;
using MediatR;

namespace Dorosak.Application.Features.Learning;

internal sealed class LearningHandler<TRequest, TResponse>(ILearningService service)
    : IRequestHandler<TRequest, Result<TResponse>>
    where TRequest : class, IRequest<Result<TResponse>>
    where TResponse : notnull
{
    public Task<Result<TResponse>> Handle(TRequest request, CancellationToken cancellationToken) => request switch
    {
        EnrollCourseCommand command => Cast(service.EnrollAsync(command, cancellationToken)),
        GetEnrollmentsQuery query => Cast(service.GetEnrollmentsAsync(query, cancellationToken)),
        GetLearningManifestQuery query => Cast(service.GetManifestAsync(query, cancellationToken)),
        GetLearningLessonQuery query => Cast(service.GetLessonAsync(query, cancellationToken)),
        UpdateLessonProgressCommand command => Cast(service.UpdateProgressAsync(command, cancellationToken)),
        GetLearningNotesQuery query => Cast(service.GetNotesAsync(query, cancellationToken)),
        UpsertLearningNoteCommand command => Cast(service.UpsertNoteAsync(command, cancellationToken)),
        DeleteLearningNoteCommand command => Cast(service.DeleteNoteAsync(command, cancellationToken)),
        AddBookmarkCommand command => Cast(service.AddBookmarkAsync(command, cancellationToken)),
        DeleteBookmarkCommand command => Cast(service.DeleteBookmarkAsync(command, cancellationToken)),
        MarkRecentlyViewedCommand command => Cast(service.MarkRecentlyViewedAsync(command, cancellationToken)),
        CreateQuizVersionCommand command => Cast(service.CreateQuizVersionAsync(command, cancellationToken)),
        MarkQuizVersionReadyCommand command => Cast(service.MarkQuizVersionReadyAsync(command, cancellationToken)),
        StartQuizAttemptCommand command => Cast(service.StartQuizAttemptAsync(command, cancellationToken)),
        GetQuizAttemptQuery query => Cast(service.GetQuizAttemptAsync(query, cancellationToken)),
        SubmitQuizAttemptCommand command => Cast(service.SubmitQuizAttemptAsync(command, cancellationToken)),
        GradeQuizAttemptCommand command => Cast(service.GradeQuizAttemptAsync(command, cancellationToken)),
        CreateAssignmentVersionCommand command => Cast(service.CreateAssignmentVersionAsync(command, cancellationToken)),
        MarkAssignmentVersionReadyCommand command => Cast(service.MarkAssignmentVersionReadyAsync(command, cancellationToken)),
        SubmitAssignmentCommand command => Cast(service.SubmitAssignmentAsync(command, cancellationToken)),
        GradeAssignmentCommand command => Cast(service.GradeAssignmentAsync(command, cancellationToken)),
        _ => throw new InvalidOperationException($"Unsupported learning request {typeof(TRequest).Name}."),
    };

    private static async Task<Result<TResponse>> Cast<TValue>(Task<Result<TValue>> task)
    {
        Result<TValue> result = await task;
        return result.IsSuccess
            ? Result.Success((TResponse)(object)result.Value!)
            : Result.Failure<TResponse>(result.Failure);
    }
}
