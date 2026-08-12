namespace Dorosak.Domain.Credentials;

public enum CertificateStatus
{
    Active,
    Revoked,
}

public sealed class Certificate
{
    private Certificate()
    {
    }

    private Certificate(
        Guid id,
        Guid completionEnrollmentId,
        Guid learnerUserId,
        Guid courseId,
        Guid releaseId,
        string learnerName,
        string courseTitle,
        string locale,
        DateTimeOffset completedAt,
        string verificationCode,
        DateTimeOffset issuedAt)
    {
        Id = id;
        CompletionEnrollmentId = completionEnrollmentId;
        LearnerUserId = learnerUserId;
        CourseId = courseId;
        ReleaseId = releaseId;
        LearnerName = NormalizeRequired(learnerName, nameof(learnerName));
        CourseTitle = NormalizeRequired(courseTitle, nameof(courseTitle));
        Locale = NormalizeLocale(locale);
        CompletedAt = completedAt;
        VerificationCode = NormalizeRequired(verificationCode, nameof(verificationCode));
        IssuedAt = issuedAt;
        Status = CertificateStatus.Active;
    }

    public Guid Id { get; private set; }

    public Guid CompletionEnrollmentId { get; private set; }

    public Guid LearnerUserId { get; private set; }

    public Guid CourseId { get; private set; }

    public Guid ReleaseId { get; private set; }

    public string LearnerName { get; private set; } = string.Empty;

    public string CourseTitle { get; private set; } = string.Empty;

    public string Locale { get; private set; } = string.Empty;

    public DateTimeOffset CompletedAt { get; private set; }

    public string VerificationCode { get; private set; } = string.Empty;

    public CertificateStatus Status { get; private set; }

    public DateTimeOffset IssuedAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public Guid? RevokedByUserId { get; private set; }

    public string? RevocationReason { get; private set; }

    public static Certificate Issue(
        Guid completionEnrollmentId,
        Guid learnerUserId,
        Guid courseId,
        Guid releaseId,
        string learnerName,
        string courseTitle,
        string locale,
        DateTimeOffset completedAt,
        string verificationCode,
        DateTimeOffset issuedAt)
    {
        if (completionEnrollmentId == Guid.Empty || learnerUserId == Guid.Empty || courseId == Guid.Empty ||
            releaseId == Guid.Empty)
        {
            throw new ArgumentException("Certificate identifiers are required.");
        }

        return new Certificate(
            Guid.CreateVersion7(),
            completionEnrollmentId,
            learnerUserId,
            courseId,
            releaseId,
            learnerName,
            courseTitle,
            locale,
            completedAt,
            verificationCode,
            issuedAt);
    }

    public void Revoke(Guid actorUserId, string reason, DateTimeOffset now)
    {
        if (actorUserId == Guid.Empty)
        {
            throw new ArgumentException("A revocation actor is required.", nameof(actorUserId));
        }

        string normalizedReason = NormalizeRequired(reason, nameof(reason));
        if (Status == CertificateStatus.Revoked)
        {
            return;
        }

        Status = CertificateStatus.Revoked;
        RevokedAt = now;
        RevokedByUserId = actorUserId;
        RevocationReason = normalizedReason;
    }

    private static string NormalizeLocale(string value) => value.Trim().ToLowerInvariant() switch
    {
        "ar" => "ar",
        "en" => "en",
        _ => throw new ArgumentOutOfRangeException(nameof(value), "Only ar and en are supported."),
    };

    private static string NormalizeRequired(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        return value.Trim();
    }
}
