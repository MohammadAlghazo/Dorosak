using Dorosak.Application.Common.Results;
using MediatR;

namespace Dorosak.Application.Features.Communications;

internal sealed class CommunicationsHandler<TRequest, TResponse>(ICommunicationsService service)
    : IRequestHandler<TRequest, Result<TResponse>>
    where TRequest : class, IRequest<Result<TResponse>>
    where TResponse : notnull
{
    public Task<Result<TResponse>> Handle(TRequest request, CancellationToken cancellationToken) => request switch
    {
        GetConversationsQuery query => Cast(service.GetConversationsAsync(query, cancellationToken)),
        CreateConversationCommand command => Cast(service.CreateConversationAsync(command, cancellationToken)),
        GetConversationMessagesQuery query => Cast(service.GetConversationMessagesAsync(query, cancellationToken)),
        CreateMessageCommand command => Cast(service.CreateMessageAsync(command, cancellationToken)),
        LeaveConversationCommand command => Cast(service.LeaveConversationAsync(command, cancellationToken)),
        GetNotificationsQuery query => Cast(service.GetNotificationsAsync(query, cancellationToken)),
        GetNotificationUnreadCountQuery query => Cast(service.GetNotificationUnreadCountAsync(query, cancellationToken)),
        MarkNotificationReadCommand command => Cast(service.MarkNotificationReadAsync(command, cancellationToken)),
        MarkAllNotificationsReadCommand command => Cast(service.MarkAllNotificationsReadAsync(command, cancellationToken)),
        GetAnnouncementsQuery query => Cast(service.GetAnnouncementsAsync(query, cancellationToken)),
        GetAnnouncementQuery query => Cast(service.GetAnnouncementAsync(query, cancellationToken)),
        CreateAnnouncementCommand command => Cast(service.CreateAnnouncementAsync(command, cancellationToken)),
        UpdateAnnouncementCommand command => Cast(service.UpdateAnnouncementAsync(command, cancellationToken)),
        DeleteAnnouncementCommand command => Cast(service.DeleteAnnouncementAsync(command, cancellationToken)),
        _ => throw new InvalidOperationException($"Unsupported communications request {typeof(TRequest).Name}."),
    };

    private static async Task<Result<TResponse>> Cast<TValue>(Task<Result<TValue>> task)
    {
        Result<TValue> result = await task;
        return result.IsSuccess
            ? Result.Success((TResponse)(object)result.Value!)
            : Result.Failure<TResponse>(result.Failure);
    }
}
