using Dorosak.Application.Common.Authorization;

namespace Dorosak.Application.Features.Communications;

internal sealed class ConversationResourceAuthorizer<TRequest>(IConversationAccessReader accessReader)
    : IRequestAuthorizer<TRequest>
    where TRequest : IConversationAuthorizedRequest
{
    public async ValueTask<AuthorizationDecision> AuthorizeAsync(
        TRequest request,
        CancellationToken cancellationToken)
    {
        bool allowed = await accessReader.CanAccessAsync(
            request.UserId,
            request.ConversationId,
            cancellationToken);
        return allowed
            ? AuthorizationDecision.Allow()
            : AuthorizationDecision.Deny(
                "CONVERSATION.ACCESS_DENIED",
                "The conversation was not found or is not available to this account.");
    }
}

internal sealed class AnnouncementResourceAuthorizer<TRequest>(IAnnouncementAccessReader accessReader)
    : IRequestAuthorizer<TRequest>
    where TRequest : IAnnouncementAuthorizedRequest
{
    public async ValueTask<AuthorizationDecision> AuthorizeAsync(
        TRequest request,
        CancellationToken cancellationToken)
    {
        bool allowed = await accessReader.CanManageCourseAsync(request.UserId, request.CourseId, cancellationToken);
        return allowed
            ? AuthorizationDecision.Allow()
            : AuthorizationDecision.Deny(
                "ANNOUNCEMENT.ACCESS_DENIED",
                "The course was not found or is not available to this account.");
    }
}
