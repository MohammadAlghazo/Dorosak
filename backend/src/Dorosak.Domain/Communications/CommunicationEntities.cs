using Dorosak.Domain.Common;

namespace Dorosak.Domain.Communications;

public sealed class Conversation
{
    private Conversation()
    {
    }

    private Conversation(
        Guid id,
        Guid createdByUserId,
        Guid courseId,
        DateTimeOffset createdAt)
    {
        Id = id;
        CreatedByUserId = createdByUserId;
        CourseId = courseId;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public Guid CourseId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public long LastSequence { get; private set; }

    public static Conversation Create(Guid createdByUserId, Guid courseId, DateTimeOffset now)
    {
        if (createdByUserId == Guid.Empty || courseId == Guid.Empty)
        {
            throw new DomainRuleException(
                "CONVERSATION.IDENTITY_REQUIRED",
                "Conversation ownership identifiers must be valid.");
        }

        EnsureUtc(now);
        return new Conversation(Guid.CreateVersion7(), createdByUserId, courseId, now);
    }

    public long RecordMessage(DateTimeOffset now)
    {
        EnsureUtc(now);
        if (now < CreatedAt)
        {
            throw new DomainRuleException(
                "CONVERSATION.MESSAGE_TIME_INVALID",
                "A message cannot precede its conversation.");
        }

        LastSequence = NextMessageSequence();
        if (now > UpdatedAt)
        {
            UpdatedAt = now;
        }
        return LastSequence;
    }

    public long NextMessageSequence()
    {
        if (LastSequence == long.MaxValue)
        {
            throw new DomainRuleException(
                "CONVERSATION.SEQUENCE_EXHAUSTED",
                "The conversation message sequence has been exhausted.");
        }

        return LastSequence + 1;
    }

    private static void EnsureUtc(DateTimeOffset value)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new DomainRuleException("CONVERSATION.UTC_REQUIRED", "Conversation timestamps must use UTC.");
        }
    }
}

public sealed class ConversationParticipant
{
    private ConversationParticipant()
    {
    }

    private ConversationParticipant(Guid conversationId, Guid userId, DateTimeOffset joinedAt)
    {
        ConversationId = conversationId;
        UserId = userId;
        JoinedAt = joinedAt;
    }

    public Guid ConversationId { get; private set; }

    public Guid UserId { get; private set; }

    public DateTimeOffset JoinedAt { get; private set; }

    public DateTimeOffset? LeftAt { get; private set; }

    public bool IsCurrent => LeftAt is null;

    public static ConversationParticipant Join(Guid conversationId, Guid userId, DateTimeOffset now)
    {
        if (conversationId == Guid.Empty || userId == Guid.Empty)
        {
            throw new DomainRuleException(
                "CONVERSATION_PARTICIPANT.IDENTITY_REQUIRED",
                "Conversation and participant identifiers are required.");
        }
        if (now.Offset != TimeSpan.Zero)
        {
            throw new DomainRuleException(
                "CONVERSATION_PARTICIPANT.UTC_REQUIRED",
                "Participant timestamps must use UTC.");
        }

        return new ConversationParticipant(conversationId, userId, now);
    }

    public bool Leave(DateTimeOffset now)
    {
        if (LeftAt is not null)
        {
            return false;
        }
        if (now.Offset != TimeSpan.Zero || now < JoinedAt)
        {
            throw new DomainRuleException(
                "CONVERSATION_PARTICIPANT.LEAVE_TIME_INVALID",
                "The participant leave time is invalid.");
        }

        LeftAt = now;
        return true;
    }
}

public sealed class Message
{
    public const int MaximumBodyLength = 5000;

    private Message()
    {
    }

    private Message(
        Guid id,
        Guid conversationId,
        Guid senderUserId,
        Guid clientMessageId,
        string body,
        long sequence,
        DateTimeOffset createdAt)
    {
        Id = id;
        ConversationId = conversationId;
        SenderUserId = senderUserId;
        ClientMessageId = clientMessageId;
        Body = body;
        Sequence = sequence;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid ConversationId { get; private set; }

    public Guid SenderUserId { get; private set; }

    public Guid ClientMessageId { get; private set; }

    public string Body { get; private set; } = string.Empty;

    public long Sequence { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static Message Create(
        Guid conversationId,
        Guid senderUserId,
        Guid clientMessageId,
        string body,
        long sequence,
        DateTimeOffset now)
    {
        if (conversationId == Guid.Empty || senderUserId == Guid.Empty || clientMessageId == Guid.Empty)
        {
            throw new DomainRuleException("MESSAGE.IDENTITY_REQUIRED", "Message identifiers are required.");
        }

        string normalizedBody = body?.Trim() ?? string.Empty;
        if (normalizedBody.Length is 0 or > MaximumBodyLength)
        {
            throw new DomainRuleException(
                "MESSAGE.BODY_INVALID",
                $"A message body is required and cannot exceed {MaximumBodyLength} characters.");
        }
        if (sequence <= 0)
        {
            throw new DomainRuleException(
                "MESSAGE.SEQUENCE_INVALID",
                "A message sequence must be positive.");
        }
        if (now.Offset != TimeSpan.Zero)
        {
            throw new DomainRuleException("MESSAGE.UTC_REQUIRED", "Message timestamps must use UTC.");
        }

        return new Message(
            Guid.CreateVersion7(),
            conversationId,
            senderUserId,
            clientMessageId,
            normalizedBody,
            sequence,
            now);
    }
}
