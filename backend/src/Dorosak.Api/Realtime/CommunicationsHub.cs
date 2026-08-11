using System.Security.Claims;
using Dorosak.Application.Features.Communications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Dorosak.Api.Realtime;

[Authorize]
public sealed class CommunicationsHub : Hub
{
    public const string Path = "/hubs/communications";
}

internal sealed class SubjectUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection)
    {
        string? subject = connection.User?.FindFirstValue("sub");
        return Guid.TryParse(subject, out Guid userId) ? userId.ToString("D") : null;
    }
}

internal sealed class SignalRCommunicationsRealtimePublisher(
    IHubContext<CommunicationsHub> hubContext) : ICommunicationsRealtimePublisher
{
    public Task PublishAsync<TPayload>(
        IReadOnlyCollection<Guid> userIds,
        CommunicationsRealtimeEnvelope<TPayload> envelope,
        CancellationToken cancellationToken)
        where TPayload : class
    {
        string[] targets = userIds
            .Select(userId => userId.ToString("D"))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return hubContext.Clients.Users(targets).SendAsync(
            CommunicationsRealtimeEvents.ClientMethod,
            envelope,
            cancellationToken);
    }
}
