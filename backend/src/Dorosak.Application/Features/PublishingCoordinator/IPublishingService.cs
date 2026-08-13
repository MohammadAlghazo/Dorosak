using Dorosak.Application.Common.Results;

namespace Dorosak.Application.Features.PublishingCoordinator;

public interface IPublishingService
{
    Task<Result<PublicationStatusResponse>> RequestPublicationAsync(RequestPublicationCommand command, CancellationToken cancellationToken);
    Task<Result<PublicationStatusResponse>> WithdrawPublicationAsync(WithdrawPublicationCommand command, CancellationToken cancellationToken);
    Task<Result<PublicationReviewResponse>> ReviewPublicationAsync(ReviewPublicationCommand command, CancellationToken cancellationToken);
    Task<Result<PublicationStatusResponse>> GetPublicationStatusAsync(GetPublicationStatusQuery query, CancellationToken cancellationToken);
    Task<Result<PagedResponse<PublicationReviewResponse>>> GetPublicationReviewsAsync(GetPublicationReviewsQuery query, CancellationToken cancellationToken);
}
