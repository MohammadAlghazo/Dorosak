using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dorosak.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase11DemoAdministrationCms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "cms");

            migrationBuilder.CreateTable(
                name: "faqs",
                schema: "cms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false),
                    published_display_order = table.Column<int>(type: "integer", nullable: true),
                    current_version = table.Column<int>(type: "integer", nullable: false),
                    published_version = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    published_by_user_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cms_faqs", x => x.id);
                    table.CheckConstraint("ck_cms_faqs_display_order", "display_order BETWEEN 0 AND 10000");
                    table.CheckConstraint("ck_cms_faqs_published_display_order", "(published_version IS NULL AND published_display_order IS NULL) OR (published_version IS NOT NULL AND published_display_order BETWEEN 0 AND 10000)");
                    table.CheckConstraint("ck_cms_faqs_versions", "current_version >= 0 AND (published_version IS NULL OR published_version BETWEEN 1 AND current_version)");
                    table.ForeignKey(
                        name: "fk_cms_faqs_users_published_by_user_id",
                        column: x => x.published_by_user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pages",
                schema: "cms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    slug = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    current_version = table.Column<int>(type: "integer", nullable: false),
                    published_version = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    published_by_user_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cms_pages", x => x.id);
                    table.CheckConstraint("ck_cms_pages_slug", "slug IN ('about', 'contact', 'privacy', 'terms')");
                    table.CheckConstraint("ck_cms_pages_versions", "current_version >= 0 AND (published_version IS NULL OR published_version BETWEEN 1 AND current_version)");
                    table.ForeignKey(
                        name: "fk_cms_pages_users_published_by_user_id",
                        column: x => x.published_by_user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "platform_settings",
                schema: "cms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    featured_course_limit = table.Column<int>(type: "integer", nullable: false),
                    show_portfolio_notice = table.Column<bool>(type: "boolean", nullable: false),
                    notice_ar = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    notice_en = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false),
                    updated_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_platform_settings", x => x.id);
                    table.CheckConstraint("ck_platform_settings_featured_limit", "featured_course_limit BETWEEN 1 AND 12");
                    table.CheckConstraint("ck_platform_settings_notice", "NOT show_portfolio_notice OR (char_length(btrim(notice_ar)) > 0 AND char_length(btrim(notice_en)) > 0)");
                    table.CheckConstraint("ck_platform_settings_singleton", "id = '018f3f0e-4380-7b1b-8f8d-b8ea9c546024'::uuid");
                    table.CheckConstraint("ck_platform_settings_version", "version > 0");
                    table.ForeignKey(
                        name: "fk_platform_settings_users_updated_by_user_id",
                        column: x => x.updated_by_user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "faq_revisions",
                schema: "cms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    faq_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    question_ar = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    question_en = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    answer_ar = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                    answer_en = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cms_faq_revisions", x => x.id);
                    table.CheckConstraint("ck_cms_faq_revisions_version", "version > 0");
                    table.ForeignKey(
                        name: "fk_cms_faq_revisions_faqs_faq_id",
                        column: x => x.faq_id,
                        principalSchema: "cms",
                        principalTable: "faqs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_cms_faq_revisions_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "page_revisions",
                schema: "cms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    page_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    title_ar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    title_en = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    body_ar = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: false),
                    body_en = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cms_page_revisions", x => x.id);
                    table.CheckConstraint("ck_cms_page_revisions_version", "version > 0");
                    table.ForeignKey(
                        name: "fk_cms_page_revisions_pages_page_id",
                        column: x => x.page_id,
                        principalSchema: "cms",
                        principalTable: "pages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_cms_page_revisions_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "cms",
                table: "platform_settings",
                columns: new[] { "id", "featured_course_limit", "notice_ar", "notice_en", "show_portfolio_notice", "updated_at", "updated_by_user_id", "version" },
                values: new object[] { new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546024"), 3, "نسخة عرض محلية بلا دفع حقيقي.", "A local showcase with no real payments.", false, new DateTimeOffset(new DateTime(2026, 8, 12, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 1L });

            migrationBuilder.CreateIndex(
                name: "ix_faq_revisions_created_by_user_id",
                schema: "cms",
                table: "faq_revisions",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "uq_cms_faq_revisions_faq_version",
                schema: "cms",
                table: "faq_revisions",
                columns: new[] { "faq_id", "version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_cms_faqs_display_id",
                schema: "cms",
                table: "faqs",
                columns: new[] { "display_order", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_faqs_published_by_user_id",
                schema: "cms",
                table: "faqs",
                column: "published_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_page_revisions_created_by_user_id",
                schema: "cms",
                table: "page_revisions",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "uq_cms_page_revisions_page_version",
                schema: "cms",
                table: "page_revisions",
                columns: new[] { "page_id", "version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_pages_published_by_user_id",
                schema: "cms",
                table: "pages",
                column: "published_by_user_id");

            migrationBuilder.CreateIndex(
                name: "uq_cms_pages_slug",
                schema: "cms",
                table: "pages",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_platform_settings_updated_by_user_id",
                schema: "cms",
                table: "platform_settings",
                column: "updated_by_user_id");

            migrationBuilder.Sql(
                """
                DO $permissions$
                BEGIN
                    IF EXISTS (SELECT FROM pg_roles WHERE rolname = 'dorosak_runtime') THEN
                        GRANT USAGE ON SCHEMA cms TO dorosak_runtime;

                        GRANT SELECT, INSERT ON cms.pages, cms.faqs TO dorosak_runtime;
                        REVOKE DELETE, TRUNCATE ON cms.pages, cms.faqs FROM dorosak_runtime;
                        GRANT UPDATE (current_version, published_version, updated_at, published_at, published_by_user_id)
                            ON cms.pages TO dorosak_runtime;
                        GRANT UPDATE (display_order, published_display_order, current_version, published_version, updated_at, published_at, published_by_user_id)
                            ON cms.faqs TO dorosak_runtime;

                        GRANT SELECT, INSERT ON cms.page_revisions, cms.faq_revisions TO dorosak_runtime;
                        REVOKE UPDATE, DELETE, TRUNCATE ON cms.page_revisions, cms.faq_revisions FROM dorosak_runtime;

                        GRANT SELECT ON cms.platform_settings TO dorosak_runtime;
                        REVOKE INSERT, DELETE, TRUNCATE ON cms.platform_settings FROM dorosak_runtime;
                        GRANT UPDATE (featured_course_limit, show_portfolio_notice, notice_ar, notice_en, version, updated_by_user_id, updated_at)
                            ON cms.platform_settings TO dorosak_runtime;
                    END IF;
                END
                $permissions$;

                UPDATE operations.schema_compatibility
                SET maximum_compatible_migration_id = '20260812221713_Phase11DemoAdministrationCms',
                    updated_at = CURRENT_TIMESTAMP
                WHERE singleton;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE operations.schema_compatibility
                SET maximum_compatible_migration_id = '20260812141430_Phase10DemoSubscriptionsCertificates',
                    updated_at = CURRENT_TIMESTAMP
                WHERE singleton;

                DO $permissions$
                BEGIN
                    IF EXISTS (SELECT FROM pg_roles WHERE rolname = 'dorosak_runtime') THEN
                        REVOKE ALL PRIVILEGES ON
                            cms.page_revisions,
                            cms.faq_revisions,
                            cms.platform_settings,
                            cms.faqs,
                            cms.pages
                            FROM dorosak_runtime;
                    END IF;
                END
                $permissions$;
                """);

            migrationBuilder.DropTable(
                name: "faq_revisions",
                schema: "cms");

            migrationBuilder.DropTable(
                name: "page_revisions",
                schema: "cms");

            migrationBuilder.DropTable(
                name: "platform_settings",
                schema: "cms");

            migrationBuilder.DropTable(
                name: "faqs",
                schema: "cms");

            migrationBuilder.DropTable(
                name: "pages",
                schema: "cms");
        }
    }
}
