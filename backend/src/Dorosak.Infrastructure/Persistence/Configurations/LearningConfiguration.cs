using Dorosak.Domain.Catalog;
using Dorosak.Domain.Learning;
using Dorosak.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dorosak.Infrastructure.Persistence.Configurations;

internal sealed class EntitlementConfiguration : IEntityTypeConfiguration<Entitlement>
{
    public void Configure(EntityTypeBuilder<Entitlement> builder)
    {
        builder.ToTable("entitlements", "learning", table =>
        {
            table.HasCheckConstraint("ck_entitlements_source", "source = 'Free'");
            table.HasCheckConstraint("ck_entitlements_status", "status IN ('Active', 'Revoked', 'Expired')");
        });
        builder.HasKey(entitlement => entitlement.Id).HasName("pk_entitlements");
        builder.Property(entitlement => entitlement.Id).ValueGeneratedNever();
        builder.Property(entitlement => entitlement.Source).HasMaxLength(30).IsRequired();
        builder.Property(entitlement => entitlement.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.HasIndex(entitlement => new { entitlement.UserId, entitlement.CourseId }).IsUnique()
            .HasFilter("status = 'Active'").HasDatabaseName("uq_entitlements_active_user_course");
        builder.HasIndex(entitlement => entitlement.CourseId).HasDatabaseName("ix_entitlements_course_id");
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(entitlement => entitlement.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Course>().WithMany().HasForeignKey(entitlement => entitlement.CourseId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class EnrollmentConfiguration : IEntityTypeConfiguration<Enrollment>
{
    public void Configure(EntityTypeBuilder<Enrollment> builder)
    {
        builder.ToTable("enrollments", "learning", table =>
            table.HasCheckConstraint("ck_enrollments_status", "status IN ('Active', 'Completed', 'Suspended', 'Revoked', 'Expired')"));
        builder.HasKey(enrollment => enrollment.Id).HasName("pk_enrollments");
        builder.Property(enrollment => enrollment.Id).ValueGeneratedNever();
        builder.Property(enrollment => enrollment.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.HasIndex(enrollment => new { enrollment.UserId, enrollment.CourseId }).IsUnique()
            .HasFilter("status IN ('Active', 'Completed', 'Suspended')")
            .HasDatabaseName("uq_enrollments_current_user_course");
        builder.HasIndex(enrollment => new { enrollment.UserId, enrollment.LastAccessedAt, enrollment.Id }).IsDescending()
            .HasDatabaseName("ix_enrollments_user_last_accessed_id");
        builder.HasIndex(enrollment => enrollment.ReleaseId).HasDatabaseName("ix_enrollments_release_id");
        builder.HasIndex(enrollment => enrollment.EntitlementId).IsUnique().HasDatabaseName("uq_enrollments_entitlement_id");
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(enrollment => enrollment.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Course>().WithMany().HasForeignKey(enrollment => enrollment.CourseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CourseRelease>().WithMany()
            .HasForeignKey(enrollment => new { enrollment.ReleaseId, enrollment.CourseId })
            .HasPrincipalKey(release => new { release.Id, release.CourseId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Entitlement>().WithMany().HasForeignKey(enrollment => enrollment.EntitlementId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class LessonProgressConfiguration : IEntityTypeConfiguration<LessonProgress>
{
    public void Configure(EntityTypeBuilder<LessonProgress> builder)
    {
        builder.ToTable("lesson_progress", "learning", table =>
        {
            table.HasCheckConstraint("ck_lesson_progress_sequence", "last_sequence >= 0");
            table.HasCheckConstraint("ck_lesson_progress_position", "position_seconds >= 0");
        });
        builder.HasKey(progress => new { progress.EnrollmentId, progress.LessonId }).HasName("pk_lesson_progress");
        builder.Property(progress => progress.PositionSeconds).HasPrecision(12, 3);
        builder.Property(progress => progress.WatchedIntervals).HasMaxLength(20000).IsRequired();
        builder.HasIndex(progress => progress.LessonId).HasDatabaseName("ix_lesson_progress_lesson_id");
        builder.HasOne<Enrollment>().WithMany().HasForeignKey(progress => progress.EnrollmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CourseReleaseLesson>().WithMany().HasForeignKey(progress => progress.LessonId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class CourseCompletionConfiguration : IEntityTypeConfiguration<CourseCompletion>
{
    public void Configure(EntityTypeBuilder<CourseCompletion> builder)
    {
        builder.ToTable("course_completions", "learning");
        builder.HasKey(completion => completion.EnrollmentId).HasName("pk_course_completions");
        builder.HasOne<Enrollment>().WithOne().HasForeignKey<CourseCompletion>(completion => completion.EnrollmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Course>().WithMany().HasForeignKey(completion => completion.CourseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CourseRelease>().WithMany()
            .HasForeignKey(completion => new { completion.ReleaseId, completion.CourseId })
            .HasPrincipalKey(release => new { release.Id, release.CourseId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class BookmarkConfiguration : IEntityTypeConfiguration<Bookmark>
{
    public void Configure(EntityTypeBuilder<Bookmark> builder)
    {
        builder.ToTable("bookmarks", "learning");
        builder.HasKey(bookmark => new { bookmark.UserId, bookmark.EnrollmentId, bookmark.LessonId }).HasName("pk_bookmarks");
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(bookmark => bookmark.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Enrollment>().WithMany().HasForeignKey(bookmark => bookmark.EnrollmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CourseReleaseLesson>().WithMany().HasForeignKey(bookmark => bookmark.LessonId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class LearningNoteConfiguration : IEntityTypeConfiguration<LearningNote>
{
    public void Configure(EntityTypeBuilder<LearningNote> builder)
    {
        builder.ToTable("notes", "learning");
        builder.HasKey(note => note.Id).HasName("pk_learning_notes");
        builder.Property(note => note.Id).ValueGeneratedNever();
        builder.Property(note => note.Text).HasMaxLength(5000).IsRequired();
        builder.HasIndex(note => new { note.UserId, note.EnrollmentId, note.LessonId, note.UpdatedAt })
            .HasDatabaseName("ix_learning_notes_user_enrollment_lesson_updated");
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(note => note.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Enrollment>().WithMany().HasForeignKey(note => note.EnrollmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CourseReleaseLesson>().WithMany().HasForeignKey(note => note.LessonId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class RecentlyViewedLessonConfiguration : IEntityTypeConfiguration<RecentlyViewedLesson>
{
    public void Configure(EntityTypeBuilder<RecentlyViewedLesson> builder)
    {
        builder.ToTable("recently_viewed", "learning");
        builder.HasKey(item => new { item.UserId, item.EnrollmentId, item.LessonId }).HasName("pk_recently_viewed");
        builder.HasIndex(item => new { item.UserId, item.ViewedAt }).IsDescending().HasDatabaseName("ix_recently_viewed_user_viewed_at");
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Enrollment>().WithMany().HasForeignKey(item => item.EnrollmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CourseReleaseLesson>().WithMany().HasForeignKey(item => item.LessonId).OnDelete(DeleteBehavior.Restrict);
    }
}
