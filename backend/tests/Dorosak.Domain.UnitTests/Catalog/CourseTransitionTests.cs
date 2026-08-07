using Dorosak.Domain.Catalog;
using Dorosak.Domain.Common;

namespace Dorosak.Domain.UnitTests.Catalog;

public sealed class CourseTransitionTests
{
    [Fact]
    public void Approval_StopsAtReadyToPublish()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Course course = Course.Create(Guid.CreateVersion7(), "ar", now);

        course.SubmitForReview(now.AddMinutes(1));
        course.ApproveForPublication(now.AddMinutes(2));

        Assert.Equal(CourseStatus.ReadyToPublish, course.Status);
        Assert.DoesNotContain("Published", Enum.GetNames<CourseStatus>(), StringComparer.Ordinal);
        Assert.Throws<DomainRuleException>(() => course.SubmitForReview(now.AddMinutes(3)));
    }

    [Fact]
    public void ChangesRequested_CanBeResubmitted()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Course course = Course.Create(Guid.CreateVersion7(), "en", now);

        course.SubmitForReview(now.AddMinutes(1));
        course.RequestChanges(now.AddMinutes(2));
        course.SubmitForReview(now.AddMinutes(3));

        Assert.Equal(CourseStatus.InReview, course.Status);
    }
}
