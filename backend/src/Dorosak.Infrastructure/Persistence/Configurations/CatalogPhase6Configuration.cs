using Dorosak.Domain.Catalog;
using Dorosak.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dorosak.Infrastructure.Persistence.Configurations;

internal sealed class CourseConfiguration : IEntityTypeConfiguration<Course>
{
    public void Configure(EntityTypeBuilder<Course> builder)
    {
        builder.ToTable("courses", "catalog", table =>
        {
            table.HasCheckConstraint("ck_courses_default_locale", "default_locale IN ('ar', 'en')");
            table.HasCheckConstraint(
                "ck_courses_status",
                "status IN ('Draft', 'InReview', 'ChangesRequested', 'ReadyToPublish', 'Archived')");
        });
        builder.HasKey(course => course.Id).HasName("pk_courses");
        builder.Property(course => course.Id).ValueGeneratedNever();
        builder.Property(course => course.DefaultLocale).HasMaxLength(2).IsRequired();
        builder.Property(course => course.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(course => course.DeletionReason).HasMaxLength(1000);
        builder.HasIndex(course => new { course.OwnerUserId, course.UpdatedAt, course.Id })
            .HasDatabaseName("ix_courses_owner_updated_id")
            .HasFilter("deleted_at IS NULL");
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(course => course.OwnerUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_courses_users_owner_user_id");
    }
}

internal sealed class CourseSlugConfiguration : IEntityTypeConfiguration<CourseSlug>
{
    public void Configure(EntityTypeBuilder<CourseSlug> builder)
    {
        builder.ToTable("course_slugs", "catalog", table =>
        {
            table.HasCheckConstraint("ck_course_slugs_locale", "locale IN ('ar', 'en')");
            table.HasCheckConstraint("ck_course_slugs_value", "slug ~ '^[a-z0-9]+(-[a-z0-9]+)*$'");
        });
        builder.HasKey(slug => slug.Id).HasName("pk_course_slugs");
        builder.Property(slug => slug.Id).ValueGeneratedNever();
        builder.Property(slug => slug.Locale).HasMaxLength(2).IsRequired();
        builder.Property(slug => slug.Slug).HasMaxLength(160).IsRequired();
        builder.HasAlternateKey(slug => new { slug.Id, slug.CourseId, slug.Locale })
            .HasName("ak_course_slugs_id_course_locale");
        builder.HasIndex(slug => new { slug.Locale, slug.Slug })
            .IsUnique()
            .HasDatabaseName("uq_course_slugs_locale_slug");
        builder.HasIndex(slug => new { slug.CourseId, slug.Locale })
            .IsUnique()
            .HasDatabaseName("uq_course_slugs_current_course_locale")
            .HasFilter("is_current");
        builder.HasOne<Course>()
            .WithMany()
            .HasForeignKey(slug => slug.CourseId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_course_slugs_courses_course_id");
    }
}

internal sealed class CourseLocalizationConfiguration : IEntityTypeConfiguration<CourseLocalization>
{
    public void Configure(EntityTypeBuilder<CourseLocalization> builder)
    {
        builder.ToTable("course_localizations", "catalog", table =>
            table.HasCheckConstraint("ck_course_localizations_locale", "locale IN ('ar', 'en')"));
        builder.HasKey(localization => new { localization.CourseId, localization.Locale })
            .HasName("pk_course_localizations");
        builder.Property(localization => localization.Locale).HasMaxLength(2);
        builder.Property(localization => localization.Title).HasMaxLength(200).IsRequired();
        builder.Property(localization => localization.Subtitle).HasMaxLength(300).IsRequired();
        builder.Property(localization => localization.Description).HasMaxLength(10000).IsRequired();
        builder.HasOne<Course>()
            .WithMany()
            .HasForeignKey(localization => localization.CourseId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_course_localizations_courses_course_id");
        builder.HasOne<CourseSlug>()
            .WithMany()
            .HasForeignKey(localization => new
            {
                localization.CurrentSlugId,
                localization.CourseId,
                localization.Locale,
            })
            .HasPrincipalKey(slug => new { slug.Id, slug.CourseId, slug.Locale })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_course_localizations_current_slug");
    }
}

internal sealed class CourseInstructorConfiguration : IEntityTypeConfiguration<CourseInstructor>
{
    public void Configure(EntityTypeBuilder<CourseInstructor> builder)
    {
        builder.ToTable("course_instructors", "catalog", table =>
            table.HasCheckConstraint("ck_course_instructors_role", "role IN ('Editor', 'CoInstructor', 'Reviewer')"));
        builder.HasKey(instructor => new { instructor.CourseId, instructor.UserId }).HasName("pk_course_instructors");
        builder.Property(instructor => instructor.Role).HasConversion<string>().HasMaxLength(30);
        builder.HasIndex(instructor => instructor.UserId).HasDatabaseName("ix_course_instructors_user_id");
        builder.HasOne<Course>()
            .WithMany()
            .HasForeignKey(instructor => instructor.CourseId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_course_instructors_courses_course_id");
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(instructor => instructor.UserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_course_instructors_users_user_id");
    }
}

internal sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    private static readonly DateTimeOffset SeededAt = new(2026, 8, 7, 0, 0, 0, TimeSpan.Zero);
    internal static readonly Guid TechnologyId = Guid.Parse("01989b44-0000-7000-8000-000000000001");
    internal static readonly Guid BusinessId = Guid.Parse("01989b44-0000-7000-8000-000000000002");
    internal static readonly Guid DataId = Guid.Parse("01989b44-0000-7000-8000-000000000003");
    internal static readonly Guid PersonalDevelopmentId = Guid.Parse("01989b44-0000-7000-8000-000000000004");

    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("categories", "catalog", table =>
            table.HasCheckConstraint("ck_categories_code", "code ~ '^[a-z0-9]+(-[a-z0-9]+)*$'"));
        builder.HasKey(category => category.Id).HasName("pk_categories");
        builder.Property(category => category.Id).ValueGeneratedNever();
        builder.Property(category => category.Code).HasMaxLength(80).IsRequired();
        builder.HasIndex(category => category.Code).IsUnique().HasDatabaseName("uq_categories_code");
        builder.HasIndex(category => new { category.ParentId, category.DisplayOrder, category.Id })
            .HasDatabaseName("ix_categories_parent_order_id");
        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(category => category.ParentId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_categories_categories_parent_id");
        builder.HasData(
            Create(TechnologyId, "technology", 10),
            Create(BusinessId, "business", 20),
            Create(DataId, "data", 30),
            Create(PersonalDevelopmentId, "personal-development", 40));
    }

    private static Category Create(Guid id, string code, int order)
    {
        Category category = Category.Create(code, null, order, SeededAt);
        typeof(Category).GetProperty(nameof(Category.Id))!.SetValue(category, id);
        return category;
    }
}

internal sealed class CategoryLocalizationConfiguration : IEntityTypeConfiguration<CategoryLocalization>
{
    public void Configure(EntityTypeBuilder<CategoryLocalization> builder)
    {
        builder.ToTable("category_localizations", "catalog", table =>
            table.HasCheckConstraint("ck_category_localizations_locale", "locale IN ('ar', 'en')"));
        builder.HasKey(localization => new { localization.CategoryId, localization.Locale })
            .HasName("pk_category_localizations");
        builder.Property(localization => localization.Locale).HasMaxLength(2);
        builder.Property(localization => localization.Name).HasMaxLength(200).IsRequired();
        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(localization => localization.CategoryId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_category_localizations_categories_category_id");
        builder.HasData(
            Create(CategoryConfiguration.TechnologyId, "ar", "التكنولوجيا"),
            Create(CategoryConfiguration.TechnologyId, "en", "Technology"),
            Create(CategoryConfiguration.BusinessId, "ar", "الأعمال"),
            Create(CategoryConfiguration.BusinessId, "en", "Business"),
            Create(CategoryConfiguration.DataId, "ar", "البيانات"),
            Create(CategoryConfiguration.DataId, "en", "Data"),
            Create(CategoryConfiguration.PersonalDevelopmentId, "ar", "التطوير الشخصي"),
            Create(CategoryConfiguration.PersonalDevelopmentId, "en", "Personal Development"));
    }

    private static CategoryLocalization Create(Guid categoryId, string locale, string name) =>
        CategoryLocalization.Create(categoryId, locale, name);
}

internal sealed class TagConfiguration : IEntityTypeConfiguration<Tag>
{
    public void Configure(EntityTypeBuilder<Tag> builder)
    {
        builder.ToTable("tags", "catalog", table =>
            table.HasCheckConstraint("ck_tags_code", "code ~ '^[a-z0-9]+(-[a-z0-9]+)*$'"));
        builder.HasKey(tag => tag.Id).HasName("pk_tags");
        builder.Property(tag => tag.Id).ValueGeneratedNever();
        builder.Property(tag => tag.Code).HasMaxLength(80).IsRequired();
        builder.HasIndex(tag => tag.Code).IsUnique().HasDatabaseName("uq_tags_code");
    }
}

internal sealed class TagLocalizationConfiguration : IEntityTypeConfiguration<TagLocalization>
{
    public void Configure(EntityTypeBuilder<TagLocalization> builder)
    {
        builder.ToTable("tag_localizations", "catalog", table =>
            table.HasCheckConstraint("ck_tag_localizations_locale", "locale IN ('ar', 'en')"));
        builder.HasKey(localization => new { localization.TagId, localization.Locale }).HasName("pk_tag_localizations");
        builder.Property(localization => localization.Locale).HasMaxLength(2);
        builder.Property(localization => localization.Name).HasMaxLength(200).IsRequired();
        builder.HasOne<Tag>()
            .WithMany()
            .HasForeignKey(localization => localization.TagId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_tag_localizations_tags_tag_id");
    }
}

internal sealed class CourseCategoryConfiguration : IEntityTypeConfiguration<CourseCategory>
{
    public void Configure(EntityTypeBuilder<CourseCategory> builder)
    {
        builder.ToTable("course_categories", "catalog");
        builder.HasKey(item => new { item.CourseId, item.CategoryId }).HasName("pk_course_categories");
        builder.HasIndex(item => new { item.CategoryId, item.CourseId }).HasDatabaseName("ix_course_categories_category_course");
        builder.HasOne<Course>().WithMany().HasForeignKey(item => item.CourseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Category>().WithMany().HasForeignKey(item => item.CategoryId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class CourseTagConfiguration : IEntityTypeConfiguration<CourseTag>
{
    public void Configure(EntityTypeBuilder<CourseTag> builder)
    {
        builder.ToTable("course_tags", "catalog");
        builder.HasKey(item => new { item.CourseId, item.TagId }).HasName("pk_course_tags");
        builder.HasIndex(item => new { item.TagId, item.CourseId }).HasDatabaseName("ix_course_tags_tag_course");
        builder.HasOne<Course>().WithMany().HasForeignKey(item => item.CourseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Tag>().WithMany().HasForeignKey(item => item.TagId).OnDelete(DeleteBehavior.Restrict);
    }
}
