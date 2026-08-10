using Dorosak.Domain.Common;
using Dorosak.Domain.Engagement;

namespace Dorosak.Domain.UnitTests.Engagement;

public sealed class DiscussionTests
{
    [Fact]
    public void ReplyDepthStopsAtConfiguredMaximum()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DiscussionComment root = DiscussionComment.Create(
            Guid.CreateVersion7(), null, -1, Guid.CreateVersion7(), "Root", now);
        DiscussionComment reply = DiscussionComment.Create(
            root.ThreadId, root.Id, root.Depth, Guid.CreateVersion7(), "Reply", now);
        DiscussionComment nestedReply = DiscussionComment.Create(
            root.ThreadId, reply.Id, reply.Depth, Guid.CreateVersion7(), "Nested reply", now);

        DomainRuleException exception = Assert.Throws<DomainRuleException>(() => DiscussionComment.Create(
            root.ThreadId,
            nestedReply.Id,
            nestedReply.Depth,
            Guid.CreateVersion7(),
            "Too deeply nested reply",
            now));

        Assert.Equal("COMMENT.DEPTH_LIMIT", exception.Code);
        Assert.Equal(0, root.Depth);
        Assert.Equal(1, reply.Depth);
        Assert.Equal(2, nestedReply.Depth);
    }

    [Fact]
    public void ThreadCannotBeEditedAfterWindowCloses()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DiscussionThread thread = DiscussionThread.Create(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            "Release question",
            "How does the release stay pinned?",
            now);

        DomainRuleException exception = Assert.Throws<DomainRuleException>(() => thread.Update(
            "Updated question",
            "Updated body",
            now.AddMinutes(DiscussionThread.EditWindowMinutes + 1)));

        Assert.Equal("DISCUSSION.EDIT_WINDOW_CLOSED", exception.Code);
    }

    [Fact]
    public void RemovedCommentCannotExposeAnEditableState()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DiscussionComment comment = DiscussionComment.Create(
            Guid.CreateVersion7(), null, -1, Guid.CreateVersion7(), "Answer", now);

        Assert.True(comment.Remove(now.AddMinutes(1)));
        Assert.False(comment.Remove(now.AddMinutes(2)));

        DomainRuleException exception = Assert.Throws<DomainRuleException>(() =>
            comment.Update("Changed answer", now.AddMinutes(2)));
        Assert.Equal("COMMENT.NOT_EDITABLE", exception.Code);
    }

    [Fact]
    public void AuthorCanRemoveContentAfterEditWindowCloses()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DiscussionThread thread = DiscussionThread.Create(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            null,
            Guid.CreateVersion7(),
            "Course question",
            "This remains removable by its author.",
            now);
        DiscussionComment comment = DiscussionComment.Create(
            thread.Id,
            null,
            -1,
            Guid.CreateVersion7(),
            "Course answer",
            now);

        DateTimeOffset afterWindow = now.AddMinutes(DiscussionThread.EditWindowMinutes + 1);
        thread.Remove(afterWindow);
        comment.Remove(afterWindow);

        Assert.Equal(DiscussionStatus.Removed, thread.Status);
        Assert.Equal(DiscussionCommentStatus.Removed, comment.Status);
    }
}
