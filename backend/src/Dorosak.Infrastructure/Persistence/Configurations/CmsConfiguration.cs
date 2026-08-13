using Dorosak.Domain.Cms;
using Dorosak.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dorosak.Infrastructure.Persistence.Configurations;

internal sealed class CmsPageConfiguration : IEntityTypeConfiguration<CmsPage>
{
    public void Configure(EntityTypeBuilder<CmsPage> builder)
    {
        builder.ToTable("pages", "cms", table =>
        {
            table.HasCheckConstraint("ck_cms_pages_slug", "slug IN ('about', 'contact', 'privacy', 'terms')");
            table.HasCheckConstraint("ck_cms_pages_versions", "current_version >= 0 AND (published_version IS NULL OR published_version BETWEEN 1 AND current_version)");
        });
        builder.HasKey(page => page.Id).HasName("pk_cms_pages");
        builder.Property(page => page.Id).ValueGeneratedNever();
        builder.Property(page => page.Slug).HasMaxLength(40).IsRequired();
        builder.HasIndex(page => page.Slug).IsUnique().HasDatabaseName("uq_cms_pages_slug");
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(page => page.PublishedByUserId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_cms_pages_users_published_by_user_id");
    }
}

internal sealed class CmsPageRevisionConfiguration : IEntityTypeConfiguration<CmsPageRevision>
{
    public void Configure(EntityTypeBuilder<CmsPageRevision> builder)
    {
        builder.ToTable("page_revisions", "cms", table =>
            table.HasCheckConstraint("ck_cms_page_revisions_version", "version > 0"));
        builder.HasKey(revision => revision.Id).HasName("pk_cms_page_revisions");
        builder.Property(revision => revision.Id).ValueGeneratedNever();
        builder.Property(revision => revision.TitleAr).HasMaxLength(200).IsRequired();
        builder.Property(revision => revision.TitleEn).HasMaxLength(200).IsRequired();
        builder.Property(revision => revision.BodyAr).HasMaxLength(20000).IsRequired();
        builder.Property(revision => revision.BodyEn).HasMaxLength(20000).IsRequired();
        builder.HasIndex(revision => new { revision.PageId, revision.Version }).IsUnique()
            .HasDatabaseName("uq_cms_page_revisions_page_version");
        builder.HasOne<CmsPage>().WithMany().HasForeignKey(revision => revision.PageId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_cms_page_revisions_pages_page_id");
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(revision => revision.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_cms_page_revisions_users_created_by_user_id");
    }
}

internal sealed class CmsFaqConfiguration : IEntityTypeConfiguration<CmsFaq>
{
    public void Configure(EntityTypeBuilder<CmsFaq> builder)
    {
        builder.ToTable("faqs", "cms", table =>
        {
            table.HasCheckConstraint("ck_cms_faqs_display_order", "display_order BETWEEN 0 AND 10000");
            table.HasCheckConstraint(
                "ck_cms_faqs_published_display_order",
                "(published_version IS NULL AND published_display_order IS NULL) OR " +
                "(published_version IS NOT NULL AND published_display_order BETWEEN 0 AND 10000)");
            table.HasCheckConstraint("ck_cms_faqs_versions", "current_version >= 0 AND (published_version IS NULL OR published_version BETWEEN 1 AND current_version)");
        });
        builder.HasKey(faq => faq.Id).HasName("pk_cms_faqs");
        builder.Property(faq => faq.Id).ValueGeneratedNever();
        builder.HasIndex(faq => new { faq.DisplayOrder, faq.Id }).HasDatabaseName("ix_cms_faqs_display_id");
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(faq => faq.PublishedByUserId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_cms_faqs_users_published_by_user_id");
    }
}

internal sealed class CmsFaqRevisionConfiguration : IEntityTypeConfiguration<CmsFaqRevision>
{
    public void Configure(EntityTypeBuilder<CmsFaqRevision> builder)
    {
        builder.ToTable("faq_revisions", "cms", table =>
            table.HasCheckConstraint("ck_cms_faq_revisions_version", "version > 0"));
        builder.HasKey(revision => revision.Id).HasName("pk_cms_faq_revisions");
        builder.Property(revision => revision.Id).ValueGeneratedNever();
        builder.Property(revision => revision.QuestionAr).HasMaxLength(300).IsRequired();
        builder.Property(revision => revision.QuestionEn).HasMaxLength(300).IsRequired();
        builder.Property(revision => revision.AnswerAr).HasMaxLength(5000).IsRequired();
        builder.Property(revision => revision.AnswerEn).HasMaxLength(5000).IsRequired();
        builder.HasIndex(revision => new { revision.FaqId, revision.Version }).IsUnique()
            .HasDatabaseName("uq_cms_faq_revisions_faq_version");
        builder.HasOne<CmsFaq>().WithMany().HasForeignKey(revision => revision.FaqId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_cms_faq_revisions_faqs_faq_id");
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(revision => revision.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_cms_faq_revisions_users_created_by_user_id");
    }
}

internal sealed class PortfolioSettingsConfiguration : IEntityTypeConfiguration<PortfolioSettings>
{
    private static readonly DateTimeOffset SeededAt = new(2026, 8, 12, 0, 0, 0, TimeSpan.Zero);

    public void Configure(EntityTypeBuilder<PortfolioSettings> builder)
    {
        builder.ToTable("platform_settings", "cms", table =>
        {
            table.HasCheckConstraint("ck_platform_settings_singleton", $"id = '{PortfolioSettings.SingletonId:D}'::uuid");
            table.HasCheckConstraint("ck_platform_settings_featured_limit", "featured_course_limit BETWEEN 1 AND 12");
            table.HasCheckConstraint("ck_platform_settings_version", "version > 0");
            table.HasCheckConstraint("ck_platform_settings_notice", "NOT show_portfolio_notice OR (char_length(btrim(notice_ar)) > 0 AND char_length(btrim(notice_en)) > 0)");
        });
        builder.HasKey(settings => settings.Id).HasName("pk_platform_settings");
        builder.Property(settings => settings.Id).ValueGeneratedNever();
        builder.Property(settings => settings.NoticeAr).HasMaxLength(240).IsRequired();
        builder.Property(settings => settings.NoticeEn).HasMaxLength(240).IsRequired();
        builder.Property(settings => settings.Version).IsConcurrencyToken();
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(settings => settings.UpdatedByUserId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_platform_settings_users_updated_by_user_id");
        builder.HasData(CreateSeed());
    }

    private static PortfolioSettings CreateSeed()
    {
        PortfolioSettings settings = PortfolioSettings.CreateDefaults(SeededAt);
        return settings;
    }
}
