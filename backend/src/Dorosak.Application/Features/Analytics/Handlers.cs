using Dorosak.Application.Common.Results;
using MediatR;

namespace Dorosak.Application.Features.Analytics;

internal sealed class GetAdminAnalyticsOverviewQueryHandler(IAnalyticsService service)
    : IRequestHandler<GetAdminAnalyticsOverviewQuery, Result<AdminAnalyticsOverviewResponse>>
{
    public Task<Result<AdminAnalyticsOverviewResponse>> Handle(
        GetAdminAnalyticsOverviewQuery request,
        CancellationToken cancellationToken) => service.GetAdminOverviewAsync(cancellationToken);
}
