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
