using Dorosak.Domain.Catalog;
using Dorosak.Domain.Engagement;
using Dorosak.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dorosak.Infrastructure.Persistence.Configurations;

internal sealed class ContentReportConfiguration : IEntityTypeConfiguration<ContentReport>
{
    public void Configure(EntityTypeBuilder<ContentReport> builder)
    {
        builder.ToTable("content_reports", "engagement", table =>
        {
            table.HasCheckConstraint(
                "ck_content_reports_exact_target",
                "num_nonnulls(course_id, review_id, comment_id, reported_user_id) = 1");
            table.HasCheckConstraint(
                "ck_content_reports_reason",
                "reason IN ('Spam', 'Harassment', 'HateSpeech', 'Misinformation', 'Copyright', 'PersonalData', 'Other')");
            table.HasCheckConstraint(
                "ck_content_reports_status",
                "status IN ('Open', 'InReview', 'Resolved', 'Dismissed')");
        });
        builder.HasKey(report => report.Id).HasName("pk_content_reports");
        builder.Property(report => report.Id).ValueGeneratedNever();
        builder.Property(report => report.Reason).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(report => report.Details).HasMaxLength(2000).IsRequired();
        builder.Property(report => report.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.HasIndex(report => new { report.Status, report.CreatedAt, report.Id })
            .IsDescending(false, true, true)
            .HasDatabaseName("ix_content_reports_status_created_id");
        builder.HasIndex(report => new { report.CreatedAt, report.Id })
            .IsDescending(true, true)
            .HasDatabaseName("ix_content_reports_created_id");
        builder.HasIndex(report => new { report.ReporterUserId, report.CreatedAt, report.Id })
            .IsDescending(false, true, true)
            .HasDatabaseName("ix_content_reports_reporter_created_id");
        ConfigureOpenTargetIndex(builder, report => new { report.ReporterUserId, report.CourseId }, "course_id", "uq_content_reports_open_course");
        ConfigureOpenTargetIndex(builder, report => new { report.ReporterUserId, report.ReviewId }, "review_id", "uq_content_reports_open_review");
        ConfigureOpenTargetIndex(builder, report => new { report.ReporterUserId, report.CommentId }, "comment_id", "uq_content_reports_open_comment");
        ConfigureOpenTargetIndex(builder, report => new { report.ReporterUserId, report.ReportedUserId }, "reported_user_id", "uq_content_reports_open_user");
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(report => report.ReporterUserId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_content_reports_users_reporter_user_id");
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(report => report.ReportedUserId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_content_reports_users_reported_user_id");
        builder.HasOne<Course>().WithMany().HasForeignKey(report => report.CourseId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_content_reports_courses_course_id");
        builder.HasOne<CourseReview>().WithMany().HasForeignKey(report => report.ReviewId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_content_reports_reviews_review_id");
        builder.HasOne<DiscussionComment>().WithMany().HasForeignKey(report => report.CommentId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_content_reports_comments_comment_id");
    }

    private static void ConfigureOpenTargetIndex(
        EntityTypeBuilder<ContentReport> builder,
        System.Linq.Expressions.Expression<Func<ContentReport, object?>> expression,
        string targetColumn,
        string indexName) => builder.HasIndex(expression)
        .IsUnique()
        .HasFilter($"{targetColumn} IS NOT NULL AND status IN ('Open', 'InReview')")
        .HasDatabaseName(indexName);
}

internal sealed class ModerationCaseConfiguration : IEntityTypeConfiguration<ModerationCase>
{
    public void Configure(EntityTypeBuilder<ModerationCase> builder)
    {
        builder.ToTable("moderation_cases", "engagement", table => table.HasCheckConstraint(
            "ck_moderation_cases_status",
            "status IN ('Open', 'InReview', 'Resolved', 'Dismissed')"));
        builder.HasKey(item => item.Id).HasName("pk_moderation_cases");
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(item => item.Version).IsConcurrencyToken();
        builder.HasIndex(item => item.ReportId).IsUnique().HasDatabaseName("uq_moderation_cases_report_id");
        builder.HasIndex(item => new { item.Status, item.CreatedAt, item.Id })
            .IsDescending(false, true, true)
            .HasDatabaseName("ix_moderation_cases_status_created_id");
        builder.HasIndex(item => new { item.CreatedAt, item.Id })
            .IsDescending(true, true)
            .HasDatabaseName("ix_moderation_cases_created_id");
        builder.HasOne<ContentReport>().WithOne().HasForeignKey<ModerationCase>(item => item.ReportId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_moderation_cases_reports_report_id");
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.AssignedToUserId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_moderation_cases_users_assigned_to_user_id");
    }
}

internal sealed class ModerationActionConfiguration : IEntityTypeConfiguration<ModerationAction>
{
    public void Configure(EntityTypeBuilder<ModerationAction> builder)
    {
        builder.ToTable("moderation_actions", "engagement", table => table.HasCheckConstraint(
            "ck_moderation_actions_type",
            "action_type IN ('StartReview', 'HideContent', 'RestoreContent', 'Resolve', 'Dismiss')"));
        builder.HasKey(action => action.Id).HasName("pk_moderation_actions");
        builder.Property(action => action.Id).ValueGeneratedNever();
        builder.Property(action => action.ActionType).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(action => action.Reason).HasMaxLength(1000).IsRequired();
        builder.HasIndex(action => new { action.CaseId, action.CreatedAt, action.Id })
            .HasDatabaseName("ix_moderation_actions_case_created_id");
        builder.HasOne<ModerationCase>().WithMany().HasForeignKey(action => action.CaseId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_moderation_actions_cases_case_id");
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(action => action.ActorUserId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_moderation_actions_users_actor_user_id");
    }
}
