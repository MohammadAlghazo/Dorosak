using Dorosak.Domain.Engagement;

namespace Dorosak.Domain.UnitTests.Engagement;

public sealed class CourseReviewTests
{
    [Fact]
    public void ReviewRequiresRatingBetweenOneAndFive()
    {
        Assert.Throws<Domain.Common.DomainRuleException>(() => CourseReview.Create(
            Guid.CreateVersion7(), Guid.CreateVersion7(), 0, null, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void RemovedReviewCannotBeUpdated()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        CourseReview review = CourseReview.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), 5, "Useful", now);
        review.Remove(now.AddMinutes(1));

        Assert.Throws<Domain.Common.DomainRuleException>(() => review.Update(4, "Changed", now.AddMinutes(2)));
    }

    [Fact]
    public void ModerationCanHideAndRestoreReview()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        CourseReview review = CourseReview.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), 5, "Useful", now);

        Assert.True(review.Hide(now.AddMinutes(1)));
        Assert.False(review.Hide(now.AddMinutes(2)));
        Assert.Throws<Domain.Common.DomainRuleException>(() => review.Update(4, "Changed", now.AddMinutes(2)));
        Assert.True(review.Restore(now.AddMinutes(3)));

        Assert.Equal(CourseReviewStatus.Published, review.Status);
    }
}
