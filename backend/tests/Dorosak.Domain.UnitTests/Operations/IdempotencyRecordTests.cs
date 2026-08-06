using Dorosak.Domain.Operations;

namespace Dorosak.Domain.UnitTests.Operations;

public sealed class IdempotencyRecordTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 8, 6, 12, 0, 0, TimeSpan.Zero);
    private static readonly string RequestHash = new('a', 64);

    [Fact]
    public void CreateCompleted_InitializesCompletedRecord()
    {
        DateTimeOffset expiresAt = CreatedAt.AddHours(24);

        IdempotencyRecord record = IdempotencyRecord.CreateCompleted(
            "user-1",
            "CreateEnrollment",
            "key-1",
            RequestHash,
            "{\"enrollmentId\":\"enrollment-1\"}",
            1,
            CreatedAt,
            expiresAt);

        Assert.Equal(7, record.Id.Version);
        Assert.Equal(IdempotencyStatus.Completed, record.Status);
        Assert.Equal(CreatedAt, record.CreatedAt);
        Assert.Equal(CreatedAt, record.CompletedAt);
        Assert.Equal(expiresAt, record.ExpiresAt);
        Assert.Equal(1, record.ResponseSchemaVersion);
    }

    [Fact]
    public void CreateCompleted_RejectsInvalidHashAndPayload()
    {
        Assert.Throws<ArgumentException>(() => Create(requestHash: "invalid"));
        Assert.Throws<ArgumentException>(() => Create(responsePayload: "{"));
    }

    [Fact]
    public void CreateCompleted_RejectsNonUtcAndInvalidExpiration()
    {
        var nonUtc = new DateTimeOffset(2026, 8, 6, 12, 0, 0, TimeSpan.FromHours(1));

        Assert.Throws<ArgumentException>(() => Create(createdAt: nonUtc, expiresAt: nonUtc.AddHours(1)));
        Assert.Throws<ArgumentException>(() => Create(expiresAt: nonUtc));
        Assert.Throws<ArgumentOutOfRangeException>(() => Create(expiresAt: CreatedAt));
    }

    private static IdempotencyRecord Create(
        string? requestHash = null,
        string? responsePayload = null,
        DateTimeOffset? createdAt = null,
        DateTimeOffset? expiresAt = null) =>
        IdempotencyRecord.CreateCompleted(
            "scope",
            "operation",
            "key",
            requestHash ?? RequestHash,
            responsePayload ?? "{}",
            1,
            createdAt ?? CreatedAt,
            expiresAt ?? CreatedAt.AddHours(1));
}
