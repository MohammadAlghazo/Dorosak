namespace Dorosak.Domain.Profiles;

public sealed class TeacherProfile
{
    private TeacherProfile()
    {
    }

    private TeacherProfile(
        Guid userId,
        Guid applicationId,
        string headline,
        string biography,
        string expertise,
        Guid approvedByUserId,
        DateTimeOffset now)
    {
        UserId = userId;
        ApplicationId = applicationId;
        Headline = headline;
        Biography = biography;
        Expertise = expertise;
        ApprovedByUserId = approvedByUserId;
        ApprovedAt = now;
        UpdatedAt = now;
    }

    public Guid UserId { get; private set; }

    public Guid ApplicationId { get; private set; }

    public string Headline { get; private set; } = string.Empty;

    public string Biography { get; private set; } = string.Empty;

    public string Expertise { get; private set; } = string.Empty;

    public Guid ApprovedByUserId { get; private set; }

    public DateTimeOffset ApprovedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static TeacherProfile Create(
        TeacherApplication application,
        Guid approvedByUserId,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(application);
        if (application.Status != TeacherApplicationStatus.Approved)
        {
            throw new InvalidOperationException("Only an approved application can create a teacher profile.");
        }

        return new TeacherProfile(
            application.UserId,
            application.Id,
            application.Headline,
            application.Biography,
            application.Expertise,
            approvedByUserId,
            now);
    }
}
