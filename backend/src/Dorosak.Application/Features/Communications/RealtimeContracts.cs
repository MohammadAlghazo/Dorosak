namespace Dorosak.Application.Features.Communications;

public static class CommunicationsRealtimeEvents
{
    public const int SchemaVersion = 1;

    public const string ClientMethod = "communicationEvent";

    public const string ConversationCreated = "communication.conversation-created";

    public const string MessageCreated = "communication.message-created";

    public const string ConversationLeft = "communication.conversation-left";

    public const string AnnouncementCreated = "communication.announcement-created";

    public const string AnnouncementUpdated = "communication.announcement-updated";

    public const string AnnouncementDeleted = "communication.announcement-deleted";
}

public sealed record CommunicationsRealtimeEnvelope<TPayload>(
    Guid EventId,
    string EventType,
    int SchemaVersion,
    DateTimeOffset OccurredAt,
    TPayload Payload)
    where TPayload : class;

public sealed record ConversationCreatedRealtimePayload(
    Guid ConversationId,
    Guid CreatedByUserId,
    Guid CourseId);

public sealed record MessageCreatedRealtimePayload(
    Guid MessageId,
    Guid ConversationId,
    Guid SenderUserId,
    long Sequence);

public sealed record ConversationLeftRealtimePayload(
    Guid ConversationId,
    Guid UserId);

public sealed record AnnouncementCreatedRealtimePayload(
    Guid AnnouncementId,
    Guid CourseId,
    Guid CreatedByUserId,
    long Version,
    long TargetCount);

public sealed record AnnouncementUpdatedRealtimePayload(
    Guid AnnouncementId,
    Guid CourseId,
    Guid UpdatedByUserId,
    long Version,
    long TargetCount);

public sealed record AnnouncementDeletedRealtimePayload(
    Guid AnnouncementId,
    Guid CourseId,
    Guid DeletedByUserId,
    long Version);
