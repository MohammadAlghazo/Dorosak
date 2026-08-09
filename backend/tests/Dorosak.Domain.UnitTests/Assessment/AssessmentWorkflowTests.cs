using Dorosak.Domain.Assessment;
using Dorosak.Domain.Common;

namespace Dorosak.Domain.UnitTests.Assessment;

public sealed class AssessmentWorkflowTests
{
    [Fact]
    public void QuizVersion_CannotBecomeReadyWithoutQuestions()
    {
        QuizVersion version = QuizVersion.Create(
            Guid.CreateVersion7(),
            1,
            "Security checkpoint",
            2,
            30,
            null,
            70,
            AssessmentAudienceType.AllEnrolled,
            DateTimeOffset.UtcNow);

        DomainRuleException exception = Assert.Throws<DomainRuleException>(() => version.MarkReady(0, DateTimeOffset.UtcNow));

        Assert.Equal("QUIZ.QUESTIONS_REQUIRED", exception.Code);
    }

    [Fact]
    public void QuizAttempt_RequiresManualGradeForUnkeyedShortAnswer()
    {
        QuizAttempt attempt = QuizAttempt.Start(Guid.CreateVersion7(), Guid.CreateVersion7(), 1, DateTimeOffset.UtcNow, null);

        attempt.Submit(40, true, 70, DateTimeOffset.UtcNow);

        Assert.Equal(QuizAttemptStatus.PendingManualGrade, attempt.Status);
        Assert.Null(attempt.Passed);
        attempt.ApplyManualGrade(80, 70);
        Assert.Equal(QuizAttemptStatus.Graded, attempt.Status);
        Assert.True(attempt.Passed);
    }

    [Fact]
    public void QuizAttempt_ExpiresWithoutBecomingGradable()
    {
        DateTimeOffset started = DateTimeOffset.UtcNow;
        QuizAttempt attempt = QuizAttempt.Start(Guid.CreateVersion7(), Guid.CreateVersion7(), 1, started, 1);

        attempt.Expire(started.AddMinutes(1));

        Assert.Equal(QuizAttemptStatus.Expired, attempt.Status);
        Assert.False(attempt.Passed);
        Assert.Throws<DomainRuleException>(() => attempt.ApplyManualGrade(90, 70));
    }

    [Fact]
    public void QuizGradeRevision_IsAppendOnlyByRevisionNumber()
    {
        Guid attemptId = Guid.CreateVersion7();
        QuizGradeRevision first = QuizGradeRevision.Create(
            attemptId,
            1,
            70,
            "Initial review",
            Guid.CreateVersion7(),
            DateTimeOffset.UtcNow);
        QuizGradeRevision second = QuizGradeRevision.Create(
            attemptId,
            2,
            85,
            "Re-reviewed",
            Guid.CreateVersion7(),
            DateTimeOffset.UtcNow);

        Assert.Equal(1, first.RevisionNumber);
        Assert.Equal(2, second.RevisionNumber);
        Assert.NotEqual(first.Id, second.Id);
    }
}
