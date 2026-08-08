using Dorosak.Domain.Learning;

namespace Dorosak.Domain.UnitTests.Learning;

public sealed class LearningProgressTests
{
    [Fact]
    public void VideoCompletion_RequiresNinetyPercentCoverageAndExplicitIntent()
    {
        LessonProgress progress = LessonProgress.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), DateTimeOffset.UtcNow);
        Guid firstCommand = Guid.CreateVersion7();

        Assert.True(progress.Apply(
            firstCommand,
            1,
            90,
            [new WatchedInterval(0, 89)],
            true,
            "Video",
            100,
            DateTimeOffset.UtcNow));
        Assert.False(progress.IsCompleted);

        Assert.True(progress.Apply(
            Guid.CreateVersion7(),
            2,
            90,
            [new WatchedInterval(89, 90)],
            true,
            "Video",
            100,
            DateTimeOffset.UtcNow));
        Assert.True(progress.IsCompleted);
    }

    [Fact]
    public void StaleSequenceAndDuplicateClientCommand_DoNotRevertProgress()
    {
        LessonProgress progress = LessonProgress.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), DateTimeOffset.UtcNow);
        Guid commandId = Guid.CreateVersion7();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        Assert.True(progress.Apply(commandId, 2, 80, [new WatchedInterval(0, 80)], false, "Video", 100, now));
        Assert.False(progress.Apply(commandId, 3, 1, [new WatchedInterval(0, 1)], false, "Video", 100, now));
        Assert.False(progress.Apply(Guid.CreateVersion7(), 1, 1, [new WatchedInterval(0, 1)], false, "Video", 100, now));
        Assert.Equal(2, progress.LastSequence);
        Assert.Equal(80, progress.PositionSeconds);
    }

    [Fact]
    public void ArticleCompletion_RequiresExplicitIntent()
    {
        LessonProgress progress = LessonProgress.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), DateTimeOffset.UtcNow);

        Assert.True(progress.Apply(Guid.CreateVersion7(), 1, 0, [], false, "Article", null, DateTimeOffset.UtcNow));
        Assert.False(progress.IsCompleted);
        Assert.True(progress.Apply(Guid.CreateVersion7(), 2, 0, [], true, "Article", null, DateTimeOffset.UtcNow));
        Assert.True(progress.IsCompleted);
    }
}
