namespace Dorosak.Application.Features.Credentials;

public sealed record CertificateResponse(
    Guid Id,
    string LearnerName,
    string CourseTitle,
    string Locale,
    DateTimeOffset CompletedAt,
    DateTimeOffset IssuedAt,
    string VerificationCode,
    string Status,
    DateTimeOffset? RevokedAt);

public sealed record PublicCertificateResponse(
    string LearnerName,
    string CourseTitle,
    string Locale,
    DateTimeOffset CompletedAt,
    DateTimeOffset IssuedAt,
    string VerificationCode,
    string Status,
    DateTimeOffset? RevokedAt);
