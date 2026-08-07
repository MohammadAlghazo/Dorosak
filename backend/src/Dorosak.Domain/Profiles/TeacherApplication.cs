using Dorosak.Domain.Common;

namespace Dorosak.Domain.Profiles;

public enum TeacherApplicationStatus
{
    Pending,
    InReview,
    Approved,
    Rejected,
    Withdrawn,
}

public sealed class TeacherApplication
{
    private TeacherApplication()
    {
    }

    private TeacherApplication(
        Guid id,
        Guid userId,
        string headline,
        string biography,
        string expertise,
        string motivation,
        DateTimeOffset now)
    {
        Id = id;
        UserId = userId;
        Headline = headline;
        Biography = biography;
        Expertise = expertise;
        Motivation = motivation;
        Status = TeacherApplicationStatus.Pending;
        SubmittedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string Headline { get; private set; } = string.Empty;

    public string Biography { get; private set; } = string.Empty;

    public string Expertise { get; private set; } = string.Empty;

    public string Motivation { get; private set; } = string.Empty;

    public TeacherApplicationStatus Status { get; private set; }

    public Guid? ReviewerUserId { get; private set; }

    public string? ReviewerReason { get; private set; }

    public DateTimeOffset SubmittedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? ReviewedAt { get; private set; }

    public static TeacherApplication Create(
        Guid userId,
        string headline,
        string biography,
        string expertise,
        string motivation,
        DateTimeOffset now)
    {
        ValidateText(headline, nameof(headline));
        ValidateText(biography, nameof(biography));
        ValidateText(expertise, nameof(expertise));
        ValidateText(motivation, nameof(motivation));
        return new TeacherApplication(
            Guid.CreateVersion7(),
            userId,
            headline.Trim(),
            biography.Trim(),
            expertise.Trim(),
            motivation.Trim(),
            now);
    }

    public void StartReview(Guid reviewerUserId, DateTimeOffset now)
    {
        EnsureStatus(TeacherApplicationStatus.Pending);
        Status = TeacherApplicationStatus.InReview;
        ReviewerUserId = reviewerUserId;
        UpdatedAt = now;
    }

    public void Approve(Guid reviewerUserId, DateTimeOffset now)
    {
        EnsureStatus(TeacherApplicationStatus.InReview);
        Status = TeacherApplicationStatus.Approved;
        ReviewerUserId = reviewerUserId;
        ReviewerReason = null;
        ReviewedAt = now;
        UpdatedAt = now;
    }

    public void Reject(Guid reviewerUserId, string reason, DateTimeOffset now)
    {
        EnsureStatus(TeacherApplicationStatus.InReview);
        ValidateText(reason, nameof(reason));
        Status = TeacherApplicationStatus.Rejected;
        ReviewerUserId = reviewerUserId;
        ReviewerReason = reason.Trim();
        ReviewedAt = now;
        UpdatedAt = now;
    }

    public void Withdraw(DateTimeOffset now)
    {
        if (Status is not (TeacherApplicationStatus.Pending or TeacherApplicationStatus.InReview))
        {
            throw new DomainRuleException(
                "TEACHER_APPLICATION.INVALID_TRANSITION",
                $"A {Status} teacher application cannot be withdrawn.");
        }

        Status = TeacherApplicationStatus.Withdrawn;
        UpdatedAt = now;
    }

    private void EnsureStatus(TeacherApplicationStatus expected)
    {
        if (Status != expected)
        {
            throw new DomainRuleException(
                "TEACHER_APPLICATION.INVALID_TRANSITION",
                $"The teacher application must be {expected} for this transition.");
        }
    }

    private static void ValidateText(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A non-empty value is required.", parameterName);
        }
    }
}
