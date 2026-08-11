using Dorosak.Domain.Common;
using Dorosak.Domain.Communications;

namespace Dorosak.Domain.UnitTests.Communications;

public sealed class CommunicationTests
{
    [Fact]
    public void ParticipantMembershipCanEndOnlyOnce()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Conversation conversation = Conversation.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), now);
        ConversationParticipant participant = ConversationParticipant.Join(
            conversation.Id,
            Guid.CreateVersion7(),
            now);

        Assert.True(participant.IsCurrent);
        Assert.True(participant.Leave(now.AddMinutes(1)));
        Assert.False(participant.Leave(now.AddMinutes(2)));
        Assert.False(participant.IsCurrent);
        Assert.Equal(now.AddMinutes(1), participant.LeftAt);
    }

    [Fact]
    public void MessageNormalizesBodyAndRetainsClientIdentity()
    {
        Guid clientMessageId = Guid.CreateVersion7();
        Message message = Message.Create(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            clientMessageId,
            "  Synthetic message body.  ",
            1,
            DateTimeOffset.UtcNow);

        Assert.Equal(clientMessageId, message.ClientMessageId);
        Assert.Equal("Synthetic message body.", message.Body);
        Assert.Equal(7, message.Id.Version);
    }

    [Fact]
    public void MessageRejectsEmptyOrOversizedBodies()
    {
        Guid conversationId = Guid.CreateVersion7();
        Guid senderId = Guid.CreateVersion7();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        DomainRuleException empty = Assert.Throws<DomainRuleException>(() => Message.Create(
            conversationId,
            senderId,
            Guid.CreateVersion7(),
            "   ",
            1,
            now));
        DomainRuleException oversized = Assert.Throws<DomainRuleException>(() => Message.Create(
            conversationId,
            senderId,
            Guid.CreateVersion7(),
            new string('x', Message.MaximumBodyLength + 1),
            1,
            now));

        Assert.Equal("MESSAGE.BODY_INVALID", empty.Code);
        Assert.Equal("MESSAGE.BODY_INVALID", oversized.Code);
    }

    [Fact]
    public void ConversationAndMessagesUseMonotonicSequences()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Conversation conversation = Conversation.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), now);

        long first = conversation.RecordMessage(now);
        long second = conversation.RecordMessage(now.AddSeconds(1));
        Message message = Message.Create(
            conversation.Id,
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            "Sequenced message.",
            second,
            now.AddSeconds(1));

        Assert.Equal(1, first);
        Assert.Equal(2, second);
        Assert.Equal(second, conversation.LastSequence);
        Assert.Equal(second, message.Sequence);
    }

    [Fact]
    public void ConversationUpdatedAtDoesNotRegressWhenMessageClockMovesBackward()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Conversation conversation = Conversation.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), now);

        conversation.RecordMessage(now.AddSeconds(2));
        conversation.RecordMessage(now.AddSeconds(1));

        Assert.Equal(2, conversation.LastSequence);
        Assert.Equal(now.AddSeconds(2), conversation.UpdatedAt);
    }

    [Fact]
    public void NotificationSequenceIsPerOwnerAndReadIsMonotonic()
    {
        Guid ownerId = Guid.CreateVersion7();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        NotificationSequence sequence = NotificationSequence.Create(ownerId);
        Notification notification = Notification.CreateForMessage(
            ownerId,
            Guid.CreateVersion7(),
            sequence.Advance(),
            now);

        Assert.Equal(ownerId, notification.UserId);
        Assert.Equal(1, notification.Sequence);
        Assert.Equal(Guid.Empty, notification.TargetAnnouncementId);
        Assert.Equal(0, notification.TargetAnnouncementVersion);
        Assert.Equal(2, sequence.Advance());
        Assert.True(notification.MarkRead(now.AddSeconds(1)));
        Assert.False(notification.MarkRead(now.AddSeconds(2)));
        Assert.True(notification.IsRead);
        Assert.Equal(now.AddSeconds(1), notification.ReadAt);
    }

    [Fact]
    public void AnnouncementBoundsContentAndVersionsOwnedProjections()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Announcement announcement = Announcement.Create(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            "  Course update  ",
            "  Durable learner announcement.  ",
            now);
        Notification notification = Notification.CreateForAnnouncement(
            Guid.CreateVersion7(),
            announcement.Id,
            announcement.Version,
            1,
            announcement.Title,
            announcement.Body,
            now);
        AnnouncementTarget target = AnnouncementTarget.Create(
            announcement.Id,
            notification.UserId,
            announcement.Version,
            notification.Id,
            now);

        Assert.Equal("Course update", announcement.Title);
        Assert.Equal("Durable learner announcement.", announcement.Body);
        Assert.Equal(announcement.Version, notification.AnnouncementVersion);
        Assert.Equal(announcement.Id, notification.TargetAnnouncementId);
        Assert.Equal(announcement.Version, notification.TargetAnnouncementVersion);
        Assert.Equal(notification.UserId, target.UserId);
        Assert.True(announcement.Update("Course update revised", "Revised body.", now.AddMinutes(1)));
        Assert.Equal(2, announcement.Version);
        Assert.False(announcement.Update("Course update revised", "Revised body.", now.AddMinutes(2)));

        DomainRuleException title = Assert.Throws<DomainRuleException>(() => Announcement.Create(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            new string('x', Announcement.MaximumTitleLength + 1),
            "Body",
            now));
        DomainRuleException body = Assert.Throws<DomainRuleException>(() => Announcement.Create(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            "Title",
            new string('x', Announcement.MaximumBodyLength + 1),
            now));

        Assert.Equal("ANNOUNCEMENT.TITLE_INVALID", title.Code);
        Assert.Equal("ANNOUNCEMENT.BODY_INVALID", body.Code);
    }
}
