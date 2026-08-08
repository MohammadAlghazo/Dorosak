using Dorosak.Domain.Authoring;
using Dorosak.Domain.Catalog;
using Dorosak.Domain.Media;
using Dorosak.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dorosak.Infrastructure.Persistence.Configurations;

internal sealed class CourseReleaseConfiguration : IEntityTypeConfiguration<CourseRelease>
{
    public void Configure(EntityTypeBuilder<CourseRelease> builder)
    {
        builder.ToTable("course_releases", "catalog", table =>
        {
            table.HasCheckConstraint("ck_course_releases_number", "release_number > 0");
            table.HasCheckConstraint("ck_course_releases_draft_version", "source_draft_version > 0");
            table.HasCheckConstraint("ck_course_releases_default_locale", "default_locale IN ('ar', 'en')");
            table.HasCheckConstraint("ck_course_releases_manifest_hash", "manifest_hash ~ '^[0-9a-f]{64}$'");
            table.HasCheckConstraint("ck_course_releases_state", "state IN ('Draft', 'Active', 'Superseded', 'Unpublished')");
        });
        builder.HasKey(release => release.Id).HasName("pk_course_releases");
        builder.Property(release => release.Id).ValueGeneratedNever();
        builder.Property(release => release.DefaultLocale).HasMaxLength(2).IsRequired();
        builder.Property(release => release.ManifestHash).HasMaxLength(64).IsRequired();
        builder.Property(release => release.State).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.HasAlternateKey(release => new { release.Id, release.CourseId })
            .HasName("ak_course_releases_id_course");
        builder.HasIndex(release => new { release.CourseId, release.ReleaseNumber })
            .IsUnique().HasDatabaseName("uq_course_releases_course_number");
        builder.HasIndex(release => new { release.CourseId, release.SourceDraftId, release.SourceDraftVersion })
            .IsUnique().HasDatabaseName("uq_course_releases_course_draft_version");
        builder.HasIndex(release => release.CourseId)
            .IsUnique()
            .HasFilter("state = 'Active'")
            .HasDatabaseName("uq_course_releases_active_course");
        builder.HasIndex(release => new { release.CourseId, release.PublishedAt, release.Id })
            .HasDatabaseName("ix_course_releases_course_published_id");
        builder.HasOne<Course>().WithMany().HasForeignKey(release => release.CourseId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_course_releases_courses_course_id");
        builder.HasOne<CourseDraft>().WithMany().HasForeignKey(release => release.SourceDraftId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_course_releases_course_drafts_source_draft_id");
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(release => release.PublishedByUserId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_course_releases_users_published_by_user_id");
    }
}

internal sealed class CourseReleaseSectionConfiguration : IEntityTypeConfiguration<CourseReleaseSection>
{
    public void Configure(EntityTypeBuilder<CourseReleaseSection> builder)
    {
        builder.ToTable("course_release_sections", "catalog", table =>
            table.HasCheckConstraint("ck_course_release_sections_position", "position >= 0"));
        builder.HasKey(section => section.Id).HasName("pk_course_release_sections");
        builder.Property(section => section.Id).ValueGeneratedNever();
        builder.Property(section => section.Title).HasMaxLength(200).IsRequired();
        builder.HasIndex(section => new { section.ReleaseId, section.Position }).IsUnique()
            .HasDatabaseName("uq_course_release_sections_release_position");
        builder.HasIndex(section => new { section.ReleaseId, section.SourceSectionId }).IsUnique()
            .HasDatabaseName("uq_course_release_sections_release_source");
        builder.HasAlternateKey(section => new { section.Id, section.ReleaseId })
            .HasName("ak_course_release_sections_id_release");
        builder.HasOne<CourseRelease>().WithMany().HasForeignKey(section => section.ReleaseId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_course_release_sections_releases_release_id");
        builder.HasOne<CourseSection>().WithMany().HasForeignKey(section => section.SourceSectionId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_course_release_sections_sections_source_section_id");
        builder.HasOne<SectionRevision>().WithMany().HasForeignKey(section => section.SourceRevisionId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_course_release_sections_revisions_source_revision_id");
    }
}

internal sealed class CourseReleaseLessonConfiguration : IEntityTypeConfiguration<CourseReleaseLesson>
{
    public void Configure(EntityTypeBuilder<CourseReleaseLesson> builder)
    {
        builder.ToTable("course_release_lessons", "catalog", table =>
        {
            table.HasCheckConstraint("ck_course_release_lessons_position", "position >= 0");
            table.HasCheckConstraint("ck_course_release_lessons_completion", "completion_requirement BETWEEN 0 AND 1");
            table.HasCheckConstraint("ck_course_release_lessons_type", "lesson_type IN ('Video', 'Article', 'Document', 'Quiz', 'Assignment')");
        });
        builder.HasKey(lesson => lesson.Id).HasName("pk_course_release_lessons");
        builder.Property(lesson => lesson.Id).ValueGeneratedNever();
        builder.Property(lesson => lesson.Title).HasMaxLength(200).IsRequired();
        builder.Property(lesson => lesson.LessonType).HasMaxLength(30).IsRequired();
        builder.Property(lesson => lesson.Content).HasMaxLength(100000).IsRequired();
        builder.Property(lesson => lesson.CompletionRequirement).HasPrecision(5, 4);
        builder.HasIndex(lesson => new { lesson.SectionId, lesson.Position }).IsUnique()
            .HasDatabaseName("uq_course_release_lessons_section_position");
        builder.HasIndex(lesson => new { lesson.ReleaseId, lesson.SourceLessonId }).IsUnique()
            .HasDatabaseName("uq_course_release_lessons_release_source");
        builder.HasAlternateKey(lesson => new { lesson.Id, lesson.ReleaseId })
            .HasName("ak_course_release_lessons_id_release");
        builder.HasIndex(lesson => lesson.MediaAssetId).HasDatabaseName("ix_course_release_lessons_media_asset_id");
        builder.HasOne<CourseRelease>().WithMany().HasForeignKey(lesson => lesson.ReleaseId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_course_release_lessons_releases_release_id");
        builder.HasOne<CourseReleaseSection>().WithMany()
            .HasForeignKey(lesson => new { lesson.SectionId, lesson.ReleaseId })
            .HasPrincipalKey(section => new { section.Id, section.ReleaseId })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_course_release_lessons_sections_section_id");
        builder.HasOne<CourseLesson>().WithMany().HasForeignKey(lesson => lesson.SourceLessonId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_course_release_lessons_lessons_source_lesson_id");
        builder.HasOne<LessonRevision>().WithMany().HasForeignKey(lesson => lesson.SourceRevisionId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_course_release_lessons_revisions_source_revision_id");
        builder.HasOne<MediaAsset>().WithMany().HasForeignKey(lesson => lesson.MediaAssetId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_course_release_lessons_media_assets_media_asset_id");
    }
}

internal sealed class CourseReleaseAssessmentConfiguration : IEntityTypeConfiguration<CourseReleaseAssessment>
{
    public void Configure(EntityTypeBuilder<CourseReleaseAssessment> builder)
    {
        builder.ToTable("course_release_assessments", "catalog", table =>
        {
            table.HasCheckConstraint("ck_course_release_assessments_position", "position >= 0");
            table.HasCheckConstraint("ck_course_release_assessments_type", "type IN ('Quiz', 'Assignment')");
            table.HasCheckConstraint(
                "ck_course_release_assessments_version",
                "(type = 'Quiz' AND quiz_version_id IS NOT NULL AND assignment_version_id IS NULL) OR (type = 'Assignment' AND quiz_version_id IS NULL AND assignment_version_id IS NOT NULL)");
        });
        builder.HasKey(assessment => assessment.Id).HasName("pk_course_release_assessments");
        builder.Property(assessment => assessment.Id).ValueGeneratedNever();
        builder.Property(assessment => assessment.Type).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Ignore(assessment => assessment.VersionId);
        builder.HasAlternateKey(assessment => new { assessment.Id, assessment.ReleaseId })
            .HasName("ak_course_release_assessments_id_release");
        builder.HasIndex(assessment => new { assessment.ReleaseId, assessment.LessonId, assessment.Type }).IsUnique()
            .HasDatabaseName("uq_course_release_assessments_release_lesson_type");
        builder.HasIndex(assessment => assessment.QuizVersionId).HasDatabaseName("ix_course_release_assessments_quiz_version_id");
        builder.HasIndex(assessment => assessment.AssignmentVersionId).HasDatabaseName("ix_course_release_assessments_assignment_version_id");
        builder.HasOne<CourseRelease>().WithMany().HasForeignKey(assessment => assessment.ReleaseId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_course_release_assessments_releases_release_id");
        builder.HasOne<CourseReleaseLesson>().WithMany()
            .HasForeignKey(assessment => new { assessment.LessonId, assessment.ReleaseId })
            .HasPrincipalKey(lesson => new { lesson.Id, lesson.ReleaseId })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_course_release_assessments_lessons_lesson_id");
        builder.HasOne<Dorosak.Domain.Assessment.QuizVersion>().WithMany()
            .HasForeignKey(assessment => assessment.QuizVersionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_course_release_assessments_quiz_versions_version_id");
        builder.HasOne<Dorosak.Domain.Assessment.AssignmentVersion>().WithMany()
            .HasForeignKey(assessment => assessment.AssignmentVersionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_course_release_assessments_assignment_versions_version_id");
    }
}

internal sealed class CourseReleaseMediaVariantConfiguration : IEntityTypeConfiguration<CourseReleaseMediaVariant>
{
    public void Configure(EntityTypeBuilder<CourseReleaseMediaVariant> builder)
    {
        builder.ToTable("course_release_media_variants", "catalog", table =>
            table.HasCheckConstraint("ck_course_release_media_variants_bytes", "bytes > 0"));
        builder.HasKey(variant => variant.Id).HasName("pk_course_release_media_variants");
        builder.Property(variant => variant.Id).ValueGeneratedNever();
        builder.Property(variant => variant.Kind).HasMaxLength(80).IsRequired();
        builder.Property(variant => variant.ContentType).HasMaxLength(120).IsRequired();
        builder.Property(variant => variant.DurationSeconds).HasPrecision(12, 3);
        builder.HasIndex(variant => new { variant.ReleaseId, variant.LessonId, variant.VariantId }).IsUnique()
            .HasDatabaseName("uq_course_release_media_variants_manifest");
        builder.HasOne<CourseRelease>().WithMany().HasForeignKey(variant => variant.ReleaseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CourseReleaseLesson>().WithMany()
            .HasForeignKey(variant => new { variant.LessonId, variant.ReleaseId })
            .HasPrincipalKey(lesson => new { lesson.Id, lesson.ReleaseId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MediaAsset>().WithMany().HasForeignKey(variant => variant.AssetId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MediaVariant>().WithMany().HasForeignKey(variant => variant.VariantId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class CourseReleaseCaptionConfiguration : IEntityTypeConfiguration<CourseReleaseCaption>
{
    public void Configure(EntityTypeBuilder<CourseReleaseCaption> builder)
    {
        builder.ToTable("course_release_captions", "catalog", table =>
            table.HasCheckConstraint("ck_course_release_captions_locale", "char_length(locale) BETWEEN 2 AND 16"));
        builder.HasKey(caption => caption.Id).HasName("pk_course_release_captions");
        builder.Property(caption => caption.Id).ValueGeneratedNever();
        builder.Property(caption => caption.Locale).HasMaxLength(16).IsRequired();
        builder.Property(caption => caption.Label).HasMaxLength(120).IsRequired();
        builder.HasIndex(caption => new { caption.ReleaseId, caption.LessonId, caption.Locale }).IsUnique()
            .HasDatabaseName("uq_course_release_captions_manifest_locale");
        builder.HasOne<CourseRelease>().WithMany().HasForeignKey(caption => caption.ReleaseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CourseReleaseLesson>().WithMany()
            .HasForeignKey(caption => new { caption.LessonId, caption.ReleaseId })
            .HasPrincipalKey(lesson => new { lesson.Id, lesson.ReleaseId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MediaAsset>().WithMany().HasForeignKey(caption => caption.AssetId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CaptionTrack>().WithMany().HasForeignKey(caption => caption.CaptionId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class CourseReleaseLocalizationConfiguration : IEntityTypeConfiguration<CourseReleaseLocalization>
{
    public void Configure(EntityTypeBuilder<CourseReleaseLocalization> builder)
    {
        builder.ToTable("course_release_localizations", "catalog", table =>
            table.HasCheckConstraint("ck_course_release_localizations_locale", "locale IN ('ar', 'en')"));
        builder.HasKey(localization => localization.Id).HasName("pk_course_release_localizations");
        builder.Property(localization => localization.Id).ValueGeneratedNever();
        builder.Property(localization => localization.Locale).HasMaxLength(2).IsRequired();
        builder.Property(localization => localization.Slug).HasMaxLength(160).IsRequired();
        builder.Property(localization => localization.Title).HasMaxLength(200).IsRequired();
        builder.Property(localization => localization.Subtitle).HasMaxLength(300).IsRequired();
        builder.Property(localization => localization.Description).HasMaxLength(10000).IsRequired();
        builder.HasIndex(localization => new { localization.ReleaseId, localization.Locale }).IsUnique()
            .HasDatabaseName("uq_course_release_localizations_release_locale");
        builder.HasOne<CourseRelease>().WithMany().HasForeignKey(localization => localization.ReleaseId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class CourseReleaseInstructorConfiguration : IEntityTypeConfiguration<CourseReleaseInstructor>
{
    public void Configure(EntityTypeBuilder<CourseReleaseInstructor> builder)
    {
        builder.ToTable("course_release_instructors", "catalog", table =>
            table.HasCheckConstraint("ck_course_release_instructors_position", "position >= 0"));
        builder.HasKey(instructor => new { instructor.ReleaseId, instructor.UserId }).HasName("pk_course_release_instructors");
        builder.Property(instructor => instructor.DisplayName).HasMaxLength(100).IsRequired();
        builder.HasIndex(instructor => new { instructor.ReleaseId, instructor.Position }).IsUnique()
            .HasDatabaseName("uq_course_release_instructors_release_position");
        builder.HasOne<CourseRelease>().WithMany().HasForeignKey(instructor => instructor.ReleaseId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class CourseReleaseTaxonomyConfiguration : IEntityTypeConfiguration<CourseReleaseTaxonomy>
{
    public void Configure(EntityTypeBuilder<CourseReleaseTaxonomy> builder)
    {
        builder.ToTable("course_release_taxonomy", "catalog");
        builder.HasKey(term => new { term.ReleaseId, term.TermId, term.IsCategory }).HasName("pk_course_release_taxonomy");
        builder.Property(term => term.Code).HasMaxLength(80).IsRequired();
        builder.Property(term => term.Name).HasMaxLength(200).IsRequired();
        builder.HasIndex(term => new { term.IsCategory, term.Code, term.ReleaseId }).HasDatabaseName("ix_course_release_taxonomy_filter");
        builder.HasOne<CourseRelease>().WithMany().HasForeignKey(term => term.ReleaseId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class CatalogDocumentConfiguration : IEntityTypeConfiguration<CatalogDocument>
{
    public void Configure(EntityTypeBuilder<CatalogDocument> builder)
    {
        builder.ToTable("catalog_documents", "catalog", table =>
        {
            table.HasCheckConstraint("ck_catalog_documents_locale", "locale IN ('ar', 'en')");
            table.HasCheckConstraint("ck_catalog_documents_duration", "duration_minutes >= 0");
            table.HasCheckConstraint("ck_catalog_documents_generation", "projection_generation > 0");
        });
        builder.HasKey(document => new { document.ReleaseId, document.Locale }).HasName("pk_catalog_documents");
        builder.Property(document => document.Locale).HasMaxLength(2);
        builder.Property(document => document.Slug).HasMaxLength(160).IsRequired();
        builder.Property(document => document.Title).HasMaxLength(200).IsRequired();
        builder.Property(document => document.Summary).HasMaxLength(300).IsRequired();
        builder.Property(document => document.Description).HasMaxLength(10000).IsRequired();
        builder.Property(document => document.Language).HasMaxLength(20).IsRequired();
        builder.Property(document => document.Level).HasMaxLength(30).IsRequired();
        builder.Property(document => document.SearchText).HasMaxLength(20000).IsRequired();
        builder.Property(document => document.NormalizedArabicText).HasMaxLength(20000).IsRequired();
        builder.HasIndex(document => new { document.Locale, document.PublishedAt, document.ReleaseId }).IsDescending()
            .HasDatabaseName("ix_catalog_documents_locale_published_release");
        builder.HasIndex(document => new { document.CourseId, document.Locale, document.ReleaseId }).IsUnique()
            .HasDatabaseName("uq_catalog_documents_course_locale_release");
        builder.HasIndex(document => new { document.Locale, document.Slug }).HasDatabaseName("ix_catalog_documents_locale_slug");
        builder.HasOne<CourseRelease>().WithMany()
            .HasForeignKey(document => new { document.ReleaseId, document.CourseId })
            .HasPrincipalKey(release => new { release.Id, release.CourseId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class CatalogProjectionStateConfiguration : IEntityTypeConfiguration<CatalogProjectionState>
{
    public void Configure(EntityTypeBuilder<CatalogProjectionState> builder)
    {
        builder.ToTable("projection_state", "catalog", table =>
            table.HasCheckConstraint("ck_catalog_projection_state_singleton", "singleton"));
        builder.HasKey(state => state.Singleton).HasName("pk_catalog_projection_state");
        builder.Property(state => state.Singleton).ValueGeneratedNever();
        builder.Property(state => state.Generation).HasDefaultValue(0L).IsConcurrencyToken();
        builder.HasData(new { Singleton = true, Generation = 0L });
    }
}
