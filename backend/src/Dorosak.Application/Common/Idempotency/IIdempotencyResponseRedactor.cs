namespace Dorosak.Application.Common.Idempotency;

public interface IIdempotencyResponseRedactor<in TRequest, TResponse>
{
    TResponse Redact(TRequest request, TResponse response);
}
