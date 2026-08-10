using Dorosak.Application.Common.Authorization;

namespace Dorosak.Application.Features.Engagement;

internal sealed class DiscussionResourceAuthorizer<TRequest>(IDiscussionAccessReader accessReader)
    : IRequestAuthorizer<TRequest>
    where TRequest : IDiscussionAuthorizedRequest
{
    public async ValueTask<AuthorizationDecision> AuthorizeAsync(
        TRequest request,
        CancellationToken cancellationToken)
    {
        bool allowed = await accessReader.CanAccessAsync(
            request.UserId,
            request.Scope,
            cancellationToken);
        return allowed
            ? AuthorizationDecision.Allow()
            : AuthorizationDecision.Deny(
                "DISCUSSION.ACCESS_DENIED",
                "The discussion scope was not found or is not available to this account.");
    }
}
