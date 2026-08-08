using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dorosak.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase8ReleaseCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE operations.schema_compatibility
                SET maximum_compatible_migration_id = '20260808134406_Phase8ReleaseCatalog',
                    updated_at = CURRENT_TIMESTAMP
                WHERE singleton;
                """);

            migrationBuilder.DropCheckConstraint(
                name: "ck_courses_status",
                schema: "catalog",
                table: "courses");

            migrationBuilder.EnsureSchema(
                name: "assessment");

            migrationBuilder.EnsureSchema(
                name: "learning");

            migrationBuilder.AddColumn<Guid>(
                name: "assignment_version_id",
                schema: "authoring",
                table: "lesson_revisions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "quiz_version_id",
                schema: "authoring",
                table: "lesson_revisions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "active_release_id",
                schema: "catalog",
                table: "courses",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "projection_generation",
                schema: "catalog",
                table: "courses",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "assignments",
                schema: "assessment",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    course_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lesson_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_assignments", x => x.id);
                    table.ForeignKey(
                        name: "fk_assignments_course_lessons_lesson_id",
                        column: x => x.lesson_id,
                        principalSchema: "authoring",
                        principalTable: "lessons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_assignments_courses_course_id",
                        column: x => x.course_id,
                        principalSchema: "catalog",
                        principalTable: "courses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_assignments_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "course_releases",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    course_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_draft_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_draft_version = table.Column<long>(type: "bigint", nullable: false),
                    release_number = table.Column<int>(type: "integer", nullable: false),
                    default_locale = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    manifest_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    published_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    state = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_course_releases", x => x.id);
                    table.UniqueConstraint("ak_course_releases_id_course", x => new { x.id, x.course_id });
                    table.CheckConstraint("ck_course_releases_default_locale", "default_locale IN ('ar', 'en')");
                    table.CheckConstraint("ck_course_releases_draft_version", "source_draft_version > 0");
                    table.CheckConstraint("ck_course_releases_manifest_hash", "manifest_hash ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("ck_course_releases_number", "release_number > 0");
                    table.CheckConstraint("ck_course_releases_state", "state IN ('Draft', 'Active', 'Superseded', 'Unpublished')");
                    table.ForeignKey(
                        name: "fk_course_releases_course_drafts_source_draft_id",
                        column: x => x.source_draft_id,
                        principalSchema: "authoring",
                        principalTable: "course_drafts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_course_releases_courses_course_id",
                        column: x => x.course_id,
                        principalSchema: "catalog",
                        principalTable: "courses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_course_releases_users_published_by_user_id",
                        column: x => x.published_by_user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "entitlements",
                schema: "learning",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    course_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    granted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_entitlements", x => x.id);
                    table.CheckConstraint("ck_entitlements_source", "source = 'Free'");
                    table.CheckConstraint("ck_entitlements_status", "status IN ('Active', 'Revoked', 'Expired')");
                    table.ForeignKey(
                        name: "fk_entitlements_courses_course_id",
                        column: x => x.course_id,
                        principalSchema: "catalog",
                        principalTable: "courses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_entitlements_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "projection_state",
                schema: "catalog",
                columns: table => new
                {
                    singleton = table.Column<bool>(type: "boolean", nullable: false),
                    generation = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_catalog_projection_state", x => x.singleton);
                    table.CheckConstraint("ck_catalog_projection_state_singleton", "singleton");
                });

            migrationBuilder.CreateTable(
                name: "quizzes",
                schema: "assessment",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    course_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lesson_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_quizzes", x => x.id);
                    table.ForeignKey(
                        name: "fk_quizzes_courses_course_id",
                        column: x => x.course_id,
                        principalSchema: "catalog",
                        principalTable: "courses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_quizzes_lessons_lesson_id",
                        column: x => x.lesson_id,
                        principalSchema: "authoring",
                        principalTable: "lessons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_quizzes_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "assignment_versions",
                schema: "assessment",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    assignment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_number = table.Column<int>(type: "integer", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    instructions = table.Column<string>(type: "character varying(100000)", maxLength: 100000, nullable: false),
                    deadline = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    allow_multiple_submissions = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ready_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_assignment_versions", x => x.id);
                    table.CheckConstraint("ck_assignment_versions_number", "version_number > 0");
                    table.CheckConstraint("ck_assignment_versions_status", "status IN ('Draft', 'Ready')");
                    table.ForeignKey(
                        name: "fk_assignment_versions_assignments_assignment_id",
                        column: x => x.assignment_id,
                        principalSchema: "assessment",
                        principalTable: "assignments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "catalog_documents",
                schema: "catalog",
                columns: table => new
                {
                    release_id = table.Column<Guid>(type: "uuid", nullable: false),
                    locale = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    course_id = table.Column<Guid>(type: "uuid", nullable: false),
                    slug = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    summary = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    description = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    language = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    level = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    duration_minutes = table.Column<int>(type: "integer", nullable: false),
                    search_text = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: false),
                    normalized_arabic_text = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: false),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    projection_generation = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_catalog_documents", x => new { x.release_id, x.locale });
                    table.CheckConstraint("ck_catalog_documents_duration", "duration_minutes >= 0");
                    table.CheckConstraint("ck_catalog_documents_generation", "projection_generation > 0");
                    table.CheckConstraint("ck_catalog_documents_locale", "locale IN ('ar', 'en')");
                    table.ForeignKey(
                        name: "fk_catalog_documents_course_releases_release_id_course_id",
                        columns: x => new { x.release_id, x.course_id },
                        principalSchema: "catalog",
                        principalTable: "course_releases",
                        principalColumns: new[] { "id", "course_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "course_release_instructors",
                schema: "catalog",
                columns: table => new
                {
                    release_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    display_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_course_release_instructors", x => new { x.release_id, x.user_id });
                    table.CheckConstraint("ck_course_release_instructors_position", "position >= 0");
                    table.ForeignKey(
                        name: "fk_course_release_instructors_course_releases_release_id",
                        column: x => x.release_id,
                        principalSchema: "catalog",
                        principalTable: "course_releases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "course_release_localizations",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    release_id = table.Column<Guid>(type: "uuid", nullable: false),
                    locale = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    slug = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    subtitle = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    description = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_course_release_localizations", x => x.id);
                    table.CheckConstraint("ck_course_release_localizations_locale", "locale IN ('ar', 'en')");
                    table.ForeignKey(
                        name: "fk_course_release_localizations_course_releases_release_id",
                        column: x => x.release_id,
                        principalSchema: "catalog",
                        principalTable: "course_releases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "course_release_sections",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    release_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_section_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_revision_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_course_release_sections", x => x.id);
                    table.UniqueConstraint("ak_course_release_sections_id_release", x => new { x.id, x.release_id });
                    table.CheckConstraint("ck_course_release_sections_position", "position >= 0");
                    table.ForeignKey(
                        name: "fk_course_release_sections_releases_release_id",
                        column: x => x.release_id,
                        principalSchema: "catalog",
                        principalTable: "course_releases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_course_release_sections_revisions_source_revision_id",
                        column: x => x.source_revision_id,
                        principalSchema: "authoring",
                        principalTable: "section_revisions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_course_release_sections_sections_source_section_id",
                        column: x => x.source_section_id,
                        principalSchema: "authoring",
                        principalTable: "sections",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "course_release_taxonomy",
                schema: "catalog",
                columns: table => new
                {
                    release_id = table.Column<Guid>(type: "uuid", nullable: false),
                    term_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_category = table.Column<bool>(type: "boolean", nullable: false),
                    code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_course_release_taxonomy", x => new { x.release_id, x.term_id, x.is_category });
                    table.ForeignKey(
                        name: "fk_course_release_taxonomy_course_releases_release_id",
                        column: x => x.release_id,
                        principalSchema: "catalog",
                        principalTable: "course_releases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "enrollments",
                schema: "learning",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    course_id = table.Column<Guid>(type: "uuid", nullable: false),
                    release_id = table.Column<Guid>(type: "uuid", nullable: false),
                    entitlement_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    enrolled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_accessed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_enrollments", x => x.id);
                    table.CheckConstraint("ck_enrollments_status", "status IN ('Active', 'Completed', 'Suspended', 'Revoked', 'Expired')");
                    table.ForeignKey(
                        name: "fk_enrollments_course_releases_release_id_course_id",
                        columns: x => new { x.release_id, x.course_id },
                        principalSchema: "catalog",
                        principalTable: "course_releases",
                        principalColumns: new[] { "id", "course_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_enrollments_courses_course_id",
                        column: x => x.course_id,
                        principalSchema: "catalog",
                        principalTable: "courses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_enrollments_entitlements_entitlement_id",
                        column: x => x.entitlement_id,
                        principalSchema: "learning",
                        principalTable: "entitlements",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_enrollments_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "quiz_versions",
                schema: "assessment",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    quiz_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_number = table.Column<int>(type: "integer", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    attempt_limit = table.Column<int>(type: "integer", nullable: false),
                    duration_minutes = table.Column<int>(type: "integer", nullable: true),
                    deadline = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    pass_score = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ready_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_quiz_versions", x => x.id);
                    table.CheckConstraint("ck_quiz_versions_attempt_limit", "attempt_limit BETWEEN 1 AND 100");
                    table.CheckConstraint("ck_quiz_versions_duration", "duration_minutes IS NULL OR duration_minutes BETWEEN 1 AND 1440");
                    table.CheckConstraint("ck_quiz_versions_number", "version_number > 0");
                    table.CheckConstraint("ck_quiz_versions_pass_score", "pass_score BETWEEN 0 AND 100");
                    table.CheckConstraint("ck_quiz_versions_status", "status IN ('Draft', 'Ready')");
                    table.ForeignKey(
                        name: "fk_quiz_versions_quizzes_quiz_id",
                        column: x => x.quiz_id,
                        principalSchema: "assessment",
                        principalTable: "quizzes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "course_release_lessons",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    release_id = table.Column<Guid>(type: "uuid", nullable: false),
                    section_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_lesson_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_revision_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    lesson_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    content = table.Column<string>(type: "character varying(100000)", maxLength: 100000, nullable: false),
                    media_asset_id = table.Column<Guid>(type: "uuid", nullable: true),
                    completion_requirement = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_course_release_lessons", x => x.id);
                    table.UniqueConstraint("ak_course_release_lessons_id_release", x => new { x.id, x.release_id });
                    table.CheckConstraint("ck_course_release_lessons_completion", "completion_requirement BETWEEN 0 AND 1");
                    table.CheckConstraint("ck_course_release_lessons_position", "position >= 0");
                    table.CheckConstraint("ck_course_release_lessons_type", "lesson_type IN ('Video', 'Article', 'Document', 'Quiz', 'Assignment')");
                    table.ForeignKey(
                        name: "fk_course_release_lessons_lessons_source_lesson_id",
                        column: x => x.source_lesson_id,
                        principalSchema: "authoring",
                        principalTable: "lessons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_course_release_lessons_media_assets_media_asset_id",
                        column: x => x.media_asset_id,
                        principalSchema: "media",
                        principalTable: "media_assets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_course_release_lessons_releases_release_id",
                        column: x => x.release_id,
                        principalSchema: "catalog",
                        principalTable: "course_releases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_course_release_lessons_revisions_source_revision_id",
                        column: x => x.source_revision_id,
                        principalSchema: "authoring",
                        principalTable: "lesson_revisions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_course_release_lessons_sections_section_id",
                        columns: x => new { x.section_id, x.release_id },
                        principalSchema: "catalog",
                        principalTable: "course_release_sections",
                        principalColumns: new[] { "id", "release_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "assignment_submissions",
                schema: "assessment",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    enrollment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assignment_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    submission_number = table.Column<int>(type: "integer", nullable: false),
                    text = table.Column<string>(type: "character varying(100000)", maxLength: 100000, nullable: false),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_assignment_submissions", x => x.id);
                    table.CheckConstraint("ck_assignment_submissions_number", "submission_number > 0");
                    table.ForeignKey(
                        name: "fk_assignment_submissions_assignment_versions_assignment_versi",
                        column: x => x.assignment_version_id,
                        principalSchema: "assessment",
                        principalTable: "assignment_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_assignment_submissions_enrollments_enrollment_id",
                        column: x => x.enrollment_id,
                        principalSchema: "learning",
                        principalTable: "enrollments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "course_completions",
                schema: "learning",
                columns: table => new
                {
                    enrollment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    course_id = table.Column<Guid>(type: "uuid", nullable: false),
                    release_id = table.Column<Guid>(type: "uuid", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_course_completions", x => x.enrollment_id);
                    table.ForeignKey(
                        name: "fk_course_completions_course_releases_release_id_course_id",
                        columns: x => new { x.release_id, x.course_id },
                        principalSchema: "catalog",
                        principalTable: "course_releases",
                        principalColumns: new[] { "id", "course_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_course_completions_courses_course_id",
                        column: x => x.course_id,
                        principalSchema: "catalog",
                        principalTable: "courses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_course_completions_enrollments_enrollment_id",
                        column: x => x.enrollment_id,
                        principalSchema: "learning",
                        principalTable: "enrollments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "questions",
                schema: "assessment",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    quiz_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    prompt = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    points = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    accepted_answer = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_questions", x => x.id);
                    table.CheckConstraint("ck_questions_points", "points > 0");
                    table.CheckConstraint("ck_questions_position", "position >= 0");
                    table.CheckConstraint("ck_questions_type", "type IN ('SingleChoice', 'MultipleChoice', 'TrueFalse', 'ShortAnswer')");
                    table.ForeignKey(
                        name: "fk_questions_quiz_versions_quiz_version_id",
                        column: x => x.quiz_version_id,
                        principalSchema: "assessment",
                        principalTable: "quiz_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "quiz_attempts",
                schema: "assessment",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    enrollment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quiz_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    attempt_number = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    score = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    passed = table.Column<bool>(type: "boolean", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_quiz_attempts", x => x.id);
                    table.CheckConstraint("ck_quiz_attempts_number", "attempt_number > 0");
                    table.CheckConstraint("ck_quiz_attempts_score", "score IS NULL OR score BETWEEN 0 AND 100");
                    table.CheckConstraint("ck_quiz_attempts_status", "status IN ('InProgress', 'Submitted', 'PendingManualGrade', 'Graded')");
                    table.ForeignKey(
                        name: "fk_quiz_attempts_enrollments_enrollment_id",
                        column: x => x.enrollment_id,
                        principalSchema: "learning",
                        principalTable: "enrollments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_quiz_attempts_quiz_versions_quiz_version_id",
                        column: x => x.quiz_version_id,
                        principalSchema: "assessment",
                        principalTable: "quiz_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "bookmarks",
                schema: "learning",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    enrollment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lesson_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bookmarks", x => new { x.user_id, x.enrollment_id, x.lesson_id });
                    table.ForeignKey(
                        name: "fk_bookmarks_course_release_lessons_lesson_id",
                        column: x => x.lesson_id,
                        principalSchema: "catalog",
                        principalTable: "course_release_lessons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_bookmarks_enrollments_enrollment_id",
                        column: x => x.enrollment_id,
                        principalSchema: "learning",
                        principalTable: "enrollments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_bookmarks_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "course_release_assessments",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    release_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lesson_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    quiz_version_id = table.Column<Guid>(type: "uuid", nullable: true),
                    assignment_version_id = table.Column<Guid>(type: "uuid", nullable: true),
                    position = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_course_release_assessments", x => x.id);
                    table.UniqueConstraint("ak_course_release_assessments_id_release", x => new { x.id, x.release_id });
                    table.CheckConstraint("ck_course_release_assessments_position", "position >= 0");
                    table.CheckConstraint("ck_course_release_assessments_type", "type IN ('Quiz', 'Assignment')");
                    table.CheckConstraint("ck_course_release_assessments_version", "(type = 'Quiz' AND quiz_version_id IS NOT NULL AND assignment_version_id IS NULL) OR (type = 'Assignment' AND quiz_version_id IS NULL AND assignment_version_id IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_course_release_assessments_assignment_versions_version_id",
                        column: x => x.assignment_version_id,
                        principalSchema: "assessment",
                        principalTable: "assignment_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_course_release_assessments_lessons_lesson_id",
                        columns: x => new { x.lesson_id, x.release_id },
                        principalSchema: "catalog",
                        principalTable: "course_release_lessons",
                        principalColumns: new[] { "id", "release_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_course_release_assessments_quiz_versions_version_id",
                        column: x => x.quiz_version_id,
                        principalSchema: "assessment",
                        principalTable: "quiz_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_course_release_assessments_releases_release_id",
                        column: x => x.release_id,
                        principalSchema: "catalog",
                        principalTable: "course_releases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "course_release_captions",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    release_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lesson_id = table.Column<Guid>(type: "uuid", nullable: false),
                    asset_id = table.Column<Guid>(type: "uuid", nullable: false),
                    caption_id = table.Column<Guid>(type: "uuid", nullable: false),
                    locale = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    label = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_course_release_captions", x => x.id);
                    table.CheckConstraint("ck_course_release_captions_locale", "char_length(locale) BETWEEN 2 AND 16");
                    table.ForeignKey(
                        name: "fk_course_release_captions_caption_tracks_caption_id",
                        column: x => x.caption_id,
                        principalSchema: "media",
                        principalTable: "caption_tracks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_course_release_captions_course_release_lessons_lesson_id_re",
                        columns: x => new { x.lesson_id, x.release_id },
                        principalSchema: "catalog",
                        principalTable: "course_release_lessons",
                        principalColumns: new[] { "id", "release_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_course_release_captions_course_releases_release_id",
                        column: x => x.release_id,
                        principalSchema: "catalog",
                        principalTable: "course_releases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_course_release_captions_media_assets_asset_id",
                        column: x => x.asset_id,
                        principalSchema: "media",
                        principalTable: "media_assets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "course_release_media_variants",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    release_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lesson_id = table.Column<Guid>(type: "uuid", nullable: false),
                    asset_id = table.Column<Guid>(type: "uuid", nullable: false),
                    variant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    content_type = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    bytes = table.Column<long>(type: "bigint", nullable: false),
                    width = table.Column<int>(type: "integer", nullable: true),
                    height = table.Column<int>(type: "integer", nullable: true),
                    duration_seconds = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_course_release_media_variants", x => x.id);
                    table.CheckConstraint("ck_course_release_media_variants_bytes", "bytes > 0");
                    table.ForeignKey(
                        name: "fk_course_release_media_variants_course_release_lessons_lesson",
                        columns: x => new { x.lesson_id, x.release_id },
                        principalSchema: "catalog",
                        principalTable: "course_release_lessons",
                        principalColumns: new[] { "id", "release_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_course_release_media_variants_course_releases_release_id",
                        column: x => x.release_id,
                        principalSchema: "catalog",
                        principalTable: "course_releases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_course_release_media_variants_media_assets_asset_id",
                        column: x => x.asset_id,
                        principalSchema: "media",
                        principalTable: "media_assets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_course_release_media_variants_media_variants_variant_id",
                        column: x => x.variant_id,
                        principalSchema: "media",
                        principalTable: "media_variants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "lesson_progress",
                schema: "learning",
                columns: table => new
                {
                    enrollment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lesson_id = table.Column<Guid>(type: "uuid", nullable: false),
                    last_sequence = table.Column<long>(type: "bigint", nullable: false),
                    position_seconds = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    watched_intervals = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: false),
                    is_completed = table.Column<bool>(type: "boolean", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lesson_progress", x => new { x.enrollment_id, x.lesson_id });
                    table.CheckConstraint("ck_lesson_progress_position", "position_seconds >= 0");
                    table.CheckConstraint("ck_lesson_progress_sequence", "last_sequence >= 0");
                    table.ForeignKey(
                        name: "fk_lesson_progress_course_release_lessons_lesson_id",
                        column: x => x.lesson_id,
                        principalSchema: "catalog",
                        principalTable: "course_release_lessons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_lesson_progress_enrollments_enrollment_id",
                        column: x => x.enrollment_id,
                        principalSchema: "learning",
                        principalTable: "enrollments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "notes",
                schema: "learning",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    enrollment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lesson_id = table.Column<Guid>(type: "uuid", nullable: false),
                    text = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_learning_notes", x => x.id);
                    table.ForeignKey(
                        name: "fk_notes_course_release_lessons_lesson_id",
                        column: x => x.lesson_id,
                        principalSchema: "catalog",
                        principalTable: "course_release_lessons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_notes_enrollments_enrollment_id",
                        column: x => x.enrollment_id,
                        principalSchema: "learning",
                        principalTable: "enrollments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_notes_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "recently_viewed",
                schema: "learning",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    enrollment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lesson_id = table.Column<Guid>(type: "uuid", nullable: false),
                    viewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_recently_viewed", x => new { x.user_id, x.enrollment_id, x.lesson_id });
                    table.ForeignKey(
                        name: "fk_recently_viewed_course_release_lessons_lesson_id",
                        column: x => x.lesson_id,
                        principalSchema: "catalog",
                        principalTable: "course_release_lessons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_recently_viewed_enrollments_enrollment_id",
                        column: x => x.enrollment_id,
                        principalSchema: "learning",
                        principalTable: "enrollments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_recently_viewed_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "grade_revisions",
                schema: "assessment",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    submission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision_number = table.Column<int>(type: "integer", nullable: false),
                    score = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    feedback = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    graded_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    graded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_grade_revisions", x => x.id);
                    table.CheckConstraint("ck_grade_revisions_number", "revision_number > 0");
                    table.CheckConstraint("ck_grade_revisions_score", "score BETWEEN 0 AND 100");
                    table.ForeignKey(
                        name: "fk_grade_revisions_assignment_submissions_submission_id",
                        column: x => x.submission_id,
                        principalSchema: "assessment",
                        principalTable: "assignment_submissions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_grade_revisions_users_graded_by_user_id",
                        column: x => x.graded_by_user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "question_options",
                schema: "assessment",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    question_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    text = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    is_correct = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_question_options", x => x.id);
                    table.CheckConstraint("ck_question_options_position", "position >= 0");
                    table.ForeignKey(
                        name: "fk_question_options_questions_question_id",
                        column: x => x.question_id,
                        principalSchema: "assessment",
                        principalTable: "questions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "quiz_answers",
                schema: "assessment",
                columns: table => new
                {
                    attempt_id = table.Column<Guid>(type: "uuid", nullable: false),
                    question_id = table.Column<Guid>(type: "uuid", nullable: false),
                    text_answer = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    selected_option_ids = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    awarded_points = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_quiz_answers", x => new { x.attempt_id, x.question_id });
                    table.ForeignKey(
                        name: "fk_quiz_answers_quiz_attempts_attempt_id",
                        column: x => x.attempt_id,
                        principalSchema: "assessment",
                        principalTable: "quiz_attempts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_quiz_answers_quiz_questions_question_id",
                        column: x => x.question_id,
                        principalSchema: "assessment",
                        principalTable: "questions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "catalog",
                table: "projection_state",
                column: "singleton",
                value: true);

            migrationBuilder.CreateIndex(
                name: "ix_lesson_revisions_assignment_version_id",
                schema: "authoring",
                table: "lesson_revisions",
                column: "assignment_version_id");

            migrationBuilder.CreateIndex(
                name: "ix_lesson_revisions_quiz_version_id",
                schema: "authoring",
                table: "lesson_revisions",
                column: "quiz_version_id");

            migrationBuilder.CreateIndex(
                name: "ix_courses_active_release_id",
                schema: "catalog",
                table: "courses",
                column: "active_release_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_courses_status",
                schema: "catalog",
                table: "courses",
                sql: "status IN ('Draft', 'InReview', 'ChangesRequested', 'ReadyToPublish', 'Published', 'Unpublished', 'Archived')");

            migrationBuilder.CreateIndex(
                name: "ix_assignment_submissions_assignment_version_id",
                schema: "assessment",
                table: "assignment_submissions",
                column: "assignment_version_id");

            migrationBuilder.CreateIndex(
                name: "uq_assignment_submissions_enrollment_version_number",
                schema: "assessment",
                table: "assignment_submissions",
                columns: new[] { "enrollment_id", "assignment_version_id", "submission_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_assignment_versions_assignment_number",
                schema: "assessment",
                table: "assignment_versions",
                columns: new[] { "assignment_id", "version_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_assignments_created_by_user_id",
                schema: "assessment",
                table: "assignments",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_assignments_lesson_id",
                schema: "assessment",
                table: "assignments",
                column: "lesson_id");

            migrationBuilder.CreateIndex(
                name: "uq_assignments_course_lesson",
                schema: "assessment",
                table: "assignments",
                columns: new[] { "course_id", "lesson_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_bookmarks_enrollment_id",
                schema: "learning",
                table: "bookmarks",
                column: "enrollment_id");

            migrationBuilder.CreateIndex(
                name: "ix_bookmarks_lesson_id",
                schema: "learning",
                table: "bookmarks",
                column: "lesson_id");

            migrationBuilder.CreateIndex(
                name: "ix_catalog_documents_locale_published_release",
                schema: "catalog",
                table: "catalog_documents",
                columns: new[] { "locale", "published_at", "release_id" },
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "ix_catalog_documents_locale_slug",
                schema: "catalog",
                table: "catalog_documents",
                columns: new[] { "locale", "slug" });

            migrationBuilder.CreateIndex(
                name: "ix_catalog_documents_release_id_course_id",
                schema: "catalog",
                table: "catalog_documents",
                columns: new[] { "release_id", "course_id" });

            migrationBuilder.CreateIndex(
                name: "uq_catalog_documents_course_locale_release",
                schema: "catalog",
                table: "catalog_documents",
                columns: new[] { "course_id", "locale", "release_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_course_completions_course_id",
                schema: "learning",
                table: "course_completions",
                column: "course_id");

            migrationBuilder.CreateIndex(
                name: "ix_course_completions_release_id_course_id",
                schema: "learning",
                table: "course_completions",
                columns: new[] { "release_id", "course_id" });

            migrationBuilder.CreateIndex(
                name: "ix_course_release_assessments_assignment_version_id",
                schema: "catalog",
                table: "course_release_assessments",
                column: "assignment_version_id");

            migrationBuilder.CreateIndex(
                name: "ix_course_release_assessments_lesson_id_release_id",
                schema: "catalog",
                table: "course_release_assessments",
                columns: new[] { "lesson_id", "release_id" });

            migrationBuilder.CreateIndex(
                name: "ix_course_release_assessments_quiz_version_id",
                schema: "catalog",
                table: "course_release_assessments",
                column: "quiz_version_id");

            migrationBuilder.CreateIndex(
                name: "uq_course_release_assessments_release_lesson_type",
                schema: "catalog",
                table: "course_release_assessments",
                columns: new[] { "release_id", "lesson_id", "type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_course_release_captions_asset_id",
                schema: "catalog",
                table: "course_release_captions",
                column: "asset_id");

            migrationBuilder.CreateIndex(
                name: "ix_course_release_captions_caption_id",
                schema: "catalog",
                table: "course_release_captions",
                column: "caption_id");

            migrationBuilder.CreateIndex(
                name: "ix_course_release_captions_lesson_id_release_id",
                schema: "catalog",
                table: "course_release_captions",
                columns: new[] { "lesson_id", "release_id" });

            migrationBuilder.CreateIndex(
                name: "uq_course_release_captions_manifest_locale",
                schema: "catalog",
                table: "course_release_captions",
                columns: new[] { "release_id", "lesson_id", "locale" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_course_release_instructors_release_position",
                schema: "catalog",
                table: "course_release_instructors",
                columns: new[] { "release_id", "position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_course_release_lessons_media_asset_id",
                schema: "catalog",
                table: "course_release_lessons",
                column: "media_asset_id");

            migrationBuilder.CreateIndex(
                name: "ix_course_release_lessons_section_id_release_id",
                schema: "catalog",
                table: "course_release_lessons",
                columns: new[] { "section_id", "release_id" });

            migrationBuilder.CreateIndex(
                name: "ix_course_release_lessons_source_lesson_id",
                schema: "catalog",
                table: "course_release_lessons",
                column: "source_lesson_id");

            migrationBuilder.CreateIndex(
                name: "ix_course_release_lessons_source_revision_id",
                schema: "catalog",
                table: "course_release_lessons",
                column: "source_revision_id");

            migrationBuilder.CreateIndex(
                name: "uq_course_release_lessons_release_source",
                schema: "catalog",
                table: "course_release_lessons",
                columns: new[] { "release_id", "source_lesson_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_course_release_lessons_section_position",
                schema: "catalog",
                table: "course_release_lessons",
                columns: new[] { "section_id", "position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_course_release_localizations_release_locale",
                schema: "catalog",
                table: "course_release_localizations",
                columns: new[] { "release_id", "locale" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_course_release_media_variants_asset_id",
                schema: "catalog",
                table: "course_release_media_variants",
                column: "asset_id");

            migrationBuilder.CreateIndex(
                name: "ix_course_release_media_variants_lesson_id_release_id",
                schema: "catalog",
                table: "course_release_media_variants",
                columns: new[] { "lesson_id", "release_id" });

            migrationBuilder.CreateIndex(
                name: "ix_course_release_media_variants_variant_id",
                schema: "catalog",
                table: "course_release_media_variants",
                column: "variant_id");

            migrationBuilder.CreateIndex(
                name: "uq_course_release_media_variants_manifest",
                schema: "catalog",
                table: "course_release_media_variants",
                columns: new[] { "release_id", "lesson_id", "variant_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_course_release_sections_source_revision_id",
                schema: "catalog",
                table: "course_release_sections",
                column: "source_revision_id");

            migrationBuilder.CreateIndex(
                name: "ix_course_release_sections_source_section_id",
                schema: "catalog",
                table: "course_release_sections",
                column: "source_section_id");

            migrationBuilder.CreateIndex(
                name: "uq_course_release_sections_release_position",
                schema: "catalog",
                table: "course_release_sections",
                columns: new[] { "release_id", "position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_course_release_sections_release_source",
                schema: "catalog",
                table: "course_release_sections",
                columns: new[] { "release_id", "source_section_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_course_release_taxonomy_filter",
                schema: "catalog",
                table: "course_release_taxonomy",
                columns: new[] { "is_category", "code", "release_id" });

            migrationBuilder.CreateIndex(
                name: "ix_course_releases_course_published_id",
                schema: "catalog",
                table: "course_releases",
                columns: new[] { "course_id", "published_at", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_course_releases_published_by_user_id",
                schema: "catalog",
                table: "course_releases",
                column: "published_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_course_releases_source_draft_id",
                schema: "catalog",
                table: "course_releases",
                column: "source_draft_id");

            migrationBuilder.CreateIndex(
                name: "uq_course_releases_active_course",
                schema: "catalog",
                table: "course_releases",
                column: "course_id",
                unique: true,
                filter: "state = 'Active'");

            migrationBuilder.CreateIndex(
                name: "uq_course_releases_course_draft_version",
                schema: "catalog",
                table: "course_releases",
                columns: new[] { "course_id", "source_draft_id", "source_draft_version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_course_releases_course_number",
                schema: "catalog",
                table: "course_releases",
                columns: new[] { "course_id", "release_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_enrollments_course_id",
                schema: "learning",
                table: "enrollments",
                column: "course_id");

            migrationBuilder.CreateIndex(
                name: "ix_enrollments_release_id",
                schema: "learning",
                table: "enrollments",
                column: "release_id");

            migrationBuilder.CreateIndex(
                name: "ix_enrollments_release_id_course_id",
                schema: "learning",
                table: "enrollments",
                columns: new[] { "release_id", "course_id" });

            migrationBuilder.CreateIndex(
                name: "ix_enrollments_user_last_accessed_id",
                schema: "learning",
                table: "enrollments",
                columns: new[] { "user_id", "last_accessed_at", "id" },
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "uq_enrollments_current_user_course",
                schema: "learning",
                table: "enrollments",
                columns: new[] { "user_id", "course_id" },
                unique: true,
                filter: "status IN ('Active', 'Completed', 'Suspended')");

            migrationBuilder.CreateIndex(
                name: "uq_enrollments_entitlement_id",
                schema: "learning",
                table: "enrollments",
                column: "entitlement_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_entitlements_course_id",
                schema: "learning",
                table: "entitlements",
                column: "course_id");

            migrationBuilder.CreateIndex(
                name: "uq_entitlements_active_user_course",
                schema: "learning",
                table: "entitlements",
                columns: new[] { "user_id", "course_id" },
                unique: true,
                filter: "status = 'Active'");

            migrationBuilder.CreateIndex(
                name: "ix_grade_revisions_graded_by_user_id",
                schema: "assessment",
                table: "grade_revisions",
                column: "graded_by_user_id");

            migrationBuilder.CreateIndex(
                name: "uq_grade_revisions_submission_number",
                schema: "assessment",
                table: "grade_revisions",
                columns: new[] { "submission_id", "revision_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_lesson_progress_lesson_id",
                schema: "learning",
                table: "lesson_progress",
                column: "lesson_id");

            migrationBuilder.CreateIndex(
                name: "ix_learning_notes_user_enrollment_lesson_updated",
                schema: "learning",
                table: "notes",
                columns: new[] { "user_id", "enrollment_id", "lesson_id", "updated_at" });

            migrationBuilder.CreateIndex(
                name: "ix_notes_enrollment_id",
                schema: "learning",
                table: "notes",
                column: "enrollment_id");

            migrationBuilder.CreateIndex(
                name: "ix_notes_lesson_id",
                schema: "learning",
                table: "notes",
                column: "lesson_id");

            migrationBuilder.CreateIndex(
                name: "uq_question_options_question_position",
                schema: "assessment",
                table: "question_options",
                columns: new[] { "question_id", "position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_questions_version_position",
                schema: "assessment",
                table: "questions",
                columns: new[] { "quiz_version_id", "position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_quiz_answers_question_id",
                schema: "assessment",
                table: "quiz_answers",
                column: "question_id");

            migrationBuilder.CreateIndex(
                name: "ix_quiz_attempts_enrollment_version",
                schema: "assessment",
                table: "quiz_attempts",
                columns: new[] { "enrollment_id", "quiz_version_id" });

            migrationBuilder.CreateIndex(
                name: "ix_quiz_attempts_quiz_version_id",
                schema: "assessment",
                table: "quiz_attempts",
                column: "quiz_version_id");

            migrationBuilder.CreateIndex(
                name: "uq_quiz_attempts_enrollment_version_number",
                schema: "assessment",
                table: "quiz_attempts",
                columns: new[] { "enrollment_id", "quiz_version_id", "attempt_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_quiz_versions_quiz_number",
                schema: "assessment",
                table: "quiz_versions",
                columns: new[] { "quiz_id", "version_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_quizzes_created_by_user_id",
                schema: "assessment",
                table: "quizzes",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_quizzes_lesson_id",
                schema: "assessment",
                table: "quizzes",
                column: "lesson_id");

            migrationBuilder.CreateIndex(
                name: "uq_quizzes_course_lesson",
                schema: "assessment",
                table: "quizzes",
                columns: new[] { "course_id", "lesson_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_recently_viewed_enrollment_id",
                schema: "learning",
                table: "recently_viewed",
                column: "enrollment_id");

            migrationBuilder.CreateIndex(
                name: "ix_recently_viewed_lesson_id",
                schema: "learning",
                table: "recently_viewed",
                column: "lesson_id");

            migrationBuilder.CreateIndex(
                name: "ix_recently_viewed_user_viewed_at",
                schema: "learning",
                table: "recently_viewed",
                columns: new[] { "user_id", "viewed_at" },
                descending: new bool[0]);

            migrationBuilder.AddForeignKey(
                name: "fk_courses_course_releases_active_release_id",
                schema: "catalog",
                table: "courses",
                column: "active_release_id",
                principalSchema: "catalog",
                principalTable: "course_releases",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_lesson_revisions_assignment_versions_assignment_version_id",
                schema: "authoring",
                table: "lesson_revisions",
                column: "assignment_version_id",
                principalSchema: "assessment",
                principalTable: "assignment_versions",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_lesson_revisions_quiz_versions_quiz_version_id",
                schema: "authoring",
                table: "lesson_revisions",
                column: "quiz_version_id",
                principalSchema: "assessment",
                principalTable: "quiz_versions",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql(
                """
                ALTER TABLE catalog.catalog_documents
                    ADD COLUMN search_vector tsvector GENERATED ALWAYS AS (
                        setweight(to_tsvector('english', coalesce(title, '')), 'A') ||
                        setweight(to_tsvector('english', coalesce(summary, '')), 'B') ||
                        setweight(to_tsvector('english', coalesce(description, '')), 'C') ||
                        setweight(to_tsvector('english', coalesce(search_text, '')), 'D')) STORED;
                CREATE INDEX ix_catalog_documents_search_english
                    ON catalog.catalog_documents USING gin (search_vector);
                CREATE INDEX ix_catalog_documents_search_arabic
                    ON catalog.catalog_documents USING gin (normalized_arabic_text gin_trgm_ops);

                CREATE FUNCTION catalog.enforce_course_release_update()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $function$
                BEGIN
                    IF ROW(
                        NEW.id,
                        NEW.course_id,
                        NEW.source_draft_id,
                        NEW.source_draft_version,
                        NEW.release_number,
                        NEW.default_locale,
                        NEW.manifest_hash,
                        NEW.published_by_user_id,
                        NEW.published_at)
                        IS DISTINCT FROM ROW(
                        OLD.id,
                        OLD.course_id,
                        OLD.source_draft_id,
                        OLD.source_draft_version,
                        OLD.release_number,
                        OLD.default_locale,
                        OLD.manifest_hash,
                        OLD.published_by_user_id,
                        OLD.published_at) THEN
                        RAISE EXCEPTION 'course release manifest fields are immutable'
                            USING ERRCODE = 'check_violation';
                    END IF;

                    IF OLD.state IS DISTINCT FROM NEW.state AND NOT (
                        (OLD.state = 'Active' AND NEW.state IN ('Superseded', 'Unpublished')) OR
                        (OLD.state IN ('Superseded', 'Unpublished') AND NEW.state = 'Active')) THEN
                        RAISE EXCEPTION 'invalid course release state transition'
                            USING ERRCODE = 'check_violation';
                    END IF;
                    RETURN NEW;
                END
                $function$;

                CREATE TRIGGER trg_course_releases_immutable
                    BEFORE UPDATE ON catalog.course_releases
                    FOR EACH ROW EXECUTE FUNCTION catalog.enforce_course_release_update();

                CREATE FUNCTION catalog.enforce_current_release_consistency()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $function$
                DECLARE
                    affected_course_id uuid;
                    current_release_id uuid;
                    current_course_status character varying(30);
                    release_course_id uuid;
                    release_state character varying(20);
                BEGIN
                    affected_course_id := CASE
                        WHEN TG_TABLE_NAME = 'courses' THEN (to_jsonb(NEW) ->> 'id')::uuid
                        ELSE (to_jsonb(NEW) ->> 'course_id')::uuid
                    END;
                    SELECT active_release_id, status
                    INTO current_release_id, current_course_status
                    FROM catalog.courses
                    WHERE id = affected_course_id;

                    IF current_release_id IS NOT NULL THEN
                        SELECT course_id, state
                        INTO release_course_id, release_state
                        FROM catalog.course_releases
                        WHERE id = current_release_id;
                        IF release_course_id IS DISTINCT FROM affected_course_id OR release_state IS DISTINCT FROM 'Active' THEN
                            RAISE EXCEPTION 'course active release is inconsistent'
                                USING ERRCODE = 'foreign_key_violation';
                        END IF;
                    ELSIF current_course_status = 'Published' THEN
                        RAISE EXCEPTION 'a published course requires an active release'
                            USING ERRCODE = 'check_violation';
                    END IF;

                    IF current_course_status = 'Unpublished' AND current_release_id IS NOT NULL THEN
                        RAISE EXCEPTION 'an unpublished course cannot have an active release'
                            USING ERRCODE = 'check_violation';
                    END IF;
                    RETURN NULL;
                END
                $function$;

                CREATE CONSTRAINT TRIGGER trg_courses_current_release
                    AFTER INSERT OR UPDATE OF active_release_id, status ON catalog.courses
                    DEFERRABLE INITIALLY DEFERRED
                    FOR EACH ROW EXECUTE FUNCTION catalog.enforce_current_release_consistency();
                CREATE CONSTRAINT TRIGGER trg_course_releases_current_release
                    AFTER INSERT OR UPDATE OF state ON catalog.course_releases
                    DEFERRABLE INITIALLY DEFERRED
                    FOR EACH ROW EXECUTE FUNCTION catalog.enforce_current_release_consistency();

                DO $permissions$
                BEGIN
                    IF EXISTS (SELECT FROM pg_roles WHERE rolname = 'dorosak_runtime') THEN
                        GRANT USAGE ON SCHEMA assessment, learning TO dorosak_runtime;
                        GRANT SELECT, INSERT, UPDATE, DELETE
                            ON ALL TABLES IN SCHEMA assessment, learning
                            TO dorosak_runtime;

                        GRANT SELECT, INSERT ON
                            catalog.course_releases,
                            catalog.course_release_sections,
                            catalog.course_release_lessons,
                            catalog.course_release_assessments,
                            catalog.course_release_media_variants,
                            catalog.course_release_captions,
                            catalog.course_release_localizations,
                            catalog.course_release_instructors,
                            catalog.course_release_taxonomy,
                            catalog.catalog_documents
                            TO dorosak_runtime;
                        REVOKE UPDATE, DELETE, TRUNCATE ON
                            catalog.course_releases,
                            catalog.course_release_sections,
                            catalog.course_release_lessons,
                            catalog.course_release_assessments,
                            catalog.course_release_media_variants,
                            catalog.course_release_captions,
                            catalog.course_release_localizations,
                            catalog.course_release_instructors,
                            catalog.course_release_taxonomy,
                            catalog.catalog_documents
                            FROM dorosak_runtime;
                        GRANT UPDATE (state) ON catalog.course_releases TO dorosak_runtime;

                        REVOKE INSERT, DELETE, TRUNCATE ON catalog.projection_state FROM dorosak_runtime;
                        GRANT SELECT, UPDATE ON catalog.projection_state TO dorosak_runtime;
                    END IF;
                END
                $permissions$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE operations.schema_compatibility
                SET maximum_compatible_migration_id = '20260808044726_Phase7MediaExitGate',
                    updated_at = CURRENT_TIMESTAMP
                WHERE singleton;

                DROP FUNCTION catalog.enforce_current_release_consistency() CASCADE;
                DROP FUNCTION catalog.enforce_course_release_update() CASCADE;
                """);

            migrationBuilder.DropForeignKey(
                name: "fk_courses_course_releases_active_release_id",
                schema: "catalog",
                table: "courses");

            migrationBuilder.DropForeignKey(
                name: "fk_lesson_revisions_assignment_versions_assignment_version_id",
                schema: "authoring",
                table: "lesson_revisions");

            migrationBuilder.DropForeignKey(
                name: "fk_lesson_revisions_quiz_versions_quiz_version_id",
                schema: "authoring",
                table: "lesson_revisions");

            migrationBuilder.DropTable(
                name: "bookmarks",
                schema: "learning");

            migrationBuilder.DropTable(
                name: "catalog_documents",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "course_completions",
                schema: "learning");

            migrationBuilder.DropTable(
                name: "course_release_assessments",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "course_release_captions",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "course_release_instructors",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "course_release_localizations",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "course_release_media_variants",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "course_release_taxonomy",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "grade_revisions",
                schema: "assessment");

            migrationBuilder.DropTable(
                name: "lesson_progress",
                schema: "learning");

            migrationBuilder.DropTable(
                name: "notes",
                schema: "learning");

            migrationBuilder.DropTable(
                name: "projection_state",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "question_options",
                schema: "assessment");

            migrationBuilder.DropTable(
                name: "quiz_answers",
                schema: "assessment");

            migrationBuilder.DropTable(
                name: "recently_viewed",
                schema: "learning");

            migrationBuilder.DropTable(
                name: "assignment_submissions",
                schema: "assessment");

            migrationBuilder.DropTable(
                name: "quiz_attempts",
                schema: "assessment");

            migrationBuilder.DropTable(
                name: "questions",
                schema: "assessment");

            migrationBuilder.DropTable(
                name: "course_release_lessons",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "assignment_versions",
                schema: "assessment");

            migrationBuilder.DropTable(
                name: "enrollments",
                schema: "learning");

            migrationBuilder.DropTable(
                name: "quiz_versions",
                schema: "assessment");

            migrationBuilder.DropTable(
                name: "course_release_sections",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "assignments",
                schema: "assessment");

            migrationBuilder.DropTable(
                name: "entitlements",
                schema: "learning");

            migrationBuilder.DropTable(
                name: "quizzes",
                schema: "assessment");

            migrationBuilder.DropTable(
                name: "course_releases",
                schema: "catalog");

            migrationBuilder.DropIndex(
                name: "ix_lesson_revisions_assignment_version_id",
                schema: "authoring",
                table: "lesson_revisions");

            migrationBuilder.DropIndex(
                name: "ix_lesson_revisions_quiz_version_id",
                schema: "authoring",
                table: "lesson_revisions");

            migrationBuilder.DropIndex(
                name: "ix_courses_active_release_id",
                schema: "catalog",
                table: "courses");

            migrationBuilder.DropCheckConstraint(
                name: "ck_courses_status",
                schema: "catalog",
                table: "courses");

            migrationBuilder.DropColumn(
                name: "assignment_version_id",
                schema: "authoring",
                table: "lesson_revisions");

            migrationBuilder.DropColumn(
                name: "quiz_version_id",
                schema: "authoring",
                table: "lesson_revisions");

            migrationBuilder.DropColumn(
                name: "active_release_id",
                schema: "catalog",
                table: "courses");

            migrationBuilder.DropColumn(
                name: "projection_generation",
                schema: "catalog",
                table: "courses");

            migrationBuilder.AddCheckConstraint(
                name: "ck_courses_status",
                schema: "catalog",
                table: "courses",
                sql: "status IN ('Draft', 'InReview', 'ChangesRequested', 'ReadyToPublish', 'Archived')");
        }
    }
}
