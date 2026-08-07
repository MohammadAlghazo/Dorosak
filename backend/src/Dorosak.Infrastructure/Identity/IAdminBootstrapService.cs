namespace Dorosak.Infrastructure.Identity;

public interface IAdminBootstrapService
{
    Task<AdminBootstrapResult> ExecuteAsync(CancellationToken cancellationToken);
}

public sealed record AdminBootstrapResult(bool Created, bool AlreadyExists);
