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
}
