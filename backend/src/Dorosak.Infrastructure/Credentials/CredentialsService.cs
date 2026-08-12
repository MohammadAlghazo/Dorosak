using System.Security.Cryptography;
using Dorosak.Application.Common.Errors;
using Dorosak.Application.Common.Results;
using Dorosak.Application.Features.Credentials;
using Dorosak.Domain.Catalog;
using Dorosak.Domain.Credentials;
using Dorosak.Domain.Learning;
using Dorosak.Domain.Operations;
using Dorosak.Infrastructure.Identity;
using Dorosak.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dorosak.Infrastructure.Credentials;

internal sealed class CredentialsService(
    DorosakDbContext dbContext,
    TimeProvider timeProvider) : ICredentialsService
{
    public async Task<Result<IReadOnlyList<CertificateResponse>>> GetMyCertificatesAsync(
        GetMyCertificatesQuery request,
        CancellationToken cancellationToken)
    {
        Certificate[] rows = await dbContext.Certificates.AsNoTracking()
            .Where(certificate => certificate.LearnerUserId == request.UserId)
            .OrderByDescending(certificate => certificate.IssuedAt)
            .ThenByDescending(certificate => certificate.Id)
            .ToArrayAsync(cancellationToken);
        CertificateResponse[] certificates = rows.Select(Map).ToArray();
        return Result.Success<IReadOnlyList<CertificateResponse>>(certificates);
    }

    public async Task<Result<CertificateResponse>> GetMyCertificateAsync(
        GetMyCertificateQuery request,
        CancellationToken cancellationToken)
    {
        Certificate? certificate = await dbContext.Certificates.AsNoTracking().SingleOrDefaultAsync(
            item => item.Id == request.CertificateId && item.LearnerUserId == request.UserId,
            cancellationToken);
        return certificate is null
            ? NotFound<CertificateResponse>()
            : Result.Success(Map(certificate));
    }

    public async Task<Result<PublicCertificateResponse>> VerifyCertificateAsync(
        VerifyCertificateQuery request,
        CancellationToken cancellationToken)
    {
        Certificate? certificate = await dbContext.Certificates.AsNoTracking().SingleOrDefaultAsync(
            item => item.VerificationCode == request.VerificationCode,
            cancellationToken);
        return certificate is null
            ? NotFound<PublicCertificateResponse>()
            : Result.Success(MapPublic(certificate));
    }

    public async Task<Result<CertificateResponse>> IssueCertificateFromCompletionAsync(
        IssueCertificateFromCompletionCommand request,
        CancellationToken cancellationToken)
    {
        Certificate? existing = await dbContext.Certificates.SingleOrDefaultAsync(
            certificate => certificate.CompletionEnrollmentId == request.CompletionEnrollmentId,
            cancellationToken);
        if (existing is not null)
        {
            return Result.Success(Map(existing));
        }

        var completion = await (
            from item in dbContext.CourseCompletions.AsNoTracking()
            join enrollment in dbContext.Enrollments.AsNoTracking() on item.EnrollmentId equals enrollment.Id
            join user in dbContext.Users.AsNoTracking() on enrollment.UserId equals user.Id
            join release in dbContext.CourseReleases.AsNoTracking() on item.ReleaseId equals release.Id
            where item.EnrollmentId == request.CompletionEnrollmentId &&
                enrollment.UserId == user.Id &&
                item.CourseId == enrollment.CourseId &&
                item.ReleaseId == enrollment.ReleaseId
            select new
            {
                Completion = item,
                LearnerUserId = enrollment.UserId,
                LearnerName = user.DisplayName,
                release.DefaultLocale,
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (completion is null)
        {
            return NotFound<CertificateResponse>();
        }

        CourseReleaseLocalization? localization = await dbContext.CourseReleaseLocalizations.AsNoTracking()
            .Where(item => item.ReleaseId == completion.Completion.ReleaseId)
            .OrderBy(item => item.Locale == completion.DefaultLocale ? 0 : 1)
            .ThenBy(item => item.Locale)
            .FirstOrDefaultAsync(cancellationToken);
        if (localization is null)
        {
            return Result.Failure<CertificateResponse>(ResultError.BusinessRule(
                "CERTIFICATE.RELEASE_LOCALIZATION_MISSING",
                "The completed release does not have a certificate title."));
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        Certificate certificate = Certificate.Issue(
            completion.Completion.EnrollmentId,
            completion.LearnerUserId,
            completion.Completion.CourseId,
            completion.Completion.ReleaseId,
            completion.LearnerName,
            localization.Title,
            localization.Locale,
            completion.Completion.CompletedAt,
            CreateVerificationCode(),
            now);
        dbContext.Certificates.Add(certificate);
        dbContext.AuditLogs.Add(AuditLog.Create(
            completion.LearnerUserId,
            "credentials.certificate-issued",
            "Certificate",
            certificate.Id,
            "Succeeded",
            completion.Completion.EnrollmentId.ToString("D"),
            now));
        return Result.Success(Map(certificate));
    }

    public async Task<Result<CertificateResponse>> RevokeCertificateAsync(
        RevokeCertificateCommand request,
        CancellationToken cancellationToken)
    {
        Certificate? certificate = await dbContext.Certificates.SingleOrDefaultAsync(
            item => item.Id == request.CertificateId,
            cancellationToken);
        if (certificate is null)
        {
            return NotFound<CertificateResponse>();
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        certificate.Revoke(request.ActorUserId, request.Reason, now);
        dbContext.AuditLogs.Add(AuditLog.Create(
            request.ActorUserId,
            "credentials.certificate-revoked",
            "Certificate",
            certificate.Id,
            "Succeeded",
            request.Reason,
            now));
        return Result.Success(Map(certificate));
    }

    private static CertificateResponse Map(Certificate certificate) => new(
        certificate.Id,
        certificate.LearnerName,
        certificate.CourseTitle,
        certificate.Locale,
        certificate.CompletedAt,
        certificate.IssuedAt,
        certificate.VerificationCode,
        certificate.Status.ToString(),
        certificate.RevokedAt);

    private static PublicCertificateResponse MapPublic(Certificate certificate) => new(
        certificate.LearnerName,
        certificate.CourseTitle,
        certificate.Locale,
        certificate.CompletedAt,
        certificate.IssuedAt,
        certificate.VerificationCode,
        certificate.Status.ToString(),
        certificate.RevokedAt);

    private static string CreateVerificationCode() =>
        Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(24));

    private static Result<T> NotFound<T>() => Result.Failure<T>(ResultError.NotFound(
        "CERTIFICATE.NOT_FOUND", "The certificate was not found."));
}
