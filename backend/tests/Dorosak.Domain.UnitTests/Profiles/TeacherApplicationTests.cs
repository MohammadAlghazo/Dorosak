using Dorosak.Domain.Common;
using Dorosak.Domain.Profiles;

namespace Dorosak.Domain.UnitTests.Profiles;

public sealed class TeacherApplicationTests
{
    [Fact]
    public void Application_FollowsReviewApprovalTransition()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        TeacherApplication application = TeacherApplication.Create(
            Guid.CreateVersion7(),
            "Backend instructor",
            "A sufficiently detailed biography.",
            "PostgreSQL and distributed systems",
            "I want to teach practical engineering.",
            now);
        Guid reviewerId = Guid.CreateVersion7();

        application.StartReview(reviewerId, now.AddMinutes(1));
        application.Approve(reviewerId, now.AddMinutes(2));

        Assert.Equal(TeacherApplicationStatus.Approved, application.Status);
        Assert.Throws<DomainRuleException>(() => application.Withdraw(now.AddMinutes(3)));
    }

    [Fact]
    public void Rejection_RequiresInReviewAndReason()
    {
        TeacherApplication application = TeacherApplication.Create(
            Guid.CreateVersion7(),
            "Data instructor",
            "A sufficiently detailed biography.",
            "Data engineering",
            "I want to teach data engineering.",
            DateTimeOffset.UtcNow);

        Assert.Throws<DomainRuleException>(() =>
            application.Reject(Guid.CreateVersion7(), "Needs more evidence.", DateTimeOffset.UtcNow));
    }
}
