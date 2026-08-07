using Dorosak.Domain.Common;

namespace Dorosak.Domain.Catalog;

public enum CourseStatus
{
    Draft,
    InReview,
    ChangesRequested,
    ReadyToPublish,
    Archived,
}

public enum CourseCollaboratorRole
{
    Editor,
    CoInstructor,
    Reviewer,
}

public sealed class Course
{
    private Course()
    {
    }

    private Course(Guid id, Guid ownerUserId, string defaultLocale, DateTimeOffset now)
    {
        Id = id;
        OwnerUserId = ownerUserId;
        DefaultLocale = NormalizeLocale(defaultLocale);
        Status = CourseStatus.Draft;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }

    public Guid OwnerUserId { get; private set; }

    public string DefaultLocale { get; private set; } = string.Empty;

    public CourseStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public Guid? DeletedByUserId { get; private set; }

    public string? DeletionReason { get; private set; }

    public static Course Create(Guid ownerUserId, string defaultLocale, DateTimeOffset now) =>
        new(Guid.CreateVersion7(), ownerUserId, defaultLocale, now);

    public void SubmitForReview(DateTimeOffset now)
    {
        if (Status is not (CourseStatus.Draft or CourseStatus.ChangesRequested) || DeletedAt is not null)
        {
            InvalidTransition("submitted for review");
        }

        Status = CourseStatus.InReview;
        UpdatedAt = now;
    }

    public void WithdrawReview(DateTimeOffset now)
    {
        EnsureStatus(CourseStatus.InReview);
        Status = CourseStatus.Draft;
        UpdatedAt = now;
    }

    public void RequestChanges(DateTimeOffset now)
    {
        EnsureStatus(CourseStatus.InReview);
        Status = CourseStatus.ChangesRequested;
        UpdatedAt = now;
    }

    public void ApproveForPublication(DateTimeOffset now)
    {
        EnsureStatus(CourseStatus.InReview);
        Status = CourseStatus.ReadyToPublish;
        UpdatedAt = now;
    }

    public void Archive(Guid actorUserId, string reason, DateTimeOffset now)
    {
        if (Status is CourseStatus.ReadyToPublish or CourseStatus.Archived)
        {
            InvalidTransition("archived");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        Status = CourseStatus.Archived;
        DeletedAt = now;
        DeletedByUserId = actorUserId;
        DeletionReason = reason.Trim();
        UpdatedAt = now;
    }

    public void TransferOwnership(Guid newOwnerUserId, DateTimeOffset now)
    {
        if (newOwnerUserId == OwnerUserId)
        {
            throw new DomainRuleException("COURSE.OWNER_UNCHANGED", "The new owner must be different.");
        }

        OwnerUserId = newOwnerUserId;
        UpdatedAt = now;
    }

    public void ChangeDefaultLocale(string locale, DateTimeOffset now)
    {
        DefaultLocale = NormalizeLocale(locale);
        UpdatedAt = now;
    }

    private void EnsureStatus(CourseStatus expected)
    {
        if (Status != expected)
        {
            InvalidTransition($"transitioned from {expected}");
        }
    }

    private void InvalidTransition(string operation) => throw new DomainRuleException(
        "COURSE.INVALID_TRANSITION",
        $"A course in {Status} cannot be {operation}.");

    private static string NormalizeLocale(string locale) => locale.Trim().ToLowerInvariant() switch
    {
        "ar" => "ar",
        "en" => "en",
        _ => throw new ArgumentOutOfRangeException(nameof(locale), "Only ar and en are supported."),
    };
}

public sealed class CourseInstructor
{
    private CourseInstructor()
    {
    }

    private CourseInstructor(Guid courseId, Guid userId, CourseCollaboratorRole role, DateTimeOffset now)
    {
        CourseId = courseId;
        UserId = userId;
        Role = role;
        AddedAt = now;
    }

    public Guid CourseId { get; private set; }

    public Guid UserId { get; private set; }

    public CourseCollaboratorRole Role { get; private set; }

    public DateTimeOffset AddedAt { get; private set; }

    public static CourseInstructor Create(
        Guid courseId,
        Guid userId,
        CourseCollaboratorRole role,
        DateTimeOffset now) => new(courseId, userId, role, now);

    public void ChangeRole(CourseCollaboratorRole role) => Role = role;
}
