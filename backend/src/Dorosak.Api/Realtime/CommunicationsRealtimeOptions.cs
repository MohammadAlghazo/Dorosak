namespace Dorosak.Api.Realtime;

public sealed class CommunicationsRealtimeOptions
{
    public const string SectionName = "CommunicationsRealtime";

    public bool DispatcherEnabled { get; init; }

    public TimeSpan IdleDelay { get; init; } = TimeSpan.FromSeconds(2);

    public TimeSpan FailureDelay { get; init; } = TimeSpan.FromSeconds(10);

    public CommunicationsRealtimeRedisOptions Redis { get; init; } = new();
}

public sealed class CommunicationsRealtimeRedisOptions
{
    public bool Enabled { get; init; }

    public string ConnectionStringName { get; init; } = "RedisRealtime";

    public string ChannelPrefixRoot { get; init; } = "dorosak";
}
