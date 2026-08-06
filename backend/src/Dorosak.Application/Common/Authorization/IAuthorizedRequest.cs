namespace Dorosak.Application.Common.Authorization;

public interface IAuthorizedRequest;

public interface IRequestAuthorizer<in TRequest>
{
    ValueTask<AuthorizationDecision> AuthorizeAsync(TRequest request, CancellationToken cancellationToken);
}
