using Dorosak.Domain.Credentials;
using Dorosak.Domain.Learning;
using Dorosak.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dorosak.Infrastructure.Persistence.Configurations;

internal sealed class CertificateConfiguration : IEntityTypeConfiguration<Certificate>
{
    public void Configure(EntityTypeBuilder<Certificate> builder)
    {
        builder.ToTable("certificates", "credentials", table =>
        {
            table.HasCheckConstraint("ck_certificates_locale", "locale IN ('ar', 'en')");
            table.HasCheckConstraint("ck_certificates_status", "status IN ('Active', 'Revoked')");
            table.HasCheckConstraint(
                "ck_certificates_revocation",
                "(status = 'Active' AND revoked_at IS NULL AND revoked_by_user_id IS NULL AND revocation_reason IS NULL) OR " +
                "(status = 'Revoked' AND revoked_at IS NOT NULL AND revoked_by_user_id IS NOT NULL AND revocation_reason IS NOT NULL)");
        });
        builder.HasKey(certificate => certificate.Id).HasName("pk_certificates");
        builder.Property(certificate => certificate.Id).ValueGeneratedNever();
        builder.Property(certificate => certificate.LearnerName).HasMaxLength(100).IsRequired();
        builder.Property(certificate => certificate.CourseTitle).HasMaxLength(200).IsRequired();
        builder.Property(certificate => certificate.Locale).HasMaxLength(10).IsRequired();
        builder.Property(certificate => certificate.VerificationCode).HasMaxLength(64).IsRequired();
        builder.Property(certificate => certificate.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(certificate => certificate.RevocationReason).HasMaxLength(1000);
        builder.HasIndex(certificate => certificate.CompletionEnrollmentId).IsUnique()
            .HasDatabaseName("uq_certificates_completion_enrollment_id");
        builder.HasIndex(certificate => certificate.VerificationCode).IsUnique()
            .HasDatabaseName("uq_certificates_verification_code");
        builder.HasIndex(certificate => new { certificate.LearnerUserId, certificate.IssuedAt, certificate.Id }).IsDescending()
            .HasDatabaseName("ix_certificates_learner_issued_id");
        builder.HasOne<CourseCompletion>().WithOne()
            .HasForeignKey<Certificate>(certificate => certificate.CompletionEnrollmentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(certificate => certificate.LearnerUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(certificate => certificate.RevokedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
