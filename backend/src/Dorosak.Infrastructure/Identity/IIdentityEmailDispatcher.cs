namespace Dorosak.Infrastructure.Identity;

public interface IIdentityEmailDispatcher
{
    Task<int> DispatchPendingAsync(CancellationToken cancellationToken);
}
