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
}
