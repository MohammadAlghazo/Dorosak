using Dorosak.Application.Common.Results;

namespace Dorosak.Application.Features.Communications;

public interface ICommunicationsService
{
    Task<Result<ConversationPageResponse>> GetConversationsAsync(
        GetConversationsQuery request,
        CancellationToken cancellationToken);

    Task<Result<ConversationResponse>> CreateConversationAsync(
        CreateConversationCommand request,
        CancellationToken cancellationToken);

    Task<Result<MessagePageResponse>> GetConversationMessagesAsync(
        GetConversationMessagesQuery request,
        CancellationToken cancellationToken);

    Task<Result<MessageResponse>> CreateMessageAsync(
        CreateMessageCommand request,
        CancellationToken cancellationToken);

    Task<Result<ConversationOperationResponse>> LeaveConversationAsync(
        LeaveConversationCommand request,
        CancellationToken cancellationToken);

    Task<Result<NotificationPageResponse>> GetNotificationsAsync(
        GetNotificationsQuery request,
        CancellationToken cancellationToken);

    Task<Result<NotificationUnreadCountResponse>> GetNotificationUnreadCountAsync(
        GetNotificationUnreadCountQuery request,
        CancellationToken cancellationToken);

    Task<Result<NotificationResponse>> MarkNotificationReadAsync(
        MarkNotificationReadCommand request,
        CancellationToken cancellationToken);

    Task<Result<NotificationsReadResponse>> MarkAllNotificationsReadAsync(
        MarkAllNotificationsReadCommand request,
        CancellationToken cancellationToken);

    Task<Result<AnnouncementPageResponse>> GetAnnouncementsAsync(
        GetAnnouncementsQuery request,
        CancellationToken cancellationToken);

    Task<Result<AnnouncementResponse>> GetAnnouncementAsync(
        GetAnnouncementQuery request,
        CancellationToken cancellationToken);

    Task<Result<AnnouncementResponse>> CreateAnnouncementAsync(
        CreateAnnouncementCommand request,
        CancellationToken cancellationToken);

    Task<Result<AnnouncementResponse>> UpdateAnnouncementAsync(
        UpdateAnnouncementCommand request,
        CancellationToken cancellationToken);

    Task<Result<AnnouncementOperationResponse>> DeleteAnnouncementAsync(
        DeleteAnnouncementCommand request,
        CancellationToken cancellationToken);

    Task<Result<ConversationResponse>> GetConversationForReplayAsync(
        Guid userId,
        Guid conversationId,
        CancellationToken cancellationToken);

    Task<Result<MessageResponse>> GetMessageForReplayAsync(
        Guid userId,
        Guid conversationId,
        Guid messageId,
        CancellationToken cancellationToken);

    Task<Result<AnnouncementResponse>> GetAnnouncementForReplayAsync(
        Guid userId,
        Guid courseId,
        Guid announcementId,
        CancellationToken cancellationToken);
}

public interface IConversationAuthorizedRequest : Common.Authorization.IAuthorizedRequest
{
    Guid UserId { get; }

    Guid ConversationId { get; }
}

public interface IConversationAccessReader
{
    Task<bool> CanAccessAsync(
        Guid userId,
        Guid conversationId,
        CancellationToken cancellationToken);
}

public interface IAnnouncementAuthorizedRequest : Common.Authorization.IAuthorizedRequest
{
    Guid UserId { get; }

    Guid CourseId { get; }
}

public interface IAnnouncementAccessReader
{
    Task<bool> CanManageCourseAsync(
        Guid userId,
        Guid courseId,
        CancellationToken cancellationToken);
}
