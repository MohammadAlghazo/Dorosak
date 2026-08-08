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
        Assert.Throws<DomainRuleException>(() => course.SubmitForReview(now.AddMinutes(3)));
    }

    [Fact]
    public void ReleaseActivationAndUnpublish_AdvanceProjectionGeneration()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Course course = Course.Create(Guid.CreateVersion7(), "en", now);
        Guid releaseId = Guid.CreateVersion7();
        course.SubmitForReview(now.AddMinutes(1));
        course.ApproveForPublication(now.AddMinutes(2));

        course.ActivateRelease(releaseId, 1, now.AddMinutes(3));

        Assert.Equal(CourseStatus.Published, course.Status);
        Assert.Equal(releaseId, course.ActiveReleaseId);
        Assert.Equal(1, course.ProjectionGeneration);

        course.StartNewDraft(now.AddMinutes(4));
        Assert.Equal(CourseStatus.Draft, course.Status);
        Assert.Equal(releaseId, course.ActiveReleaseId);

        course.Unpublish(2, now.AddMinutes(5));
        Assert.Equal(CourseStatus.Unpublished, course.Status);
        Assert.Null(course.ActiveReleaseId);
        Assert.Equal(2, course.ProjectionGeneration);
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
