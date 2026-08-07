using Dorosak.Domain.Common;

namespace Dorosak.Domain.Authoring;

public enum PublicationReviewStatus
{
    Pending,
    ChangesRequested,
    Approved,
    Withdrawn,
}

public sealed class PublicationReview
{
    private PublicationReview()
    {
    }

    private PublicationReview(Guid id, Guid courseId, Guid draftId, long draftVersion, Guid requestedByUserId, DateTimeOffset now)
    {
        Id = id;
        CourseId = courseId;
        DraftId = draftId;
        DraftVersion = draftVersion;
        RequestedByUserId = requestedByUserId;
        Status = PublicationReviewStatus.Pending;
        RequestedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }

    public Guid CourseId { get; private set; }

    public Guid DraftId { get; private set; }

    public long DraftVersion { get; private set; }

    public Guid RequestedByUserId { get; private set; }

    public PublicationReviewStatus Status { get; private set; }

    public Guid? ReviewerUserId { get; private set; }

    public string? ReviewerReason { get; private set; }

    public DateTimeOffset RequestedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? DecidedAt { get; private set; }

    public static PublicationReview Create(
        Guid courseId,
        Guid draftId,
        long draftVersion,
        Guid requestedByUserId,
        DateTimeOffset now) => new(
            Guid.CreateVersion7(),
            courseId,
            draftId,
            draftVersion,
            requestedByUserId,
            now);

    public void RequestChanges(Guid reviewerUserId, string reason, DateTimeOffset now)
    {
        EnsurePending();
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        Status = PublicationReviewStatus.ChangesRequested;
        ReviewerUserId = reviewerUserId;
        ReviewerReason = reason.Trim();
        DecidedAt = now;
        UpdatedAt = now;
    }

    public void Approve(Guid reviewerUserId, DateTimeOffset now)
    {
        EnsurePending();
        Status = PublicationReviewStatus.Approved;
        ReviewerUserId = reviewerUserId;
        DecidedAt = now;
        UpdatedAt = now;
    }

    public void Withdraw(DateTimeOffset now)
    {
        EnsurePending();
        Status = PublicationReviewStatus.Withdrawn;
        UpdatedAt = now;
    }

    private void EnsurePending()
    {
        if (Status != PublicationReviewStatus.Pending)
        {
            throw new DomainRuleException(
                "PUBLICATION_REVIEW.INVALID_TRANSITION",
                "Only a pending publication review can be decided or withdrawn.");
        }
    }
}
