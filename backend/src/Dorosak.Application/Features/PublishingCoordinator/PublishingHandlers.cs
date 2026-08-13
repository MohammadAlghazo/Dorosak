using Dorosak.Application.Common.Models;
using Dorosak.Application.Common.Results;
using MediatR;

namespace Dorosak.Application.Features.PublishingCoordinator;

internal sealed class PublishingHandlers(IPublishingService service)
    : IRequestHandler<RequestPublicationCommand, Result<PublicationStatusResponse>>,
      IRequestHandler<WithdrawPublicationCommand, Result<PublicationStatusResponse>>,
      IRequestHandler<GetPublicationStatusQuery, Result<PublicationStatusResponse>>,
      IRequestHandler<ReviewPublicationCommand, Result<PublicationReviewResponse>>,
      IRequestHandler<GetPublicationReviewsQuery, Result<PagedResponse<PublicationReviewResponse>>>
{
    public Task<Result<PublicationStatusResponse>> Handle(RequestPublicationCommand request, CancellationToken cancellationToken)
        => service.RequestPublicationAsync(request, cancellationToken);

    public Task<Result<PublicationStatusResponse>> Handle(WithdrawPublicationCommand request, CancellationToken cancellationToken)
        => service.WithdrawPublicationAsync(request, cancellationToken);

    public Task<Result<PublicationStatusResponse>> Handle(GetPublicationStatusQuery request, CancellationToken cancellationToken)
        => service.GetPublicationStatusAsync(request, cancellationToken);

    public Task<Result<PublicationReviewResponse>> Handle(ReviewPublicationCommand request, CancellationToken cancellationToken)
        => service.ReviewPublicationAsync(request, cancellationToken);

    public Task<Result<PagedResponse<PublicationReviewResponse>>> Handle(GetPublicationReviewsQuery request, CancellationToken cancellationToken)
        => service.GetPublicationReviewsAsync(request, cancellationToken);
}
