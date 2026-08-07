using Dorosak.Application.Common.Authorization;

namespace Dorosak.Application.Features.Phase6;

internal sealed class Phase6ResourceAuthorizer<TRequest>(ICourseAccessReader accessReader)
    : IRequestAuthorizer<TRequest>
    where TRequest : IPhase6AuthorizedRequest
{
    public async ValueTask<AuthorizationDecision> AuthorizeAsync(
        TRequest request,
        CancellationToken cancellationToken)
    {
        bool allowed = await accessReader.CanAccessAsync(
            request.CourseId,
            request.UserId,
            request.Access,
            cancellationToken);
        return allowed
            ? AuthorizationDecision.Allow()
            : AuthorizationDecision.Deny(
                "COURSE.ACCESS_DENIED",
                "The course was not found or is not available to this account.");
    }
}
