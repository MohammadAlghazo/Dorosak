using Dorosak.Application.Common.Messaging;

namespace Dorosak.Application.Features.Communications;

public sealed record GetConversationsQuery(
    Guid UserId,
    int Limit,
    string? Cursor) : IQuery<ConversationPageResponse>;

public sealed record CreateConversationCommand(
    Guid UserId,
    IReadOnlyList<Guid> ParticipantUserIds,
    Guid CourseId,
    string IdempotencyKey) : IIdempotentCommand<ConversationResponse>
{
    public string IdempotencyOperation => "communications.conversation-create.v1";

    public string IdempotencyScope => $"user:{UserId:D}";

    public object IdempotencyPayload => new
    {
        ParticipantUserIds = ParticipantUserIds.Order().ToArray(),
        CourseId,
    };

    public int ResponseSchemaVersion => 1;

    public TimeSpan Retention => TimeSpan.FromDays(30);
}

public sealed record GetConversationMessagesQuery(
    Guid UserId,
    Guid ConversationId,
    int Limit,
    string? Cursor,
    long? AfterSequence = null) : IQuery<MessagePageResponse>, IConversationAuthorizedRequest;

public sealed record CreateMessageCommand(
    Guid UserId,
    Guid ConversationId,
    Guid ClientMessageId,
    string Body,
    string IdempotencyKey) : IIdempotentCommand<MessageResponse>, IConversationAuthorizedRequest
{
    public string IdempotencyOperation => "communications.message-create.v1";

    public string IdempotencyScope => $"user:{UserId:D}";

    public object IdempotencyPayload => new { ConversationId, ClientMessageId, Body };

    public int ResponseSchemaVersion => 1;

    public TimeSpan Retention => TimeSpan.FromHours(24);
}

public sealed record LeaveConversationCommand(
    Guid UserId,
    Guid ConversationId) : ITransactionalCommand<ConversationOperationResponse>, IConversationAuthorizedRequest;

public sealed record GetNotificationsQuery(
    Guid UserId,
    int Limit,
    string? Cursor,
    long? AfterSequence) : IQuery<NotificationPageResponse>;

public sealed record GetNotificationUnreadCountQuery(Guid UserId) : IQuery<NotificationUnreadCountResponse>;

public sealed record MarkNotificationReadCommand(
    Guid UserId,
    Guid NotificationId) : ITransactionalCommand<NotificationResponse>;

public sealed record MarkAllNotificationsReadCommand(Guid UserId) : ITransactionalCommand<NotificationsReadResponse>;

public sealed record GetAnnouncementsQuery(
    Guid UserId,
    Guid CourseId,
    int Limit,
    string? Cursor) : IQuery<AnnouncementPageResponse>, IAnnouncementAuthorizedRequest;

public sealed record GetAnnouncementQuery(
    Guid UserId,
    Guid CourseId,
    Guid AnnouncementId) : IQuery<AnnouncementResponse>, IAnnouncementAuthorizedRequest;

public sealed record CreateAnnouncementCommand(
    Guid UserId,
    Guid CourseId,
    string Title,
    string Body,
    string IdempotencyKey) : IIdempotentCommand<AnnouncementResponse>, IAnnouncementAuthorizedRequest
{
    public string IdempotencyOperation => "communications.announcement-create.v1";

    public string IdempotencyScope => $"user:{UserId:D}";

    public object IdempotencyPayload => new { CourseId, Title, Body };

    public int ResponseSchemaVersion => 1;

    public TimeSpan Retention => TimeSpan.FromDays(30);
}

public sealed record UpdateAnnouncementCommand(
    Guid UserId,
    Guid CourseId,
    Guid AnnouncementId,
    string Title,
    string Body,
    string IdempotencyKey) : IIdempotentCommand<AnnouncementResponse>, IAnnouncementAuthorizedRequest
{
    public string IdempotencyOperation => "communications.announcement-update.v1";

    public string IdempotencyScope => $"user:{UserId:D}";

    public object IdempotencyPayload => new { CourseId, AnnouncementId, Title, Body };

    public int ResponseSchemaVersion => 1;

    public TimeSpan Retention => TimeSpan.FromDays(30);
}

public sealed record DeleteAnnouncementCommand(
    Guid UserId,
    Guid CourseId,
    Guid AnnouncementId) : ITransactionalCommand<AnnouncementOperationResponse>, IAnnouncementAuthorizedRequest;
