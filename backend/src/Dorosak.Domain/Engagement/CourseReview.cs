using Dorosak.Domain.Common;

namespace Dorosak.Domain.Engagement;

public enum CourseReviewStatus
{
    Published,
    Hidden,
    Removed,
}

public sealed class CourseReview
{
    private CourseReview()
    {
    }

    private CourseReview(
        Guid id,
        Guid userId,
        Guid courseId,
        short rating,
        string text,
        DateTimeOffset now)
    {
        Id = id;
        UserId = userId;
        CourseId = courseId;
        Rating = rating;
        Text = text;
        Status = CourseReviewStatus.Published;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public Guid CourseId { get; private set; }

    public short Rating { get; private set; }

    public string Text { get; private set; } = string.Empty;

    public CourseReviewStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? RemovedAt { get; private set; }

    public static CourseReview Create(
        Guid userId,
        Guid courseId,
        short rating,
        string? text,
        DateTimeOffset now)
    {
        Validate(rating, text);
        return new CourseReview(Guid.CreateVersion7(), userId, courseId, rating, NormalizeText(text), now);
    }

    public void Update(short rating, string? text, DateTimeOffset now)
    {
        Validate(rating, text);
        if (Status != CourseReviewStatus.Published)
        {
            throw new DomainRuleException("REVIEW.NOT_EDITABLE", "A non-published review cannot be edited.");
        }

        Rating = rating;
        Text = NormalizeText(text);
        UpdatedAt = now;
    }

    public bool Remove(DateTimeOffset now)
    {
        if (Status == CourseReviewStatus.Removed)
        {
            return false;
        }
        if (Status != CourseReviewStatus.Published)
        {
            throw new DomainRuleException("REVIEW.NOT_REMOVABLE", "A moderated review cannot be removed by its author.");
        }

        Status = CourseReviewStatus.Removed;
        RemovedAt = now;
        UpdatedAt = now;
        return true;
    }

    public void Republish(short rating, string? text, DateTimeOffset now)
    {
        Validate(rating, text);
        if (Status != CourseReviewStatus.Removed)
        {
            throw new DomainRuleException("REVIEW.NOT_REMOVED", "Only a removed review can be republished.");
        }

        Rating = rating;
        Text = NormalizeText(text);
        Status = CourseReviewStatus.Published;
        RemovedAt = null;
        UpdatedAt = now;
    }

    public bool Hide(DateTimeOffset now)
    {
        if (Status == CourseReviewStatus.Hidden)
        {
            return false;
        }
        if (Status != CourseReviewStatus.Published)
        {
            throw new DomainRuleException("REVIEW.NOT_HIDEABLE", "Only a published review can be hidden.");
        }

        Status = CourseReviewStatus.Hidden;
        UpdatedAt = now;
        return true;
    }

    public bool Restore(DateTimeOffset now)
    {
        if (Status == CourseReviewStatus.Published)
        {
            return false;
        }
        if (Status != CourseReviewStatus.Hidden)
        {
            throw new DomainRuleException("REVIEW.NOT_RESTORABLE", "Only a hidden review can be restored.");
        }

        Status = CourseReviewStatus.Published;
        UpdatedAt = now;
        return true;
    }

    private static void Validate(short rating, string? text)
    {
        if (rating is < 1 or > 5)
        {
            throw new DomainRuleException("REVIEW.RATING_INVALID", "A course rating must be between one and five.");
        }
        if (text?.Length > 4000)
        {
            throw new DomainRuleException("REVIEW.TEXT_TOO_LONG", "A course review cannot exceed 4000 characters.");
        }
    }

    private static string NormalizeText(string? text) => text?.Trim() ?? string.Empty;
}
