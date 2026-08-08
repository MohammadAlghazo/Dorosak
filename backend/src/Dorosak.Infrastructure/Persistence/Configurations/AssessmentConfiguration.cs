using Dorosak.Domain.Assessment;
using Dorosak.Domain.Authoring;
using Dorosak.Domain.Catalog;
using Dorosak.Domain.Learning;
using Dorosak.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dorosak.Infrastructure.Persistence.Configurations;

internal sealed class QuizConfiguration : IEntityTypeConfiguration<Quiz>
{
    public void Configure(EntityTypeBuilder<Quiz> builder)
    {
        builder.ToTable("quizzes", "assessment");
        builder.HasKey(quiz => quiz.Id).HasName("pk_quizzes");
        builder.Property(quiz => quiz.Id).ValueGeneratedNever();
        builder.HasIndex(quiz => new { quiz.CourseId, quiz.LessonId }).IsUnique().HasDatabaseName("uq_quizzes_course_lesson");
        builder.HasOne<Course>().WithMany().HasForeignKey(quiz => quiz.CourseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CourseLesson>().WithMany().HasForeignKey(quiz => quiz.LessonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(quiz => quiz.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class QuizVersionConfiguration : IEntityTypeConfiguration<QuizVersion>
{
    public void Configure(EntityTypeBuilder<QuizVersion> builder)
    {
        builder.ToTable("quiz_versions", "assessment", table =>
        {
            table.HasCheckConstraint("ck_quiz_versions_number", "version_number > 0");
            table.HasCheckConstraint("ck_quiz_versions_attempt_limit", "attempt_limit BETWEEN 1 AND 100");
            table.HasCheckConstraint("ck_quiz_versions_duration", "duration_minutes IS NULL OR duration_minutes BETWEEN 1 AND 1440");
            table.HasCheckConstraint("ck_quiz_versions_pass_score", "pass_score BETWEEN 0 AND 100");
            table.HasCheckConstraint("ck_quiz_versions_status", "status IN ('Draft', 'Ready')");
        });
        builder.HasKey(version => version.Id).HasName("pk_quiz_versions");
        builder.Property(version => version.Id).ValueGeneratedNever();
        builder.Property(version => version.Title).HasMaxLength(200).IsRequired();
        builder.Property(version => version.PassScore).HasPrecision(5, 2);
        builder.Property(version => version.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.HasIndex(version => new { version.QuizId, version.VersionNumber }).IsUnique().HasDatabaseName("uq_quiz_versions_quiz_number");
        builder.HasOne<Quiz>().WithMany().HasForeignKey(version => version.QuizId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class QuizQuestionConfiguration : IEntityTypeConfiguration<QuizQuestion>
{
    public void Configure(EntityTypeBuilder<QuizQuestion> builder)
    {
        builder.ToTable("questions", "assessment", table =>
        {
            table.HasCheckConstraint("ck_questions_position", "position >= 0");
            table.HasCheckConstraint("ck_questions_points", "points > 0");
            table.HasCheckConstraint("ck_questions_type", "type IN ('SingleChoice', 'MultipleChoice', 'TrueFalse', 'ShortAnswer')");
        });
        builder.HasKey(question => question.Id).HasName("pk_questions");
        builder.Property(question => question.Id).ValueGeneratedNever();
        builder.Property(question => question.Type).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(question => question.Prompt).HasMaxLength(10000).IsRequired();
        builder.Property(question => question.AcceptedAnswer).HasMaxLength(2000);
        builder.Property(question => question.Points).HasPrecision(8, 2);
        builder.HasIndex(question => new { question.QuizVersionId, question.Position }).IsUnique().HasDatabaseName("uq_questions_version_position");
        builder.HasOne<QuizVersion>().WithMany().HasForeignKey(question => question.QuizVersionId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class QuizQuestionOptionConfiguration : IEntityTypeConfiguration<QuizQuestionOption>
{
    public void Configure(EntityTypeBuilder<QuizQuestionOption> builder)
    {
        builder.ToTable("question_options", "assessment", table =>
            table.HasCheckConstraint("ck_question_options_position", "position >= 0"));
        builder.HasKey(option => option.Id).HasName("pk_question_options");
        builder.Property(option => option.Id).ValueGeneratedNever();
        builder.Property(option => option.Text).HasMaxLength(2000).IsRequired();
        builder.HasIndex(option => new { option.QuestionId, option.Position }).IsUnique().HasDatabaseName("uq_question_options_question_position");
        builder.HasOne<QuizQuestion>().WithMany().HasForeignKey(option => option.QuestionId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class QuizAttemptConfiguration : IEntityTypeConfiguration<QuizAttempt>
{
    public void Configure(EntityTypeBuilder<QuizAttempt> builder)
    {
        builder.ToTable("quiz_attempts", "assessment", table =>
        {
            table.HasCheckConstraint("ck_quiz_attempts_number", "attempt_number > 0");
            table.HasCheckConstraint("ck_quiz_attempts_score", "score IS NULL OR score BETWEEN 0 AND 100");
            table.HasCheckConstraint("ck_quiz_attempts_status", "status IN ('InProgress', 'Submitted', 'PendingManualGrade', 'Graded')");
        });
        builder.HasKey(attempt => attempt.Id).HasName("pk_quiz_attempts");
        builder.Property(attempt => attempt.Id).ValueGeneratedNever();
        builder.Property(attempt => attempt.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(attempt => attempt.Score).HasPrecision(5, 2);
        builder.HasIndex(attempt => new { attempt.EnrollmentId, attempt.QuizVersionId, attempt.AttemptNumber }).IsUnique()
            .HasDatabaseName("uq_quiz_attempts_enrollment_version_number");
        builder.HasIndex(attempt => new { attempt.EnrollmentId, attempt.QuizVersionId }).HasDatabaseName("ix_quiz_attempts_enrollment_version");
        builder.HasOne<Enrollment>().WithMany().HasForeignKey(attempt => attempt.EnrollmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<QuizVersion>().WithMany().HasForeignKey(attempt => attempt.QuizVersionId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class QuizAnswerConfiguration : IEntityTypeConfiguration<QuizAnswer>
{
    public void Configure(EntityTypeBuilder<QuizAnswer> builder)
    {
        builder.ToTable("quiz_answers", "assessment");
        builder.HasKey(answer => new { answer.AttemptId, answer.QuestionId }).HasName("pk_quiz_answers");
        builder.Property(answer => answer.TextAnswer).HasMaxLength(10000);
        builder.Property(answer => answer.SelectedOptionIds).HasMaxLength(4000).IsRequired();
        builder.Property(answer => answer.AwardedPoints).HasPrecision(8, 2);
        builder.HasOne<QuizAttempt>().WithMany().HasForeignKey(answer => answer.AttemptId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<QuizQuestion>().WithMany().HasForeignKey(answer => answer.QuestionId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class AssignmentConfiguration : IEntityTypeConfiguration<Assignment>
{
    public void Configure(EntityTypeBuilder<Assignment> builder)
    {
        builder.ToTable("assignments", "assessment");
        builder.HasKey(assignment => assignment.Id).HasName("pk_assignments");
        builder.Property(assignment => assignment.Id).ValueGeneratedNever();
        builder.HasIndex(assignment => new { assignment.CourseId, assignment.LessonId }).IsUnique().HasDatabaseName("uq_assignments_course_lesson");
        builder.HasOne<Course>().WithMany().HasForeignKey(assignment => assignment.CourseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CourseLesson>().WithMany().HasForeignKey(assignment => assignment.LessonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(assignment => assignment.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class AssignmentVersionConfiguration : IEntityTypeConfiguration<AssignmentVersion>
{
    public void Configure(EntityTypeBuilder<AssignmentVersion> builder)
    {
        builder.ToTable("assignment_versions", "assessment", table =>
        {
            table.HasCheckConstraint("ck_assignment_versions_number", "version_number > 0");
            table.HasCheckConstraint("ck_assignment_versions_status", "status IN ('Draft', 'Ready')");
        });
        builder.HasKey(version => version.Id).HasName("pk_assignment_versions");
        builder.Property(version => version.Id).ValueGeneratedNever();
        builder.Property(version => version.Title).HasMaxLength(200).IsRequired();
        builder.Property(version => version.Instructions).HasMaxLength(100000).IsRequired();
        builder.Property(version => version.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.HasIndex(version => new { version.AssignmentId, version.VersionNumber }).IsUnique().HasDatabaseName("uq_assignment_versions_assignment_number");
        builder.HasOne<Assignment>().WithMany().HasForeignKey(version => version.AssignmentId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class AssignmentSubmissionConfiguration : IEntityTypeConfiguration<AssignmentSubmission>
{
    public void Configure(EntityTypeBuilder<AssignmentSubmission> builder)
    {
        builder.ToTable("assignment_submissions", "assessment", table =>
            table.HasCheckConstraint("ck_assignment_submissions_number", "submission_number > 0"));
        builder.HasKey(submission => submission.Id).HasName("pk_assignment_submissions");
        builder.Property(submission => submission.Id).ValueGeneratedNever();
        builder.Property(submission => submission.Text).HasMaxLength(100000).IsRequired();
        builder.HasIndex(submission => new { submission.EnrollmentId, submission.AssignmentVersionId, submission.SubmissionNumber }).IsUnique()
            .HasDatabaseName("uq_assignment_submissions_enrollment_version_number");
        builder.HasOne<Enrollment>().WithMany().HasForeignKey(submission => submission.EnrollmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AssignmentVersion>().WithMany().HasForeignKey(submission => submission.AssignmentVersionId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class GradeRevisionConfiguration : IEntityTypeConfiguration<GradeRevision>
{
    public void Configure(EntityTypeBuilder<GradeRevision> builder)
    {
        builder.ToTable("grade_revisions", "assessment", table =>
        {
            table.HasCheckConstraint("ck_grade_revisions_number", "revision_number > 0");
            table.HasCheckConstraint("ck_grade_revisions_score", "score BETWEEN 0 AND 100");
        });
        builder.HasKey(revision => revision.Id).HasName("pk_grade_revisions");
        builder.Property(revision => revision.Id).ValueGeneratedNever();
        builder.Property(revision => revision.Score).HasPrecision(5, 2);
        builder.Property(revision => revision.Feedback).HasMaxLength(10000).IsRequired();
        builder.HasIndex(revision => new { revision.SubmissionId, revision.RevisionNumber }).IsUnique().HasDatabaseName("uq_grade_revisions_submission_number");
        builder.HasOne<AssignmentSubmission>().WithMany().HasForeignKey(revision => revision.SubmissionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(revision => revision.GradedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
