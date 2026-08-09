using Dorosak.Application.Common.Results;

namespace Dorosak.Application.Features.Commerce;

public interface ICommerceService
{
    Task<Result<DemoCheckoutResponse>> CreateDemoCheckoutAsync(
        CreateDemoCheckoutCommand request,
        CancellationToken cancellationToken);
}
