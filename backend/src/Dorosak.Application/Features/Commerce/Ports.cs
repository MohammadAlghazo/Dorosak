using Dorosak.Application.Common.Results;

namespace Dorosak.Application.Features.Commerce;

public interface ICommerceService
{
    Task<Result<DemoCheckoutResponse>> CreateDemoCheckoutAsync(
        CreateDemoCheckoutCommand request,
        CancellationToken cancellationToken);

    Task<Result<DemoSubscriptionStateResponse>> GetDemoSubscriptionAsync(
        GetDemoSubscriptionQuery request,
        CancellationToken cancellationToken);

    Task<Result<DemoSubscriptionResponse>> ActivateDemoSubscriptionAsync(
        ActivateDemoSubscriptionCommand request,
        CancellationToken cancellationToken);

    Task<Result<DemoSubscriptionResponse>> CancelDemoSubscriptionAsync(
        CancelDemoSubscriptionCommand request,
        CancellationToken cancellationToken);
}
