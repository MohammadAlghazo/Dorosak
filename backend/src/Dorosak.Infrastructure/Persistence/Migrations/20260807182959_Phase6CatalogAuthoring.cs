using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Dorosak.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase6CatalogAuthoring : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pg_trgm;");
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS unaccent;");

            migrationBuilder.EnsureSchema(
                name: "catalog");

            migrationBuilder.EnsureSchema(
                name: "authoring");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,")
                .Annotation("Npgsql:PostgresExtension:unaccent", ",,");

            migrationBuilder.CreateTable(
                name: "audit_logs",
                schema: "operations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    target_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    target_id = table.Column<Guid>(type: "uuid", nullable: false),
                    result = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_logs", x => x.id);
                    table.ForeignKey(
                        name: "fk_audit_logs_users_actor_user_id",
                        column: x => x.actor_user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "categories",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    parent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    display_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_categories", x => x.id);
                    table.CheckConstraint("ck_categories_code", "code ~ '^[a-z0-9]+(-[a-z0-9]+)*$'");
                    table.ForeignKey(
                        name: "fk_categories_categories_parent_id",
                        column: x => x.parent_id,
                        principalSchema: "catalog",
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "courses",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    default_locale = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    deletion_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_courses", x => x.id);
                    table.CheckConstraint("ck_courses_default_locale", "default_locale IN ('ar', 'en')");
                    table.CheckConstraint("ck_courses_status", "status IN ('Draft', 'InReview', 'ChangesRequested', 'ReadyToPublish', 'Archived')");
                    table.ForeignKey(
                        name: "fk_courses_users_owner_user_id",
                        column: x => x.owner_user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tags",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tags", x => x.id);
                    table.CheckConstraint("ck_tags_code", "code ~ '^[a-z0-9]+(-[a-z0-9]+)*$'");
                });

            migrationBuilder.CreateTable(
                name: "teacher_applications",
                schema: "profiles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    headline = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    biography = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    expertise = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    motivation = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    reviewer_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewer_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_teacher_applications", x => x.id);
                    table.CheckConstraint("ck_teacher_applications_status", "status IN ('Pending', 'InReview', 'Approved', 'Rejected', 'Withdrawn')");
                    table.ForeignKey(
                        name: "fk_teacher_applications_users_reviewer_user_id",
                        column: x => x.reviewer_user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_teacher_applications_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "category_localizations",
                schema: "catalog",
                columns: table => new
                {
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    locale = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_category_localizations", x => new { x.category_id, x.locale });
                    table.CheckConstraint("ck_category_localizations_locale", "locale IN ('ar', 'en')");
                    table.ForeignKey(
                        name: "fk_category_localizations_categories_category_id",
                        column: x => x.category_id,
                        principalSchema: "catalog",
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "course_categories",
                schema: "catalog",
                columns: table => new
                {
                    course_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_course_categories", x => new { x.course_id, x.category_id });
                    table.ForeignKey(
                        name: "fk_course_categories_categories_category_id",
                        column: x => x.category_id,
                        principalSchema: "catalog",
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_course_categories_courses_course_id",
                        column: x => x.course_id,
                        principalSchema: "catalog",
                        principalTable: "courses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "course_drafts",
                schema: "authoring",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    course_id = table.Column<Guid>(type: "uuid", nullable: false),
                    level = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_course_drafts", x => x.id);
                    table.CheckConstraint("ck_course_drafts_level", "level IN ('Beginner', 'Intermediate', 'Advanced', 'AllLevels')");
                    table.CheckConstraint("ck_course_drafts_version", "version > 0");
                    table.ForeignKey(
                        name: "fk_course_drafts_courses_course_id",
                        column: x => x.course_id,
                        principalSchema: "catalog",
                        principalTable: "courses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "course_instructors",
                schema: "catalog",
                columns: table => new
                {
                    course_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    added_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_course_instructors", x => new { x.course_id, x.user_id });
                    table.CheckConstraint("ck_course_instructors_role", "role IN ('Editor', 'CoInstructor', 'Reviewer')");
                    table.ForeignKey(
                        name: "fk_course_instructors_courses_course_id",
                        column: x => x.course_id,
                        principalSchema: "catalog",
                        principalTable: "courses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_course_instructors_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "course_slugs",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    course_id = table.Column<Guid>(type: "uuid", nullable: false),
                    locale = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    slug = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    is_current = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    retired_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_course_slugs", x => x.id);
                    table.UniqueConstraint("ak_course_slugs_id_course_locale", x => new { x.id, x.course_id, x.locale });
                    table.CheckConstraint("ck_course_slugs_locale", "locale IN ('ar', 'en')");
                    table.CheckConstraint("ck_course_slugs_value", "slug ~ '^[a-z0-9]+(-[a-z0-9]+)*$'");
                    table.ForeignKey(
                        name: "fk_course_slugs_courses_course_id",
                        column: x => x.course_id,
                        principalSchema: "catalog",
                        principalTable: "courses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "course_tags",
                schema: "catalog",
                columns: table => new
                {
                    course_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tag_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_course_tags", x => new { x.course_id, x.tag_id });
                    table.ForeignKey(
                        name: "fk_course_tags_courses_course_id",
                        column: x => x.course_id,
                        principalSchema: "catalog",
                        principalTable: "courses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_course_tags_tags_tag_id",
                        column: x => x.tag_id,
                        principalSchema: "catalog",
                        principalTable: "tags",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tag_localizations",
                schema: "catalog",
                columns: table => new
                {
                    tag_id = table.Column<Guid>(type: "uuid", nullable: false),
                    locale = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tag_localizations", x => new { x.tag_id, x.locale });
                    table.CheckConstraint("ck_tag_localizations_locale", "locale IN ('ar', 'en')");
                    table.ForeignKey(
                        name: "fk_tag_localizations_tags_tag_id",
                        column: x => x.tag_id,
                        principalSchema: "catalog",
                        principalTable: "tags",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "teacher_profiles",
                schema: "profiles",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    application_id = table.Column<Guid>(type: "uuid", nullable: false),
                    headline = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    biography = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    expertise = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    approved_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    approved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_teacher_profiles", x => x.user_id);
                    table.ForeignKey(
                        name: "fk_teacher_profiles_applications_application_id",
                        column: x => x.application_id,
                        principalSchema: "profiles",
                        principalTable: "teacher_applications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_teacher_profiles_users_approved_by_user_id",
                        column: x => x.approved_by_user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_teacher_profiles_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "publication_reviews",
                schema: "authoring",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    course_id = table.Column<Guid>(type: "uuid", nullable: false),
                    draft_id = table.Column<Guid>(type: "uuid", nullable: false),
                    draft_version = table.Column<long>(type: "bigint", nullable: false),
                    requested_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    reviewer_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewer_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    requested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_publication_reviews", x => x.id);
                    table.CheckConstraint("ck_publication_reviews_status", "status IN ('Pending', 'ChangesRequested', 'Approved', 'Withdrawn')");
                    table.ForeignKey(
                        name: "fk_publication_reviews_course_drafts_draft_id",
                        column: x => x.draft_id,
                        principalSchema: "authoring",
                        principalTable: "course_drafts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_publication_reviews_courses_course_id",
                        column: x => x.course_id,
                        principalSchema: "catalog",
                        principalTable: "courses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_publication_reviews_users_requested_by_user_id",
                        column: x => x.requested_by_user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_publication_reviews_users_reviewer_user_id",
                        column: x => x.reviewer_user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "course_localizations",
                schema: "catalog",
                columns: table => new
                {
                    course_id = table.Column<Guid>(type: "uuid", nullable: false),
                    locale = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    subtitle = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    description = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    current_slug_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_course_localizations", x => new { x.course_id, x.locale });
                    table.CheckConstraint("ck_course_localizations_locale", "locale IN ('ar', 'en')");
                    table.ForeignKey(
                        name: "fk_course_localizations_courses_course_id",
                        column: x => x.course_id,
                        principalSchema: "catalog",
                        principalTable: "courses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_course_localizations_current_slug",
                        columns: x => new { x.current_slug_id, x.course_id, x.locale },
                        principalSchema: "catalog",
                        principalTable: "course_slugs",
                        principalColumns: new[] { "id", "course_id", "locale" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "lesson_revisions",
                schema: "authoring",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    lesson_id = table.Column<Guid>(type: "uuid", nullable: false),
                    draft_version = table.Column<long>(type: "bigint", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    lesson_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    content = table.Column<string>(type: "character varying(100000)", maxLength: 100000, nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lesson_revisions", x => x.id);
                    table.CheckConstraint("ck_lesson_revisions_position", "position >= 0");
                    table.CheckConstraint("ck_lesson_revisions_type", "lesson_type IN ('Video', 'Article', 'Document', 'Quiz', 'Assignment')");
                    table.CheckConstraint("ck_lesson_revisions_version", "draft_version > 0");
                });

            migrationBuilder.CreateTable(
                name: "lessons",
                schema: "authoring",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    draft_id = table.Column<Guid>(type: "uuid", nullable: false),
                    section_id = table.Column<Guid>(type: "uuid", nullable: false),
                    current_revision_id = table.Column<Guid>(type: "uuid", nullable: true),
                    position = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    removed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lessons", x => x.id);
                    table.CheckConstraint("ck_lessons_position", "position >= 0");
                    table.ForeignKey(
                        name: "fk_lessons_course_drafts_draft_id",
                        column: x => x.draft_id,
                        principalSchema: "authoring",
                        principalTable: "course_drafts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_lessons_current_revision_id",
                        column: x => x.current_revision_id,
                        principalSchema: "authoring",
                        principalTable: "lesson_revisions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "section_revisions",
                schema: "authoring",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    section_id = table.Column<Guid>(type: "uuid", nullable: false),
                    draft_version = table.Column<long>(type: "bigint", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_section_revisions", x => x.id);
                    table.CheckConstraint("ck_section_revisions_position", "position >= 0");
                    table.CheckConstraint("ck_section_revisions_version", "draft_version > 0");
                });

            migrationBuilder.CreateTable(
                name: "sections",
                schema: "authoring",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    draft_id = table.Column<Guid>(type: "uuid", nullable: false),
                    current_revision_id = table.Column<Guid>(type: "uuid", nullable: true),
                    position = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    removed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sections", x => x.id);
                    table.CheckConstraint("ck_sections_position", "position >= 0");
                    table.ForeignKey(
                        name: "fk_sections_course_drafts_draft_id",
                        column: x => x.draft_id,
                        principalSchema: "authoring",
                        principalTable: "course_drafts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sections_current_revision_id",
                        column: x => x.current_revision_id,
                        principalSchema: "authoring",
                        principalTable: "section_revisions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "catalog",
                table: "categories",
                columns: new[] { "id", "code", "created_at", "display_order", "is_active", "parent_id", "updated_at" },
                values: new object[,]
                {
                    { new Guid("01989b44-0000-7000-8000-000000000001"), "technology", new DateTimeOffset(new DateTime(2026, 8, 7, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 10, true, null, new DateTimeOffset(new DateTime(2026, 8, 7, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("01989b44-0000-7000-8000-000000000002"), "business", new DateTimeOffset(new DateTime(2026, 8, 7, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 20, true, null, new DateTimeOffset(new DateTime(2026, 8, 7, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("01989b44-0000-7000-8000-000000000003"), "data", new DateTimeOffset(new DateTime(2026, 8, 7, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 30, true, null, new DateTimeOffset(new DateTime(2026, 8, 7, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("01989b44-0000-7000-8000-000000000004"), "personal-development", new DateTimeOffset(new DateTime(2026, 8, 7, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 40, true, null, new DateTimeOffset(new DateTime(2026, 8, 7, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) }
                });

            migrationBuilder.InsertData(
                schema: "catalog",
                table: "category_localizations",
                columns: new[] { "category_id", "locale", "name" },
                values: new object[,]
                {
                    { new Guid("01989b44-0000-7000-8000-000000000001"), "ar", "التكنولوجيا" },
                    { new Guid("01989b44-0000-7000-8000-000000000001"), "en", "Technology" },
                    { new Guid("01989b44-0000-7000-8000-000000000002"), "ar", "الأعمال" },
                    { new Guid("01989b44-0000-7000-8000-000000000002"), "en", "Business" },
                    { new Guid("01989b44-0000-7000-8000-000000000003"), "ar", "البيانات" },
                    { new Guid("01989b44-0000-7000-8000-000000000003"), "en", "Data" },
                    { new Guid("01989b44-0000-7000-8000-000000000004"), "ar", "التطوير الشخصي" },
                    { new Guid("01989b44-0000-7000-8000-000000000004"), "en", "Personal Development" }
                });

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_actor_occurred_id",
                schema: "operations",
                table: "audit_logs",
                columns: new[] { "actor_user_id", "occurred_at", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_target_occurred",
                schema: "operations",
                table: "audit_logs",
                columns: new[] { "target_type", "target_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_categories_parent_order_id",
                schema: "catalog",
                table: "categories",
                columns: new[] { "parent_id", "display_order", "id" });

            migrationBuilder.CreateIndex(
                name: "uq_categories_code",
                schema: "catalog",
                table: "categories",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_course_categories_category_course",
                schema: "catalog",
                table: "course_categories",
                columns: new[] { "category_id", "course_id" });

            migrationBuilder.CreateIndex(
                name: "uq_course_drafts_course_id",
                schema: "authoring",
                table: "course_drafts",
                column: "course_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_course_instructors_user_id",
                schema: "catalog",
                table: "course_instructors",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_course_localizations_current_slug_id_course_id_locale",
                schema: "catalog",
                table: "course_localizations",
                columns: new[] { "current_slug_id", "course_id", "locale" });

            migrationBuilder.CreateIndex(
                name: "uq_course_slugs_current_course_locale",
                schema: "catalog",
                table: "course_slugs",
                columns: new[] { "course_id", "locale" },
                unique: true,
                filter: "is_current");

            migrationBuilder.CreateIndex(
                name: "uq_course_slugs_locale_slug",
                schema: "catalog",
                table: "course_slugs",
                columns: new[] { "locale", "slug" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_course_tags_tag_course",
                schema: "catalog",
                table: "course_tags",
                columns: new[] { "tag_id", "course_id" });

            migrationBuilder.CreateIndex(
                name: "ix_courses_owner_updated_id",
                schema: "catalog",
                table: "courses",
                columns: new[] { "owner_user_id", "updated_at", "id" },
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "uq_lesson_revisions_lesson_version",
                schema: "authoring",
                table: "lesson_revisions",
                columns: new[] { "lesson_id", "draft_version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_lessons_current_revision_id",
                schema: "authoring",
                table: "lessons",
                column: "current_revision_id");

            migrationBuilder.CreateIndex(
                name: "ix_lessons_draft_id",
                schema: "authoring",
                table: "lessons",
                column: "draft_id");

            migrationBuilder.CreateIndex(
                name: "uq_lessons_active_section_position",
                schema: "authoring",
                table: "lessons",
                columns: new[] { "section_id", "position" },
                unique: true,
                filter: "removed_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_publication_reviews_course_requested_id",
                schema: "authoring",
                table: "publication_reviews",
                columns: new[] { "course_id", "requested_at", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_publication_reviews_draft_id",
                schema: "authoring",
                table: "publication_reviews",
                column: "draft_id");

            migrationBuilder.CreateIndex(
                name: "ix_publication_reviews_requested_by_user_id",
                schema: "authoring",
                table: "publication_reviews",
                column: "requested_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_publication_reviews_reviewer_user_id",
                schema: "authoring",
                table: "publication_reviews",
                column: "reviewer_user_id");

            migrationBuilder.CreateIndex(
                name: "uq_publication_reviews_pending_course",
                schema: "authoring",
                table: "publication_reviews",
                column: "course_id",
                unique: true,
                filter: "status = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "uq_section_revisions_section_version",
                schema: "authoring",
                table: "section_revisions",
                columns: new[] { "section_id", "draft_version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sections_current_revision_id",
                schema: "authoring",
                table: "sections",
                column: "current_revision_id");

            migrationBuilder.CreateIndex(
                name: "uq_sections_active_position",
                schema: "authoring",
                table: "sections",
                columns: new[] { "draft_id", "position" },
                unique: true,
                filter: "removed_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "uq_tags_code",
                schema: "catalog",
                table: "tags",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_teacher_applications_reviewer_user_id",
                schema: "profiles",
                table: "teacher_applications",
                column: "reviewer_user_id");

            migrationBuilder.CreateIndex(
                name: "uq_teacher_applications_active_user",
                schema: "profiles",
                table: "teacher_applications",
                column: "user_id",
                unique: true,
                filter: "status IN ('Pending', 'InReview')");

            migrationBuilder.CreateIndex(
                name: "ix_teacher_profiles_approved_by_user_id",
                schema: "profiles",
                table: "teacher_profiles",
                column: "approved_by_user_id");

            migrationBuilder.CreateIndex(
                name: "uq_teacher_profiles_application_id",
                schema: "profiles",
                table: "teacher_profiles",
                column: "application_id",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_lesson_revisions_lessons_lesson_id",
                schema: "authoring",
                table: "lesson_revisions",
                column: "lesson_id",
                principalSchema: "authoring",
                principalTable: "lessons",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql(
                """
                UPDATE operations.schema_compatibility
                SET maximum_compatible_migration_id = '20260807182959_Phase6CatalogAuthoring',
                    updated_at = CURRENT_TIMESTAMP
                WHERE singleton;
                """);

            migrationBuilder.Sql(
                """
                DO $permissions$
                BEGIN
                    IF EXISTS (SELECT FROM pg_roles WHERE rolname = 'dorosak_runtime') THEN
                        GRANT USAGE ON SCHEMA catalog, authoring TO dorosak_runtime;
                        GRANT SELECT, INSERT, UPDATE, DELETE
                            ON ALL TABLES IN SCHEMA catalog, authoring
                            TO dorosak_runtime;
                        REVOKE UPDATE, DELETE, TRUNCATE ON operations.audit_logs FROM dorosak_runtime;
                        GRANT SELECT, INSERT ON operations.audit_logs TO dorosak_runtime;
                    END IF;
                END
                $permissions$;
                """);

            migrationBuilder.AddForeignKey(
                name: "fk_lessons_sections_section_id",
                schema: "authoring",
                table: "lessons",
                column: "section_id",
                principalSchema: "authoring",
                principalTable: "sections",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_section_revisions_sections_section_id",
                schema: "authoring",
                table: "section_revisions",
                column: "section_id",
                principalSchema: "authoring",
                principalTable: "sections",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE operations.schema_compatibility
                SET maximum_compatible_migration_id = '20260807113555_AddPendingEmailChange',
                    updated_at = CURRENT_TIMESTAMP
                WHERE singleton;
                """);
            migrationBuilder.DropForeignKey(
                name: "fk_course_drafts_courses_course_id",
                schema: "authoring",
                table: "course_drafts");

            migrationBuilder.DropForeignKey(
                name: "fk_lesson_revisions_lessons_lesson_id",
                schema: "authoring",
                table: "lesson_revisions");

            migrationBuilder.DropForeignKey(
                name: "fk_sections_course_drafts_draft_id",
                schema: "authoring",
                table: "sections");

            migrationBuilder.DropForeignKey(
                name: "fk_section_revisions_sections_section_id",
                schema: "authoring",
                table: "section_revisions");

            migrationBuilder.DropTable(
                name: "audit_logs",
                schema: "operations");

            migrationBuilder.DropTable(
                name: "category_localizations",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "course_categories",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "course_instructors",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "course_localizations",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "course_tags",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "publication_reviews",
                schema: "authoring");

            migrationBuilder.DropTable(
                name: "tag_localizations",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "teacher_profiles",
                schema: "profiles");

            migrationBuilder.DropTable(
                name: "categories",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "course_slugs",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "tags",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "teacher_applications",
                schema: "profiles");

            migrationBuilder.DropTable(
                name: "courses",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "lessons",
                schema: "authoring");

            migrationBuilder.DropTable(
                name: "lesson_revisions",
                schema: "authoring");

            migrationBuilder.DropTable(
                name: "course_drafts",
                schema: "authoring");

            migrationBuilder.DropTable(
                name: "sections",
                schema: "authoring");

            migrationBuilder.DropTable(
                name: "section_revisions",
                schema: "authoring");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:pg_trgm", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:unaccent", ",,");
        }
    }
}
