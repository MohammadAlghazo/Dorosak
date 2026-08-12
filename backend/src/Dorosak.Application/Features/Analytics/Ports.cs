using Dorosak.Application.Common.Results;

namespace Dorosak.Application.Features.Analytics;

public interface IAnalyticsService
{
    Task<Result<AdminAnalyticsOverviewResponse>> GetAdminOverviewAsync(
        CancellationToken cancellationToken);
}
