using Dorosak.Domain.Catalog;
using Dorosak.Domain.Engagement;
using Dorosak.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dorosak.Infrastructure.Persistence.Configurations;

internal sealed class CourseReviewConfiguration : IEntityTypeConfiguration<CourseReview>
{
    public void Configure(EntityTypeBuilder<CourseReview> builder)
    {
        builder.ToTable("course_reviews", "engagement", table =>
        {
            table.HasCheckConstraint("ck_course_reviews_rating", "rating BETWEEN 1 AND 5");
            table.HasCheckConstraint("ck_course_reviews_status", "status IN ('Published', 'Hidden', 'Removed')");
        });
        builder.HasKey(review => review.Id).HasName("pk_course_reviews");
        builder.Property(review => review.Id).ValueGeneratedNever();
        builder.Property(review => review.Rating).IsRequired();
        builder.Property(review => review.Text).HasMaxLength(4000).IsRequired();
        builder.Property(review => review.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.HasIndex(review => new { review.UserId, review.CourseId }).IsUnique()
            .HasDatabaseName("uq_course_reviews_user_course");
        builder.HasIndex(review => new { review.CourseId, review.Status, review.CreatedAt, review.Id })
            .IsDescending(false, false, true, true)
            .HasDatabaseName("ix_course_reviews_course_status_created_id");
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(review => review.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Course>().WithMany().HasForeignKey(review => review.CourseId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class DiscussionThreadConfiguration : IEntityTypeConfiguration<DiscussionThread>
{
    public void Configure(EntityTypeBuilder<DiscussionThread> builder)
    {
        builder.ToTable("discussion_threads", "engagement", table =>
        {
            table.HasCheckConstraint("ck_discussion_threads_status", "status IN ('Published', 'Hidden', 'Removed')");
        });
        builder.HasKey(thread => thread.Id).HasName("pk_discussion_threads");
        builder.Property(thread => thread.Id).ValueGeneratedNever();
        builder.Property(thread => thread.Title).HasMaxLength(200).IsRequired();
        builder.Property(thread => thread.Body).HasMaxLength(10000).IsRequired();
        builder.Property(thread => thread.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.HasIndex(thread => new { thread.CourseId, thread.ReleaseId, thread.LessonId, thread.CreatedAt, thread.Id })
            .IsDescending(false, false, false, true, true)
            .HasDatabaseName("ix_discussion_threads_scope_created_id");
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(thread => thread.AuthorUserId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_discussion_threads_users_author_user_id");
        builder.HasOne<Course>().WithMany().HasForeignKey(thread => thread.CourseId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_discussion_threads_courses_course_id");
        builder.HasOne<CourseRelease>().WithMany()
            .HasForeignKey(thread => new { thread.ReleaseId, thread.CourseId })
            .HasPrincipalKey(release => new { release.Id, release.CourseId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_discussion_threads_releases_release_id");
        builder.HasOne<CourseReleaseLesson>().WithMany()
            .HasForeignKey(thread => new { thread.LessonId, thread.ReleaseId })
            .HasPrincipalKey(lesson => new { lesson.Id, lesson.ReleaseId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_discussion_threads_lessons_lesson_id");
    }
}

internal sealed class DiscussionCommentConfiguration : IEntityTypeConfiguration<DiscussionComment>
{
    public void Configure(EntityTypeBuilder<DiscussionComment> builder)
    {
        builder.ToTable("comments", "engagement", table =>
        {
            table.HasCheckConstraint("ck_comments_depth", "depth BETWEEN 0 AND 2");
            table.HasCheckConstraint("ck_comments_status", "status IN ('Published', 'Hidden', 'Removed')");
            table.HasCheckConstraint(
                "ck_comments_parent_depth",
                "(depth = 0 AND parent_comment_id IS NULL AND parent_depth IS NULL) OR (depth BETWEEN 1 AND 2 AND parent_comment_id IS NOT NULL AND parent_depth = depth - 1)");
        });
        builder.HasKey(comment => comment.Id).HasName("pk_comments");
        builder.Property(comment => comment.Id).ValueGeneratedNever();
        builder.Property(comment => comment.Body).HasMaxLength(5000).IsRequired();
        builder.Property(comment => comment.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.HasAlternateKey(comment => new { comment.Id, comment.ThreadId, comment.Depth })
            .HasName("ak_comments_id_thread_depth");
        builder.HasIndex(comment => new { comment.ThreadId, comment.CreatedAt, comment.Id })
            .IsDescending(false, true, true)
            .HasDatabaseName("ix_comments_thread_created_id");
        builder.HasIndex(comment => new { comment.ParentCommentId, comment.ThreadId, comment.ParentDepth })
            .HasDatabaseName("ix_comments_parent_thread");
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(comment => comment.AuthorUserId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_comments_users_author_user_id");
        builder.HasOne<DiscussionThread>().WithMany().HasForeignKey(comment => comment.ThreadId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_comments_threads_thread_id");
        builder.HasOne<DiscussionComment>().WithMany()
            .HasForeignKey(comment => new { comment.ParentCommentId, comment.ThreadId, comment.ParentDepth })
            .HasPrincipalKey(comment => new { comment.Id, comment.ThreadId, comment.Depth })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_comments_parent_thread");
    }
}

internal sealed class CommentLikeConfiguration : IEntityTypeConfiguration<CommentLike>
{
    public void Configure(EntityTypeBuilder<CommentLike> builder)
    {
        builder.ToTable("comment_likes", "engagement");
        builder.HasKey(like => new { like.CommentId, like.UserId }).HasName("pk_comment_likes");
        builder.HasIndex(like => new { like.UserId, like.CreatedAt }).HasDatabaseName("ix_comment_likes_user_created");
        builder.HasOne<DiscussionComment>().WithMany().HasForeignKey(like => like.CommentId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_comment_likes_comments_comment_id");
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(like => like.UserId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_comment_likes_users_user_id");
    }
}
