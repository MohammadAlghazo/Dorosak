using Dorosak.Application.Common.Authorization;

namespace Dorosak.Application.Features.Media;

internal sealed class MediaResourceAuthorizer<TRequest>(IMediaAccessReader accessReader)
    : IRequestAuthorizer<TRequest>
    where TRequest : IMediaAuthorizedRequest
{
    public async ValueTask<AuthorizationDecision> AuthorizeAsync(
        TRequest request,
        CancellationToken cancellationToken)
    {
        bool allowed = request.Target == MediaAuthorizationTarget.Asset
            ? await accessReader.CanAccessAssetAsync(request.MediaId, request.UserId, cancellationToken)
            : await accessReader.CanAccessUploadSessionAsync(request.MediaId, request.UserId, cancellationToken);

        return allowed
            ? AuthorizationDecision.Allow()
            : AuthorizationDecision.Deny(
                "MEDIA.ACCESS_DENIED",
                "The media resource was not found or is not available to this account.");
    }
}
