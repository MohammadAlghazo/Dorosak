using System.Globalization;
using Dorosak.Domain.Common;

namespace Dorosak.Domain.Learning;

public enum EntitlementStatus
{
    Active,
    Revoked,
    Expired,
}

public enum EnrollmentStatus
{
    Active,
    Completed,
    Suspended,
    Revoked,
    Expired,
}

public sealed class Entitlement
{
    private Entitlement()
    {
    }

    private Entitlement(Guid id, Guid userId, Guid courseId, DateTimeOffset grantedAt)
    {
        Id = id;
        UserId = userId;
        CourseId = courseId;
        Source = "Free";
        Status = EntitlementStatus.Active;
        GrantedAt = grantedAt;
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid CourseId { get; private set; }
    public string Source { get; private set; } = string.Empty;
    public EntitlementStatus Status { get; private set; }
    public DateTimeOffset GrantedAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public DateTimeOffset? ExpiresAt { get; private set; }

    public static Entitlement GrantFree(Guid userId, Guid courseId, DateTimeOffset now) =>
        new(Guid.CreateVersion7(), userId, courseId, now);

    public static Entitlement GrantDemo(Guid userId, Guid courseId, DateTimeOffset now)
    {
        Entitlement entitlement = new(Guid.CreateVersion7(), userId, courseId, now);
        entitlement.Source = "Demo";
        return entitlement;
    }

    public void Revoke(DateTimeOffset now)
    {
        if (Status == EntitlementStatus.Revoked)
        {
            return;
        }

        Status = EntitlementStatus.Revoked;
        RevokedAt = now;
    }

    public bool IsActive(DateTimeOffset now) => Status == EntitlementStatus.Active && (ExpiresAt is null || ExpiresAt > now);
}

public sealed class Enrollment
{
    private Enrollment()
    {
    }

    private Enrollment(Guid id, Guid userId, Guid courseId, Guid releaseId, Guid entitlementId, DateTimeOffset enrolledAt)
    {
        Id = id;
        UserId = userId;
        CourseId = courseId;
        ReleaseId = releaseId;
        EntitlementId = entitlementId;
        Status = EnrollmentStatus.Active;
        EnrolledAt = enrolledAt;
        LastAccessedAt = enrolledAt;
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid CourseId { get; private set; }
    public Guid ReleaseId { get; private set; }
    public Guid EntitlementId { get; private set; }
    public EnrollmentStatus Status { get; private set; }
    public DateTimeOffset EnrolledAt { get; private set; }
    public DateTimeOffset LastAccessedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }

    public static Enrollment Create(Guid userId, Guid courseId, Guid releaseId, Guid entitlementId, DateTimeOffset now) =>
        new(Guid.CreateVersion7(), userId, courseId, releaseId, entitlementId, now);

    public void Touch(DateTimeOffset now)
    {
        if (Status is EnrollmentStatus.Active or EnrollmentStatus.Completed)
        {
            LastAccessedAt = now;
        }
    }

    public void Complete(DateTimeOffset now)
    {
        if (Status == EnrollmentStatus.Completed)
        {
            return;
        }
        if (Status != EnrollmentStatus.Active)
        {
            throw new DomainRuleException("LEARNING.ENROLLMENT_INACTIVE", "An inactive enrollment cannot be completed.");
        }

        Status = EnrollmentStatus.Completed;
        CompletedAt = now;
        LastAccessedAt = now;
    }

    public void Revoke(DateTimeOffset now)
    {
        if (Status == EnrollmentStatus.Revoked)
        {
            return;
        }

        Status = EnrollmentStatus.Revoked;
        RevokedAt = now;
    }

    public bool AllowsLearning => Status is EnrollmentStatus.Active or EnrollmentStatus.Completed;
}

public readonly record struct WatchedInterval(decimal StartSeconds, decimal EndSeconds)
{
    public WatchedInterval Normalize(decimal? durationSeconds)
    {
        decimal start = Math.Max(0, StartSeconds);
        decimal end = Math.Max(start, EndSeconds);
        if (durationSeconds is > 0)
        {
            start = Math.Min(start, durationSeconds.Value);
            end = Math.Min(end, durationSeconds.Value);
        }
        return new WatchedInterval(start, end);
    }
}

public sealed class LessonProgress
{
    private LessonProgress()
    {
    }

