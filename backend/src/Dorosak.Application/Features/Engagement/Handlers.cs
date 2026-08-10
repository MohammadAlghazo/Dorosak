using Dorosak.Application.Common.Results;
using MediatR;

namespace Dorosak.Application.Features.Engagement;

internal sealed class EngagementHandler<TRequest, TResponse>(IEngagementService service)
    : IRequestHandler<TRequest, Result<TResponse>>
    where TRequest : class, IRequest<Result<TResponse>>
    where TResponse : notnull
{
    public Task<Result<TResponse>> Handle(TRequest request, CancellationToken cancellationToken) => request switch
    {
        GetCourseReviewsQuery query => Cast(service.GetCourseReviewsAsync(query, cancellationToken)),
        GetMyCourseReviewQuery query => Cast(service.GetMyCourseReviewAsync(query, cancellationToken)),
        CreateCourseReviewCommand command => Cast(service.CreateCourseReviewAsync(command, cancellationToken)),
        UpdateCourseReviewCommand command => Cast(service.UpdateCourseReviewAsync(command, cancellationToken)),
        DeleteCourseReviewCommand command => Cast(service.DeleteCourseReviewAsync(command, cancellationToken)),
        GetDiscussionThreadsQuery query => Cast(service.GetDiscussionThreadsAsync(query, cancellationToken)),
        GetDiscussionThreadQuery query => Cast(service.GetDiscussionThreadAsync(query, cancellationToken)),
        CreateDiscussionThreadCommand command => Cast(service.CreateDiscussionThreadAsync(command, cancellationToken)),
        UpdateDiscussionThreadCommand command => Cast(service.UpdateDiscussionThreadAsync(command, cancellationToken)),
        DeleteDiscussionThreadCommand command => Cast(service.DeleteDiscussionThreadAsync(command, cancellationToken)),
        CreateDiscussionCommentCommand command => Cast(service.CreateDiscussionCommentAsync(command, cancellationToken)),
        UpdateDiscussionCommentCommand command => Cast(service.UpdateDiscussionCommentAsync(command, cancellationToken)),
        DeleteDiscussionCommentCommand command => Cast(service.DeleteDiscussionCommentAsync(command, cancellationToken)),
        LikeDiscussionCommentCommand command => Cast(service.LikeDiscussionCommentAsync(command, cancellationToken)),
        UnlikeDiscussionCommentCommand command => Cast(service.UnlikeDiscussionCommentAsync(command, cancellationToken)),
        _ => throw new InvalidOperationException($"Unsupported engagement request {typeof(TRequest).Name}."),
    };

    private static async Task<Result<TResponse>> Cast<TValue>(Task<Result<TValue>> task)
    {
        Result<TValue> result = await task;
        return result.IsSuccess
            ? Result.Success((TResponse)(object)result.Value!)
            : Result.Failure<TResponse>(result.Failure);
    }
}
