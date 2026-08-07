using Dorosak.Domain.Common;

namespace Dorosak.Domain.Authoring;

public sealed class CourseDraft
{
    private CourseDraft()
    {
    }

    private CourseDraft(Guid id, Guid courseId, string level, DateTimeOffset now)
    {
        Id = id;
        CourseId = courseId;
        Level = level;
        Version = 1;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }

    public Guid CourseId { get; private set; }

    public string Level { get; private set; } = string.Empty;

    public long Version { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static CourseDraft Create(Guid courseId, string level, DateTimeOffset now) =>
        new(Guid.CreateVersion7(), courseId, level.Trim(), now);

    public void UpdateLevel(string level, long expectedVersion, DateTimeOffset now)
    {
        Advance(expectedVersion, now);
        Level = level.Trim();
    }

    public void Advance(long expectedVersion, DateTimeOffset now)
    {
        if (Version != expectedVersion)
        {
            throw new DomainRuleException(
                "COURSE.VERSION_CONFLICT",
                "The course draft was changed by another request.");
        }

        Version++;
        UpdatedAt = now;
    }
}