    private LessonProgress(Guid enrollmentId, Guid lessonId, DateTimeOffset now)
    {
        EnrollmentId = enrollmentId;
        LessonId = lessonId;
        WatchedIntervals = string.Empty;
        UpdatedAt = now;
    }

    public Guid EnrollmentId { get; private set; }
    public Guid LessonId { get; private set; }
    public long LastSequence { get; private set; }
    public Guid? LastClientCommandId { get; private set; }
    public decimal PositionSeconds { get; private set; }
    public string WatchedIntervals { get; private set; } = string.Empty;
    public bool IsCompleted { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static LessonProgress Create(Guid enrollmentId, Guid lessonId, DateTimeOffset now) => new(enrollmentId, lessonId, now);

    public bool Apply(
        Guid clientCommandId,
        long sequence,
        decimal positionSeconds,
        IReadOnlyList<WatchedInterval> intervals,
        bool completionIntent,
        string lessonType,
        decimal? durationSeconds,
        DateTimeOffset now)
    {
        if (clientCommandId == Guid.Empty)
        {
            throw new DomainRuleException("PROGRESS.CLIENT_COMMAND_REQUIRED", "A client command identifier is required.");
        }
        if (LastClientCommandId == clientCommandId || sequence <= LastSequence)
        {
            return false;
        }
        if (positionSeconds < 0)
        {
            throw new DomainRuleException("PROGRESS.POSITION_INVALID", "Progress position cannot be negative.");
        }

        LastSequence = sequence;
        LastClientCommandId = clientCommandId;
        PositionSeconds = durationSeconds is > 0 ? Math.Min(positionSeconds, durationSeconds.Value) : positionSeconds;
        IReadOnlyList<WatchedInterval> merged = MergeIntervals([.. ParseIntervals(WatchedIntervals), .. intervals], durationSeconds);
        WatchedIntervals = SerializeIntervals(merged);

        bool qualifies = lessonType switch
        {
            "Video" => completionIntent && durationSeconds is > 0 && Coverage(merged) / durationSeconds.Value >= 0.9m,
            "Article" or "Document" => completionIntent,
            "Quiz" or "Assignment" => false,
            _ => completionIntent,
        };
        if (!IsCompleted && qualifies)
        {
            IsCompleted = true;
            CompletedAt = now;
        }

        UpdatedAt = now;
        return true;
    }

    public void CompleteFromAssessment(DateTimeOffset now)
    {
        if (!IsCompleted)
        {
            IsCompleted = true;
            CompletedAt = now;
        }
        UpdatedAt = now;
    }

    public static IReadOnlyList<WatchedInterval> MergeIntervals(IEnumerable<WatchedInterval> intervals, decimal? durationSeconds = null)
    {
        WatchedInterval[] ordered = intervals
            .Select(interval => interval.Normalize(durationSeconds))
            .Where(interval => interval.EndSeconds > interval.StartSeconds)
            .OrderBy(interval => interval.StartSeconds)
            .ThenBy(interval => interval.EndSeconds)
            .ToArray();
        if (ordered.Length == 0)
        {
            return [];
        }

        var merged = new List<WatchedInterval>(ordered.Length) { ordered[0] };
        foreach (WatchedInterval interval in ordered.Skip(1))
        {
            WatchedInterval current = merged[^1];
            if (interval.StartSeconds <= current.EndSeconds)
            {
                merged[^1] = current with { EndSeconds = Math.Max(current.EndSeconds, interval.EndSeconds) };
            }
            else
            {
                merged.Add(interval);
            }
        }
        return merged;
    }

    public static decimal Coverage(IEnumerable<WatchedInterval> intervals) =>
        intervals.Sum(interval => interval.EndSeconds - interval.StartSeconds);

    public static IReadOnlyList<WatchedInterval> ParseIntervals(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return [];
        }

        var result = new List<WatchedInterval>();
        foreach (string item in value.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] boundaries = item.Split(',', StringSplitOptions.TrimEntries);
            if (boundaries.Length == 2 &&
                decimal.TryParse(boundaries[0], NumberStyles.Number, CultureInfo.InvariantCulture, out decimal start) &&
                decimal.TryParse(boundaries[1], NumberStyles.Number, CultureInfo.InvariantCulture, out decimal end))
            {
                result.Add(new WatchedInterval(start, end));
            }
        }
        return result;
    }

