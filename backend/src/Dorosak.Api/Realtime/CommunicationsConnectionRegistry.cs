using System.Collections.Concurrent;

namespace Dorosak.Api.Realtime;

public sealed class CommunicationsConnectionRegistry
{
    private readonly ConcurrentDictionary<string, CommunicationsConnectionRegistration> _connections =
        new(StringComparer.Ordinal);

    internal CommunicationsConnectionRegistration Register(
        string connectionId,
        Guid userId,
        Guid sessionId,
        int authorizationVersion,
        Action abort)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(authorizationVersion);
        ArgumentNullException.ThrowIfNull(abort);

        var registration = new CommunicationsConnectionRegistration(
            connectionId,
            userId,
            sessionId,
            authorizationVersion,
            abort);
        if (!_connections.TryAdd(connectionId, registration))
        {
            throw new InvalidOperationException("The realtime connection is already registered.");
        }

        return registration;
    }

    internal bool Remove(string connectionId) => _connections.TryRemove(connectionId, out _);

    internal bool Remove(CommunicationsConnectionRegistration registration) =>
        ((ICollection<KeyValuePair<string, CommunicationsConnectionRegistration>>)_connections)
            .Remove(new KeyValuePair<string, CommunicationsConnectionRegistration>(
                registration.ConnectionId,
                registration));

    internal CommunicationsConnectionRegistration[] Snapshot() => [.. _connections.Values];
}

internal sealed class CommunicationsConnectionRegistration(
    string connectionId,
    Guid userId,
    Guid sessionId,
    int authorizationVersion,
    Action abort)
{
    public string ConnectionId { get; } = connectionId;

    public Guid UserId { get; } = userId;

    public Guid SessionId { get; } = sessionId;

    public int AuthorizationVersion { get; } = authorizationVersion;

    public Action Abort { get; } = abort;
}
