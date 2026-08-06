using System.Net;

namespace Dorosak.Api.Middleware;

public sealed class CorrelationIdOptions
{
    public string[] TrustedClientAddresses { get; init; } = [];

    public bool HasValidAddresses() => TrustedClientAddresses.All(address => IPAddress.TryParse(address, out _));
}
