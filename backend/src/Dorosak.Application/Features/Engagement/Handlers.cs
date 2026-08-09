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
