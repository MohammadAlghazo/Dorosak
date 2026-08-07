using Dorosak.Domain.Operations;

namespace Dorosak.Domain.UnitTests.Operations;

public sealed class OutboxMessageTests
{
    private static readonly DateTimeOffset OccurredAt = new(2026, 8, 6, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_InitializesPendingMessage()
    {
        DateTimeOffset availableAt = OccurredAt.AddMinutes(5);

        OutboxMessage message = OutboxMessage.Create(
            "course.published",
            1,
            "{\"courseId\":\"course-1\"}",
            "{\"correlationId\":\"correlation-1\"}",
            OccurredAt,
            availableAt);

        Assert.Equal(7, message.Id.Version);
        Assert.Equal(OccurredAt, message.OccurredAt);
        Assert.Equal(availableAt, message.AvailableAt);
        Assert.Equal("course.published", message.EventType);
        Assert.Equal(1, message.SchemaVersion);
        Assert.Equal(0, message.AttemptCount);
        Assert.Null(message.ProcessedAt);
        Assert.Null(message.LockToken);
    }

    [Theory]
    [InlineData("{", "{}")]
    [InlineData("{}", "not-json")]
    public void Create_RejectsInvalidJson(string payload, string headers)
    {
        Assert.Throws<ArgumentException>(() =>
            OutboxMessage.Create("event", 1, payload, headers, OccurredAt));
    }

    [Fact]
    public void Create_RejectsNonUtcTimestamps()
    {
        var nonUtc = new DateTimeOffset(2026, 8, 6, 12, 0, 0, TimeSpan.FromHours(2));

        Assert.Throws<ArgumentException>(() =>
            OutboxMessage.Create("event", 1, "{}", "{}", nonUtc));
        Assert.Throws<ArgumentException>(() =>
            OutboxMessage.Create("event", 1, "{}", "{}", OccurredAt, nonUtc));
    }

    [Fact]
    public void Create_RejectsAvailabilityBeforeOccurrence()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            OutboxMessage.Create("event", 1, "{}", "{}", OccurredAt, OccurredAt.AddTicks(-1)));
    }

    [Fact]
    public void WorkerLock_RequiresOwnerToCompleteOrRelease()
    {
        OutboxMessage message = OutboxMessage.Create("identity.email", 1, "{}", "{}", OccurredAt);
        Guid lockToken = Guid.CreateVersion7();

        Assert.True(message.TryAcquire(OccurredAt, TimeSpan.FromMinutes(1), lockToken));
        Assert.Equal(1, message.AttemptCount);
        Assert.False(message.TryAcquire(OccurredAt.AddSeconds(1), TimeSpan.FromMinutes(1), Guid.CreateVersion7()));
        Assert.Throws<InvalidOperationException>(() =>
            message.MarkProcessed(OccurredAt.AddSeconds(2), Guid.CreateVersion7()));

        message.MarkProcessed(OccurredAt.AddSeconds(2), lockToken);

        Assert.Equal(OccurredAt.AddSeconds(2), message.ProcessedAt);
        Assert.Null(message.LockToken);
    }

    [Fact]
    public void WorkerFailure_ReleasesMessageWithBackoff()
    {
        OutboxMessage message = OutboxMessage.Create("identity.email", 1, "{}", "{}", OccurredAt);
        Guid lockToken = Guid.CreateVersion7();
        Assert.True(message.TryAcquire(OccurredAt, TimeSpan.FromMinutes(1), lockToken));

        message.ReleaseAfterFailure(OccurredAt, lockToken, "SMTP.UNAVAILABLE", TimeSpan.FromSeconds(30));

        Assert.Equal(OccurredAt.AddSeconds(30), message.AvailableAt);
        Assert.Equal("SMTP.UNAVAILABLE", message.LastErrorCode);
        Assert.Null(message.LockToken);
    }
}
