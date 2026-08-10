using Dorosak.Application.Common.Idempotency;
using Dorosak.Application.Common.Results;

namespace Dorosak.Application.Features.Engagement;

internal sealed class CreateDiscussionThreadReplayHandler(IEngagementService service)
    : IIdempotencyReplayHandler<CreateDiscussionThreadCommand, Result<DiscussionThreadResponse>>
{
    public Task<Result<DiscussionThreadResponse>> ResolveAsync(
        CreateDiscussionThreadCommand request,
        Result<DiscussionThreadResponse> storedResponse,
        CancellationToken cancellationToken) =>
        storedResponse.IsSuccess
            ? service.GetDiscussionThreadAsync(
                new GetDiscussionThreadQuery(
                    request.UserId,
                    request.Scope,
                    storedResponse.Value.Id,
                    50,
                    null),
                cancellationToken)
            : Task.FromResult(storedResponse);
}

internal sealed class CreateDiscussionCommentReplayHandler(IEngagementService service)
    : IIdempotencyReplayHandler<CreateDiscussionCommentCommand, Result<DiscussionCommentResponse>>
{
    public Task<Result<DiscussionCommentResponse>> ResolveAsync(
        CreateDiscussionCommentCommand request,
        Result<DiscussionCommentResponse> storedResponse,
        CancellationToken cancellationToken) =>
        storedResponse.IsSuccess
            ? service.GetDiscussionCommentForReplayAsync(
                request.UserId,
                request.Scope,
                request.ThreadId,
                storedResponse.Value.Id,
                cancellationToken)
            : Task.FromResult(storedResponse);
}
