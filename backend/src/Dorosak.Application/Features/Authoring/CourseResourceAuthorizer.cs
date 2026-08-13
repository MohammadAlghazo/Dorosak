using Dorosak.Application.Common.Authorization;

namespace Dorosak.Application.Features.Authoring;

internal sealed class CourseResourceAuthorizer<TRequest>(ICourseAccessReader accessReader)
    : IRequestAuthorizer<TRequest>
    where TRequest : ICourseAuthorizedRequest
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
