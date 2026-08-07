using Dorosak.Domain.Profiles;
using Dorosak.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dorosak.Infrastructure.Persistence.Configurations;

internal sealed class TeacherApplicationConfiguration : IEntityTypeConfiguration<TeacherApplication>
{
    public void Configure(EntityTypeBuilder<TeacherApplication> builder)
    {
        builder.ToTable("teacher_applications", "profiles", table =>
            table.HasCheckConstraint(
                "ck_teacher_applications_status",
                "status IN ('Pending', 'InReview', 'Approved', 'Rejected', 'Withdrawn')"));
        builder.HasKey(application => application.Id).HasName("pk_teacher_applications");
        builder.Property(application => application.Id).ValueGeneratedNever();
        builder.Property(application => application.Headline).HasMaxLength(160).IsRequired();
        builder.Property(application => application.Biography).HasMaxLength(4000).IsRequired();
        builder.Property(application => application.Expertise).HasMaxLength(1000).IsRequired();
        builder.Property(application => application.Motivation).HasMaxLength(4000).IsRequired();
        builder.Property(application => application.ReviewerReason).HasMaxLength(2000);
        builder.Property(application => application.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.HasIndex(application => application.UserId).HasDatabaseName("ix_teacher_applications_user_id");
        builder.HasIndex(application => application.ReviewerUserId).HasDatabaseName("ix_teacher_applications_reviewer_user_id");
        builder.HasIndex(application => application.UserId)
            .IsUnique()
            .HasDatabaseName("uq_teacher_applications_active_user")
            .HasFilter("status IN ('Pending', 'InReview')");
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(application => application.UserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_teacher_applications_users_user_id");
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(application => application.ReviewerUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_teacher_applications_users_reviewer_user_id");
    }
}

internal sealed class TeacherProfileConfiguration : IEntityTypeConfiguration<TeacherProfile>
{
    public void Configure(EntityTypeBuilder<TeacherProfile> builder)
    {
        builder.ToTable("teacher_profiles", "profiles");
        builder.HasKey(profile => profile.UserId).HasName("pk_teacher_profiles");
        builder.Property(profile => profile.UserId).ValueGeneratedNever();
        builder.Property(profile => profile.Headline).HasMaxLength(160).IsRequired();
        builder.Property(profile => profile.Biography).HasMaxLength(4000).IsRequired();
        builder.Property(profile => profile.Expertise).HasMaxLength(1000).IsRequired();
        builder.HasIndex(profile => profile.ApplicationId).IsUnique().HasDatabaseName("uq_teacher_profiles_application_id");
        builder.HasOne<ApplicationUser>()
            .WithOne()
            .HasForeignKey<TeacherProfile>(profile => profile.UserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_teacher_profiles_users_user_id");
        builder.HasOne<TeacherApplication>()
            .WithOne()
            .HasForeignKey<TeacherProfile>(profile => profile.ApplicationId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_teacher_profiles_applications_application_id");
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(profile => profile.ApprovedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_teacher_profiles_users_approved_by_user_id");
    }
}
