using System.Globalization;
using System.Security.Claims;
using Dorosak.Application.Features.Communications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Dorosak.Api.Realtime;

[Authorize]
public sealed class CommunicationsHub(CommunicationsConnectionRegistry registry) : Hub
{
    public const string Path = "/hubs/communications";

    public override async Task OnConnectedAsync()
    {
        string? userValue = Context.User?.FindFirstValue("sub");
        string? sessionValue = Context.User?.FindFirstValue("sid");
        string? versionValue = Context.User?.FindFirstValue("authz_ver");
        if (!Guid.TryParse(userValue, out Guid userId) ||
            !Guid.TryParse(sessionValue, out Guid sessionId) ||
            !int.TryParse(
                versionValue,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int authorizationVersion) ||
            authorizationVersion <= 0)
        {
            Context.Abort();
            return;
        }

        registry.Register(
            Context.ConnectionId,
            userId,
            sessionId,
            authorizationVersion,
            Context.Abort);
        try
        {
            await base.OnConnectedAsync();
        }
        catch
        {
            registry.Remove(Context.ConnectionId);
            throw;
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        registry.Remove(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
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
