using Dorosak.Domain.Catalog;
using Dorosak.Domain.Media;
using Dorosak.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dorosak.Infrastructure.Persistence.Configurations;

internal sealed class UploadSessionConfiguration : IEntityTypeConfiguration<UploadSession>
{
    public void Configure(EntityTypeBuilder<UploadSession> builder)
    {
        builder.ToTable("upload_sessions", "media", table =>
        {
            table.HasCheckConstraint("ck_upload_sessions_expected_bytes", "expected_bytes > 0");
            table.HasCheckConstraint("ck_upload_sessions_reserved_bytes", "reserved_bytes >= 0 AND reserved_bytes <= expected_bytes");
            table.HasCheckConstraint("ck_upload_sessions_state", "state IN ('Initiated', 'Uploading', 'Completed', 'Cancelled', 'Expired')");
        });
        builder.HasKey(session => session.Id).HasName("pk_upload_sessions");
        builder.Property(session => session.Id).ValueGeneratedNever();
        builder.Property(session => session.Purpose).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(session => session.State).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(session => session.FileName).HasMaxLength(255).IsRequired();
        builder.Property(session => session.ContentType).HasMaxLength(120).IsRequired();
        builder.Property(session => session.QuarantineObjectKey).HasMaxLength(512).IsRequired();
        builder.Property(session => session.MultipartUploadId).HasMaxLength(500);
        builder.HasIndex(session => new { session.OwnerUserId, session.CreatedAt, session.Id })
            .HasDatabaseName("ix_upload_sessions_owner_created_id");
        builder.HasIndex(session => session.ExpiresAt)
            .HasDatabaseName("ix_upload_sessions_expires_at")
            .HasFilter("state IN ('Initiated', 'Uploading')");
        builder.HasIndex(session => session.AssetId).IsUnique().HasDatabaseName("uq_upload_sessions_asset_id");
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(session => session.OwnerUserId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_upload_sessions_users_owner_user_id");
        builder.HasOne<MediaAsset>().WithMany().HasForeignKey(session => session.AssetId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_upload_sessions_media_assets_asset_id");
    }
}

internal sealed class UploadPartConfiguration : IEntityTypeConfiguration<UploadPart>
{
    public void Configure(EntityTypeBuilder<UploadPart> builder)
    {
        builder.ToTable("upload_parts", "media", table =>
        {
            table.HasCheckConstraint("ck_upload_parts_part_number", "part_number BETWEEN 1 AND 10000");
            table.HasCheckConstraint("ck_upload_parts_expected_bytes", "expected_bytes > 0");
            table.HasCheckConstraint("ck_upload_parts_sha256", "sha256 ~ '^[0-9a-f]{64}$'");
        });
        builder.HasKey(part => part.Id).HasName("pk_upload_parts");
        builder.Property(part => part.Id).ValueGeneratedNever();
        builder.Property(part => part.Sha256).HasMaxLength(64).IsRequired();
        builder.Property(part => part.ETag).HasMaxLength(512);
        builder.Property(part => part.VersionId).HasMaxLength(500);
        builder.HasIndex(part => new { part.UploadSessionId, part.PartNumber })
            .IsUnique().HasDatabaseName("uq_upload_parts_session_number");
        builder.HasIndex(part => part.UploadSessionId).HasDatabaseName("ix_upload_parts_session_id");
        builder.HasOne<UploadSession>().WithMany().HasForeignKey(part => part.UploadSessionId)
            .OnDelete(DeleteBehavior.Cascade).HasConstraintName("fk_upload_parts_upload_sessions_session_id");
    }
}

internal sealed class MediaAssetConfiguration : IEntityTypeConfiguration<MediaAsset>
{
    public void Configure(EntityTypeBuilder<MediaAsset> builder)
    {
        builder.ToTable("media_assets", "media", table =>
        {
            table.HasCheckConstraint("ck_media_assets_declared_bytes", "declared_bytes > 0");
            table.HasCheckConstraint("ck_media_assets_verified_bytes", "verified_bytes IS NULL OR verified_bytes > 0");
            table.HasCheckConstraint("ck_media_assets_declared_sha256", "declared_sha256 ~ '^[0-9a-f]{64}$'");
            table.HasCheckConstraint("ck_media_assets_state", "state IN ('Initiated', 'Uploaded', 'Scanning', 'Processing', 'Ready', 'Rejected', 'RecoveryPending', 'Deleted')");
        });
        builder.HasKey(asset => asset.Id).HasName("pk_media_assets");
        builder.Property(asset => asset.Id).ValueGeneratedNever();
        builder.Property(asset => asset.Purpose).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(asset => asset.State).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(asset => asset.FileName).HasMaxLength(255).IsRequired();
        builder.Property(asset => asset.ContentType).HasMaxLength(120).IsRequired();
        builder.Property(asset => asset.DeclaredSha256).HasMaxLength(64).IsRequired();
        builder.Property(asset => asset.VerifiedSha256).HasMaxLength(64);
        builder.Property(asset => asset.QuarantineObjectKey).HasMaxLength(512).IsRequired();
        builder.Property(asset => asset.StorageProvider).HasMaxLength(50).IsRequired();
        builder.Property(asset => asset.StorageContainer).HasMaxLength(255).IsRequired();
        builder.Property(asset => asset.QuarantineETag).HasMaxLength(512);
        builder.Property(asset => asset.QuarantineVersionId).HasMaxLength(500);
        builder.Property(asset => asset.RejectionCode).HasMaxLength(100);
        builder.HasIndex(asset => new { asset.OwnerUserId, asset.State, asset.CreatedAt, asset.Id })
            .HasDatabaseName("ix_media_assets_owner_state_created_id");
        builder.HasIndex(asset => new { asset.CourseId, asset.State })
            .HasDatabaseName("ix_media_assets_course_state");
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(asset => asset.OwnerUserId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_media_assets_users_owner_user_id");
        builder.HasOne<Course>().WithMany().HasForeignKey(asset => asset.CourseId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_media_assets_courses_course_id");
    }
}

internal sealed class MediaVariantConfiguration : IEntityTypeConfiguration<MediaVariant>
{
    public void Configure(EntityTypeBuilder<MediaVariant> builder)
    {
        builder.ToTable("media_variants", "media", table =>
        {
            table.HasCheckConstraint("ck_media_variants_bytes", "bytes > 0");
            table.HasCheckConstraint("ck_media_variants_sha256", "sha256 ~ '^[0-9a-f]{64}$'");
            table.HasCheckConstraint("ck_media_variants_dimensions", "(width IS NULL AND height IS NULL) OR (width > 0 AND height > 0)");
            table.HasCheckConstraint("ck_media_variants_duration", "duration_seconds IS NULL OR duration_seconds >= 0");
        });
        builder.HasKey(variant => variant.Id).HasName("pk_media_variants");
        builder.Property(variant => variant.Id).ValueGeneratedNever();
        builder.Property(variant => variant.Kind).HasMaxLength(80).IsRequired();
        builder.Property(variant => variant.ContentType).HasMaxLength(120).IsRequired();
        builder.Property(variant => variant.ObjectKey).HasMaxLength(512).IsRequired();
        builder.Property(variant => variant.StorageProvider).HasMaxLength(50).IsRequired();
        builder.Property(variant => variant.StorageContainer).HasMaxLength(255).IsRequired();
        builder.Property(variant => variant.Sha256).HasMaxLength(64).IsRequired();
        builder.Property(variant => variant.ETag).HasMaxLength(512).IsRequired();
        builder.Property(variant => variant.VersionId).HasMaxLength(500);
        builder.HasIndex(variant => new { variant.AssetId, variant.Kind }).IsUnique().HasDatabaseName("uq_media_variants_asset_kind");
        builder.HasIndex(variant => variant.AssetId).HasDatabaseName("ix_media_variants_asset_id");
        builder.HasOne<MediaAsset>().WithMany().HasForeignKey(variant => variant.AssetId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_media_variants_media_assets_asset_id");
    }
}

internal sealed class CaptionTrackConfiguration : IEntityTypeConfiguration<CaptionTrack>
{
    public void Configure(EntityTypeBuilder<CaptionTrack> builder)
    {
        builder.ToTable("caption_tracks", "media", table =>
        {
            table.HasCheckConstraint("ck_caption_tracks_bytes", "bytes IS NULL OR bytes > 0");
            table.HasCheckConstraint("ck_caption_tracks_state", "state IN ('Pending', 'Ready', 'Rejected')");
        });
        builder.HasKey(track => track.Id).HasName("pk_caption_tracks");
        builder.Property(track => track.Id).ValueGeneratedNever();
        builder.Property(track => track.State).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(track => track.Locale).HasMaxLength(16).IsRequired();
        builder.Property(track => track.Label).HasMaxLength(120).IsRequired();
        builder.Property(track => track.ObjectKey).HasMaxLength(512).IsRequired();
        builder.Property(track => track.StorageProvider).HasMaxLength(50).IsRequired();
        builder.Property(track => track.StorageContainer).HasMaxLength(255).IsRequired();
        builder.Property(track => track.Sha256).HasMaxLength(64);
        builder.Property(track => track.ETag).HasMaxLength(512);
        builder.Property(track => track.VersionId).HasMaxLength(500);
        builder.Property(track => track.RejectionCode).HasMaxLength(100);
        builder.HasIndex(track => new { track.AssetId, track.Locale }).IsUnique().HasDatabaseName("uq_caption_tracks_asset_locale");
        builder.HasIndex(track => track.SourceMediaAssetId).IsUnique().HasDatabaseName("uq_caption_tracks_source_media_asset_id");
        builder.HasOne<MediaAsset>().WithMany().HasForeignKey(track => track.AssetId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_caption_tracks_media_assets_asset_id");
        builder.HasOne<MediaAsset>().WithMany().HasForeignKey(track => track.SourceMediaAssetId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_caption_tracks_media_assets_source_media_asset_id");
    }
}

internal sealed class MediaProcessingJobConfiguration : IEntityTypeConfiguration<MediaProcessingJob>
{
    public void Configure(EntityTypeBuilder<MediaProcessingJob> builder)
    {
        builder.ToTable("media_processing_jobs", "media", table =>
        {
            table.HasCheckConstraint("ck_media_processing_jobs_attempt_count", "attempt_count >= 0");
            table.HasCheckConstraint("ck_media_processing_jobs_state", "state IN ('Pending', 'Processing', 'Completed', 'Failed')");
        });
        builder.HasKey(job => job.Id).HasName("pk_media_processing_jobs");
        builder.Property(job => job.Id).ValueGeneratedNever();
        builder.Property(job => job.State).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(job => job.LastErrorCode).HasMaxLength(200);
        builder.HasIndex(job => new { job.AvailableAt, job.CreatedAt, job.Id })
            .HasDatabaseName("ix_media_processing_jobs_pending")
            .HasFilter("state IN ('Pending', 'Processing') AND completed_at IS NULL");
        builder.HasIndex(job => job.LockedUntil)
            .HasDatabaseName("ix_media_processing_jobs_locked_until")
            .HasFilter("state = 'Processing' AND completed_at IS NULL");
        builder.HasIndex(job => job.AssetId).IsUnique().HasDatabaseName("uq_media_processing_jobs_asset_id");
        builder.HasOne<MediaAsset>().WithMany().HasForeignKey(job => job.AssetId)
            .OnDelete(DeleteBehavior.Cascade).HasConstraintName("fk_media_processing_jobs_media_assets_asset_id");
    }
}
