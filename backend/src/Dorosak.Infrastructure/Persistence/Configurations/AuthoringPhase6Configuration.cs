using Dorosak.Domain.Authoring;
using Dorosak.Domain.Catalog;
using Dorosak.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dorosak.Infrastructure.Persistence.Configurations;

internal sealed class CourseDraftConfiguration : IEntityTypeConfiguration<CourseDraft>
{
    public void Configure(EntityTypeBuilder<CourseDraft> builder)
    {
        builder.ToTable("course_drafts", "authoring", table =>
        {
            table.HasCheckConstraint("ck_course_drafts_version", "version > 0");
            table.HasCheckConstraint(
                "ck_course_drafts_level",
                "level IN ('Beginner', 'Intermediate', 'Advanced', 'AllLevels')");
        });
        builder.HasKey(draft => draft.Id).HasName("pk_course_drafts");
        builder.Property(draft => draft.Id).ValueGeneratedNever();
        builder.Property(draft => draft.Level).HasMaxLength(30).IsRequired();
        builder.Property(draft => draft.Version).IsConcurrencyToken();
        builder.HasIndex(draft => draft.CourseId).IsUnique().HasDatabaseName("uq_course_drafts_course_id");
        builder.HasOne<Course>()
            .WithOne()
            .HasForeignKey<CourseDraft>(draft => draft.CourseId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_course_drafts_courses_course_id");
    }
}

internal sealed class CourseSectionConfiguration : IEntityTypeConfiguration<CourseSection>
{
    public void Configure(EntityTypeBuilder<CourseSection> builder)
    {
        builder.ToTable("sections", "authoring", table =>
            table.HasCheckConstraint("ck_sections_position", "position >= 0"));
        builder.HasKey(section => section.Id).HasName("pk_sections");
        builder.Property(section => section.Id).ValueGeneratedNever();
        builder.HasIndex(section => new { section.DraftId, section.Position })
            .IsUnique()
            .HasDatabaseName("uq_sections_active_position")
            .HasFilter("removed_at IS NULL");
        builder.HasOne<CourseDraft>()
            .WithMany()
            .HasForeignKey(section => section.DraftId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_sections_course_drafts_draft_id");
        builder.HasOne<SectionRevision>()
            .WithMany()
            .HasForeignKey(section => section.CurrentRevisionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_sections_current_revision_id");
    }
}

internal sealed class SectionRevisionConfiguration : IEntityTypeConfiguration<SectionRevision>
{
    public void Configure(EntityTypeBuilder<SectionRevision> builder)
    {
        builder.ToTable("section_revisions", "authoring", table =>
        {
            table.HasCheckConstraint("ck_section_revisions_version", "draft_version > 0");
            table.HasCheckConstraint("ck_section_revisions_position", "position >= 0");
        });
        builder.HasKey(revision => revision.Id).HasName("pk_section_revisions");
        builder.Property(revision => revision.Id).ValueGeneratedNever();
        builder.Property(revision => revision.Title).HasMaxLength(200).IsRequired();
        builder.HasIndex(revision => new { revision.SectionId, revision.DraftVersion })
            .IsUnique()
            .HasDatabaseName("uq_section_revisions_section_version");
        builder.HasOne<CourseSection>()
            .WithMany()
            .HasForeignKey(revision => revision.SectionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_section_revisions_sections_section_id");
    }
}

internal sealed class CourseLessonConfiguration : IEntityTypeConfiguration<CourseLesson>
{
    public void Configure(EntityTypeBuilder<CourseLesson> builder)
    {
        builder.ToTable("lessons", "authoring", table =>
            table.HasCheckConstraint("ck_lessons_position", "position >= 0"));
        builder.HasKey(lesson => lesson.Id).HasName("pk_lessons");
        builder.Property(lesson => lesson.Id).ValueGeneratedNever();
        builder.HasIndex(lesson => lesson.DraftId).HasDatabaseName("ix_lessons_draft_id");
        builder.HasIndex(lesson => new { lesson.SectionId, lesson.Position })
            .IsUnique()
            .HasDatabaseName("uq_lessons_active_section_position")
            .HasFilter("removed_at IS NULL");
        builder.HasOne<CourseDraft>()
            .WithMany()
            .HasForeignKey(lesson => lesson.DraftId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_lessons_course_drafts_draft_id");
        builder.HasOne<CourseSection>()
            .WithMany()
            .HasForeignKey(lesson => lesson.SectionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_lessons_sections_section_id");
        builder.HasOne<LessonRevision>()
            .WithMany()
            .HasForeignKey(lesson => lesson.CurrentRevisionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_lessons_current_revision_id");
    }
}

internal sealed class LessonRevisionConfiguration : IEntityTypeConfiguration<LessonRevision>
{
    public void Configure(EntityTypeBuilder<LessonRevision> builder)
    {
        builder.ToTable("lesson_revisions", "authoring", table =>
        {
            table.HasCheckConstraint("ck_lesson_revisions_version", "draft_version > 0");
            table.HasCheckConstraint("ck_lesson_revisions_position", "position >= 0");
            table.HasCheckConstraint(
                "ck_lesson_revisions_type",
                "lesson_type IN ('Video', 'Article', 'Document', 'Quiz', 'Assignment')");
        });
        builder.HasKey(revision => revision.Id).HasName("pk_lesson_revisions");
        builder.Property(revision => revision.Id).ValueGeneratedNever();
        builder.Property(revision => revision.Title).HasMaxLength(200).IsRequired();
        builder.Property(revision => revision.LessonType).HasMaxLength(30).IsRequired();
        builder.Property(revision => revision.Content).HasMaxLength(100000).IsRequired();
        builder.HasIndex(revision => new { revision.LessonId, revision.DraftVersion })
            .IsUnique()
            .HasDatabaseName("uq_lesson_revisions_lesson_version");
        builder.HasOne<CourseLesson>()
            .WithMany()
            .HasForeignKey(revision => revision.LessonId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_lesson_revisions_lessons_lesson_id");
    }
}

internal sealed class PublicationReviewConfiguration : IEntityTypeConfiguration<PublicationReview>
{
    public void Configure(EntityTypeBuilder<PublicationReview> builder)
    {
        builder.ToTable("publication_reviews", "authoring", table =>
            table.HasCheckConstraint(
                "ck_publication_reviews_status",
                "status IN ('Pending', 'ChangesRequested', 'Approved', 'Withdrawn')"));
        builder.HasKey(review => review.Id).HasName("pk_publication_reviews");
        builder.Property(review => review.Id).ValueGeneratedNever();
        builder.Property(review => review.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(review => review.ReviewerReason).HasMaxLength(2000);
        builder.HasIndex(review => new { review.CourseId, review.RequestedAt, review.Id })
            .HasDatabaseName("ix_publication_reviews_course_requested_id");
        builder.HasIndex(review => review.CourseId)
            .IsUnique()
            .HasDatabaseName("uq_publication_reviews_pending_course")
            .HasFilter("status = 'Pending'");
        builder.HasOne<Course>()
            .WithMany()
            .HasForeignKey(review => review.CourseId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_publication_reviews_courses_course_id");
        builder.HasOne<CourseDraft>()
            .WithMany()
            .HasForeignKey(review => review.DraftId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_publication_reviews_course_drafts_draft_id");
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(review => review.RequestedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_publication_reviews_users_requested_by_user_id");
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(review => review.ReviewerUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_publication_reviews_users_reviewer_user_id");
    }
}
