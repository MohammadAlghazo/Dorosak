using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dorosak.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase7Media : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE operations.schema_compatibility
                SET maximum_compatible_migration_id = '20260808011129_Phase7Media',
                    updated_at = CURRENT_TIMESTAMP
                WHERE singleton
                """);

            migrationBuilder.EnsureSchema(
                name: "media");

            migrationBuilder.AddColumn<Guid>(
                name: "media_asset_id",
                schema: "authoring",
                table: "lesson_revisions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "media_assets",
                schema: "media",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    course_id = table.Column<Guid>(type: "uuid", nullable: true),
                    purpose = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    state = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    content_type = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    declared_bytes = table.Column<long>(type: "bigint", nullable: false),
                    verified_bytes = table.Column<long>(type: "bigint", nullable: true),
                    declared_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    verified_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    quarantine_object_key = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    quarantine_e_tag = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    quarantine_version_id = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    rejection_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ready_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_media_assets", x => x.id);
                    table.CheckConstraint("ck_media_assets_declared_bytes", "declared_bytes > 0");
                    table.CheckConstraint("ck_media_assets_declared_sha256", "declared_sha256 ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("ck_media_assets_state", "state IN ('Initiated', 'Uploaded', 'Scanning', 'Processing', 'Ready', 'Rejected', 'RecoveryPending', 'Deleted')");
                    table.CheckConstraint("ck_media_assets_verified_bytes", "verified_bytes IS NULL OR verified_bytes > 0");
                    table.ForeignKey(
                        name: "fk_media_assets_courses_course_id",
                        column: x => x.course_id,
                        principalSchema: "catalog",
                        principalTable: "courses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_media_assets_users_owner_user_id",
                        column: x => x.owner_user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "caption_tracks",
                schema: "media",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    asset_id = table.Column<Guid>(type: "uuid", nullable: false),
                    locale = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    label = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    object_key = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    bytes = table.Column<long>(type: "bigint", nullable: false),
                    e_tag = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_caption_tracks", x => x.id);
                    table.CheckConstraint("ck_caption_tracks_bytes", "bytes > 0");
                    table.ForeignKey(
                        name: "fk_caption_tracks_media_assets_asset_id",
                        column: x => x.asset_id,
                        principalSchema: "media",
                        principalTable: "media_assets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "media_processing_jobs",
                schema: "media",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    asset_id = table.Column<Guid>(type: "uuid", nullable: false),
                    state = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    available_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    locked_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    lock_token = table.Column<Guid>(type: "uuid", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_error_code = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_media_processing_jobs", x => x.id);
                    table.CheckConstraint("ck_media_processing_jobs_attempt_count", "attempt_count >= 0");
                    table.CheckConstraint("ck_media_processing_jobs_state", "state IN ('Pending', 'Processing', 'Completed', 'Failed')");
                    table.ForeignKey(
                        name: "fk_media_processing_jobs_media_assets_asset_id",
                        column: x => x.asset_id,
                        principalSchema: "media",
                        principalTable: "media_assets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "media_variants",
                schema: "media",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    asset_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    content_type = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    object_key = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    bytes = table.Column<long>(type: "bigint", nullable: false),
                    e_tag = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    version_id = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    width = table.Column<int>(type: "integer", nullable: true),
                    height = table.Column<int>(type: "integer", nullable: true),
                    duration_seconds = table.Column<decimal>(type: "numeric", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_media_variants", x => x.id);
                    table.CheckConstraint("ck_media_variants_bytes", "bytes > 0");
                    table.CheckConstraint("ck_media_variants_dimensions", "(width IS NULL AND height IS NULL) OR (width > 0 AND height > 0)");
                    table.CheckConstraint("ck_media_variants_duration", "duration_seconds IS NULL OR duration_seconds >= 0");
                    table.ForeignKey(
                        name: "fk_media_variants_media_assets_asset_id",
                        column: x => x.asset_id,
                        principalSchema: "media",
                        principalTable: "media_assets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "upload_sessions",
                schema: "media",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    asset_id = table.Column<Guid>(type: "uuid", nullable: false),
                    course_id = table.Column<Guid>(type: "uuid", nullable: true),
                    purpose = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    state = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    expected_bytes = table.Column<long>(type: "bigint", nullable: false),
                    reserved_bytes = table.Column<long>(type: "bigint", nullable: false),
                    file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    content_type = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    quarantine_object_key = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    multipart_upload_id = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cancelled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_upload_sessions", x => x.id);
                    table.CheckConstraint("ck_upload_sessions_expected_bytes", "expected_bytes > 0");
                    table.CheckConstraint("ck_upload_sessions_reserved_bytes", "reserved_bytes >= 0 AND reserved_bytes <= expected_bytes");
                    table.CheckConstraint("ck_upload_sessions_state", "state IN ('Initiated', 'Uploading', 'Completed', 'Cancelled', 'Expired')");
                    table.ForeignKey(
                        name: "fk_upload_sessions_media_assets_asset_id",
                        column: x => x.asset_id,
                        principalSchema: "media",
                        principalTable: "media_assets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_upload_sessions_users_owner_user_id",
                        column: x => x.owner_user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "upload_parts",
                schema: "media",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    upload_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    part_number = table.Column<int>(type: "integer", nullable: false),
                    expected_bytes = table.Column<long>(type: "bigint", nullable: false),
                    sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    e_tag = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    version_id = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_upload_parts", x => x.id);
                    table.CheckConstraint("ck_upload_parts_expected_bytes", "expected_bytes > 0");
                    table.CheckConstraint("ck_upload_parts_part_number", "part_number BETWEEN 1 AND 10000");
                    table.CheckConstraint("ck_upload_parts_sha256", "sha256 ~ '^[0-9a-f]{64}$'");
                    table.ForeignKey(
                        name: "fk_upload_parts_upload_sessions_session_id",
                        column: x => x.upload_session_id,
                        principalSchema: "media",
                        principalTable: "upload_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_lesson_revisions_media_asset_id",
                schema: "authoring",
                table: "lesson_revisions",
                column: "media_asset_id");

            migrationBuilder.CreateIndex(
                name: "uq_caption_tracks_asset_locale",
                schema: "media",
                table: "caption_tracks",
                columns: new[] { "asset_id", "locale" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_media_assets_course_state",
                schema: "media",
                table: "media_assets",
                columns: new[] { "course_id", "state" });

            migrationBuilder.CreateIndex(
                name: "ix_media_assets_owner_state_created_id",
                schema: "media",
                table: "media_assets",
                columns: new[] { "owner_user_id", "state", "created_at", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_media_processing_jobs_locked_until",
                schema: "media",
                table: "media_processing_jobs",
                column: "locked_until",
                filter: "state = 'Processing' AND completed_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_media_processing_jobs_pending",
                schema: "media",
                table: "media_processing_jobs",
                columns: new[] { "available_at", "created_at", "id" },
                filter: "state IN ('Pending', 'Processing') AND completed_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "uq_media_processing_jobs_asset_id",
                schema: "media",
                table: "media_processing_jobs",
                column: "asset_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_media_variants_asset_id",
                schema: "media",
                table: "media_variants",
                column: "asset_id");

            migrationBuilder.CreateIndex(
                name: "uq_media_variants_asset_kind",
                schema: "media",
                table: "media_variants",
                columns: new[] { "asset_id", "kind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_upload_parts_session_id",
                schema: "media",
                table: "upload_parts",
                column: "upload_session_id");

            migrationBuilder.CreateIndex(
                name: "uq_upload_parts_session_number",
                schema: "media",
                table: "upload_parts",
                columns: new[] { "upload_session_id", "part_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_upload_sessions_expires_at",
                schema: "media",
                table: "upload_sessions",
                column: "expires_at",
                filter: "state IN ('Initiated', 'Uploading')");

            migrationBuilder.CreateIndex(
                name: "ix_upload_sessions_owner_created_id",
                schema: "media",
                table: "upload_sessions",
                columns: new[] { "owner_user_id", "created_at", "id" });

            migrationBuilder.CreateIndex(
                name: "uq_upload_sessions_asset_id",
                schema: "media",
                table: "upload_sessions",
                column: "asset_id",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_lesson_revisions_media_assets_media_asset_id",
                schema: "authoring",
                table: "lesson_revisions",
                column: "media_asset_id",
                principalSchema: "media",
                principalTable: "media_assets",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE operations.schema_compatibility
                SET maximum_compatible_migration_id = '20260807221624_Phase6ConcurrencyAndCatalogIndexes',
                    updated_at = CURRENT_TIMESTAMP
                WHERE singleton
                """);

            migrationBuilder.DropForeignKey(
                name: "fk_lesson_revisions_media_assets_media_asset_id",
                schema: "authoring",
                table: "lesson_revisions");

            migrationBuilder.DropTable(
                name: "caption_tracks",
                schema: "media");

            migrationBuilder.DropTable(
                name: "media_processing_jobs",
                schema: "media");

            migrationBuilder.DropTable(
                name: "media_variants",
                schema: "media");

            migrationBuilder.DropTable(
                name: "upload_parts",
                schema: "media");

            migrationBuilder.DropTable(
                name: "upload_sessions",
                schema: "media");

            migrationBuilder.DropTable(
                name: "media_assets",
                schema: "media");

            migrationBuilder.DropIndex(
                name: "ix_lesson_revisions_media_asset_id",
                schema: "authoring",
                table: "lesson_revisions");

            migrationBuilder.DropColumn(
                name: "media_asset_id",
                schema: "authoring",
                table: "lesson_revisions");
        }
    }
}
