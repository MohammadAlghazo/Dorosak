using Dorosak.Domain.Common;
using Dorosak.Domain.Engagement;

namespace Dorosak.Domain.UnitTests.Engagement;

public sealed class ModerationTests
{
    [Fact]
    public void ReportRequiresExactlyOneConcreteTarget()
    {
        Guid reporterId = Guid.CreateVersion7();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        DomainRuleException missing = Assert.Throws<DomainRuleException>(() => ContentReport.Create(
            reporterId, null, null, null, null, null, ContentReportReason.Spam, null, now));
        DomainRuleException multiple = Assert.Throws<DomainRuleException>(() => ContentReport.Create(
            reporterId,
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            null,
            null,
            null,
            ContentReportReason.Spam,
            null,
            now));

        Assert.Equal("REPORT.TARGET_INVALID", missing.Code);
        Assert.Equal("REPORT.TARGET_INVALID", multiple.Code);
    }

    [Fact]
    public void AccountCannotReportItself()
    {
        Guid userId = Guid.CreateVersion7();

        DomainRuleException exception = Assert.Throws<DomainRuleException>(() => ContentReport.Create(
            userId,
            null,
            null,
            null,
            userId,
            null,
            ContentReportReason.Harassment,
            "Synthetic report details",
            DateTimeOffset.UtcNow));

        Assert.Equal("REPORT.SELF_REPORT_INVALID", exception.Code);
    }

    [Fact]
    public void CaseAndReportFollowReviewThenResolveWorkflow()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        ContentReport report = ContentReport.Create(
            Guid.CreateVersion7(),
            null,
            Guid.CreateVersion7(),
            null,
            null,
            null,
            ContentReportReason.Spam,
            null,
            now);
        ModerationCase moderationCase = ModerationCase.Create(report.Id, now);
        Guid actorId = Guid.CreateVersion7();

        Assert.True(report.StartReview(now.AddMinutes(1)));
        Assert.True(moderationCase.StartReview(actorId, now.AddMinutes(1)));
        moderationCase.Close(dismissed: false, now.AddMinutes(2));
        report.Resolve(dismissed: false, now.AddMinutes(2));

        Assert.Equal(ContentReportStatus.Resolved, report.Status);
        Assert.Equal(ModerationCaseStatus.Resolved, moderationCase.Status);
        Assert.Equal(actorId, moderationCase.AssignedToUserId);
        Assert.Equal(3, moderationCase.Version);
        Assert.NotNull(report.ClosedAt);
        Assert.NotNull(moderationCase.ClosedAt);
    }

    [Fact]
    public void MessageReportPersistsItsImmutableModerationSnapshot()
    {
        Guid reporterId = Guid.CreateVersion7();
        Guid messageId = Guid.CreateVersion7();
        DateTimeOffset createdAt = DateTimeOffset.UtcNow;
        var snapshot = new MessageReportSnapshot(
            Guid.CreateVersion7(),
            "Message sender",
            Guid.CreateVersion7(),
            "Message course",
            Guid.CreateVersion7(),
            7,
            "Immutable message body",
            createdAt);

        ContentReport report = ContentReport.Create(
            reporterId,
            null,
            null,
            null,
            null,
            messageId,
            ContentReportReason.Harassment,
            "Message report details",
            createdAt,
            snapshot);

        Assert.Equal(messageId, report.MessageId);
        Assert.Equal(snapshot.Body, report.MessageBodySnapshot);
        Assert.Equal(snapshot.SenderName, report.MessageSenderNameSnapshot);
        Assert.Equal(snapshot.CourseId, report.MessageCourseIdSnapshot);
        Assert.Equal(snapshot.ConversationId, report.MessageConversationIdSnapshot);
    }

    [Fact]
    public void AccountCannotReportItsOwnMessage()
    {
        Guid userId = Guid.CreateVersion7();
        DateTimeOffset createdAt = DateTimeOffset.UtcNow;
        var snapshot = new MessageReportSnapshot(
            userId,
            "Message sender",
            Guid.CreateVersion7(),
            "Message course",
            Guid.CreateVersion7(),
            1,
            "Immutable message body",
            createdAt);

        DomainRuleException exception = Assert.Throws<DomainRuleException>(() => ContentReport.Create(
            userId,
            null,
            null,
            null,
            null,
            Guid.CreateVersion7(),
            ContentReportReason.Harassment,
            null,
            createdAt,
            snapshot));

        Assert.Equal("REPORT.SELF_REPORT_INVALID", exception.Code);
    }

    [Fact]
    public void ModerationActionRequiresUsefulReason()
    {
        DomainRuleException exception = Assert.Throws<DomainRuleException>(() => ModerationAction.Create(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            ModerationActionType.HideContent,
            "short",
            DateTimeOffset.UtcNow));

        Assert.Equal("MODERATION.REASON_INVALID", exception.Code);
    }

    [Fact]
    public void HiddenCommentCanOnlyBeRestoredByModerationTransition()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DiscussionComment comment = DiscussionComment.Create(
            Guid.CreateVersion7(), null, -1, Guid.CreateVersion7(), "Synthetic comment", now);

        Assert.True(comment.Hide(now.AddMinutes(1)));
        Assert.False(comment.Hide(now.AddMinutes(2)));
        Assert.Throws<DomainRuleException>(() => comment.Update("Edited", now.AddMinutes(2)));
        Assert.True(comment.Restore(now.AddMinutes(3)));

        Assert.Equal(DiscussionCommentStatus.Published, comment.Status);
    }
}