    private static string SerializeIntervals(IEnumerable<WatchedInterval> intervals) => string.Join(
        ';',
        intervals.Select(interval => $"{interval.StartSeconds.ToString(CultureInfo.InvariantCulture)},{interval.EndSeconds.ToString(CultureInfo.InvariantCulture)}"));
}

public sealed class CourseCompletion
{
    private CourseCompletion()
    {
    }

    private CourseCompletion(Guid enrollmentId, Guid courseId, Guid releaseId, DateTimeOffset completedAt)
    {
        EnrollmentId = enrollmentId;
        CourseId = courseId;
        ReleaseId = releaseId;
        CompletedAt = completedAt;
    }

    public Guid EnrollmentId { get; private set; }
    public Guid CourseId { get; private set; }
    public Guid ReleaseId { get; private set; }
    public DateTimeOffset CompletedAt { get; private set; }

    public static CourseCompletion Create(Guid enrollmentId, Guid courseId, Guid releaseId, DateTimeOffset now) =>
        new(enrollmentId, courseId, releaseId, now);
}

public sealed class Bookmark
{
    private Bookmark()
    {
    }

    private Bookmark(Guid userId, Guid enrollmentId, Guid lessonId, DateTimeOffset createdAt)
    {
        UserId = userId;
        EnrollmentId = enrollmentId;
        LessonId = lessonId;
        CreatedAt = createdAt;
    }

    public Guid UserId { get; private set; }
    public Guid EnrollmentId { get; private set; }
    public Guid LessonId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static Bookmark Create(Guid userId, Guid enrollmentId, Guid lessonId, DateTimeOffset now) =>
        new(userId, enrollmentId, lessonId, now);
}

public sealed class LearningNote
{
    private LearningNote()
    {
    }

    private LearningNote(Guid id, Guid userId, Guid enrollmentId, Guid lessonId, string text, DateTimeOffset now)
    {
        Id = id;
        UserId = userId;
        EnrollmentId = enrollmentId;
        LessonId = lessonId;
        Text = NormalizeText(text);
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid EnrollmentId { get; private set; }
    public Guid LessonId { get; private set; }
    public string Text { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static LearningNote Create(Guid userId, Guid enrollmentId, Guid lessonId, string text, DateTimeOffset now) =>
        new(Guid.CreateVersion7(), userId, enrollmentId, lessonId, text, now);

    public void Update(string text, DateTimeOffset now)
    {
        Text = NormalizeText(text);
        UpdatedAt = now;
    }

    private static string NormalizeText(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        string value = text.Trim();
        if (value.Length > 5000)
        {
            throw new DomainRuleException("LEARNING.NOTE_TOO_LONG", "A note cannot exceed 5000 characters.");
        }
        return value;
    }
}

public sealed class RecentlyViewedLesson
{
    private RecentlyViewedLesson()
    {
    }

    private RecentlyViewedLesson(Guid userId, Guid enrollmentId, Guid lessonId, DateTimeOffset viewedAt)
    {
        UserId = userId;
        EnrollmentId = enrollmentId;
        LessonId = lessonId;
        ViewedAt = viewedAt;
    }

    public Guid UserId { get; private set; }
    public Guid EnrollmentId { get; private set; }
    public Guid LessonId { get; private set; }
    public DateTimeOffset ViewedAt { get; private set; }

    public static RecentlyViewedLesson Create(Guid userId, Guid enrollmentId, Guid lessonId, DateTimeOffset now) =>
        new(userId, enrollmentId, lessonId, now);

    public void Touch(DateTimeOffset now) => ViewedAt = now;
}
