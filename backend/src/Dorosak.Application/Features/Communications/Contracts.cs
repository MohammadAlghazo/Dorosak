namespace Dorosak.Application.Features.Communications;

public sealed record ConversationParticipantResponse(
    Guid UserId,
    string DisplayName,
    DateTimeOffset JoinedAt);

public sealed record ConversationResponse(
    Guid Id,
    Guid CourseId,
    Guid CreatedByUserId,
    IReadOnlyList<ConversationParticipantResponse> Participants,
    long LastSequence,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record ConversationPageResponse(
    IReadOnlyList<ConversationResponse> Items,
    string? NextCursor,
    bool HasMore);

public sealed record MessageResponse(
    Guid Id,
    Guid ConversationId,
    Guid SenderUserId,
    string SenderName,
    Guid ClientMessageId,
    long Sequence,
    string Body,
    DateTimeOffset CreatedAt);

public sealed record MessagePageResponse(
    IReadOnlyList<MessageResponse> Items,
    string? NextCursor,
    bool HasMore,
    long LatestSequence);

public sealed record NotificationResponse(
    Guid Id,
    long Sequence,
    string Type,
    Guid ResourceId,
    Guid? CourseId,
    Guid? ConversationId,
    Guid ActorUserId,
    long? AnnouncementVersion,
    string? Title,
    string? Body,
    bool IsRead,
    DateTimeOffset? ReadAt,
    DateTimeOffset CreatedAt);

public sealed record NotificationPageResponse(
    IReadOnlyList<NotificationResponse> Items,
    string? NextCursor,
    bool HasMore,
    long LatestSequence,
    long UnreadCount);

public sealed record NotificationUnreadCountResponse(long Count, long LatestSequence);

public sealed record NotificationsReadResponse(long UpdatedCount, long ThroughSequence);

public sealed record AnnouncementResponse(
    Guid Id,
    Guid CourseId,
    Guid CreatedByUserId,
    string Title,
    string Body,
    long Version,
    long TargetCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record AnnouncementPageResponse(
    IReadOnlyList<AnnouncementResponse> Items,
    string? NextCursor,
    bool HasMore);

public sealed record AnnouncementOperationResponse(bool Completed);

public sealed record ConversationOperationResponse(bool Completed);
