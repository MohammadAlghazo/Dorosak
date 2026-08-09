using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dorosak.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase9EngagementAssignmentAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE operations.schema_compatibility
                SET maximum_compatible_migration_id = '20260809123709_Phase9EngagementAssignmentAttachments',
                    updated_at = CURRENT_TIMESTAMP
                WHERE singleton;
                """);

            migrationBuilder.AddColumn<string>(
                name: "audience_type",
                schema: "assessment",
                table: "quiz_versions",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "AllEnrolled");

            migrationBuilder.AddColumn<string>(
                name: "audience_type",
                schema: "catalog",
                table: "course_release_assessments",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "AllEnrolled");

            migrationBuilder.AddColumn<string>(
                name: "audience_type",
                schema: "assessment",
                table: "assignment_versions",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "AllEnrolled");

            migrationBuilder.CreateTable(
                name: "assignment_audience_members",
                schema: "assessment",
                columns: table => new
                {
                    assignment_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_assignment_audience_members", x => new { x.assignment_version_id, x.user_id });
                    table.ForeignKey(
                        name: "fk_assignment_audience_members_assignment_versions_version_id",
                        column: x => x.assignment_version_id,
                        principalSchema: "assessment",
                        principalTable: "assignment_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_assignment_audience_members_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "quiz_audience_members",
                schema: "assessment",
                columns: table => new
                {
                    quiz_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_quiz_audience_members", x => new { x.quiz_version_id, x.user_id });
                    table.ForeignKey(
                        name: "fk_quiz_audience_members_quiz_versions_version_id",
                        column: x => x.quiz_version_id,
                        principalSchema: "assessment",
                        principalTable: "quiz_versions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_quiz_audience_members_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "submission_files",
                schema: "assessment",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    submission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    asset_id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_file_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_submission_files", x => x.id);
                    table.ForeignKey(
                        name: "fk_submission_files_assignment_submissions_submission_id",
                        column: x => x.submission_id,
                        principalSchema: "assessment",
                        principalTable: "assignment_submissions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_submission_files_media_assets_asset_id",
                        column: x => x.asset_id,
                        principalSchema: "media",
                        principalTable: "media_assets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_quiz_versions_audience_type",
                schema: "assessment",
                table: "quiz_versions",
                sql: "audience_type IN ('AllEnrolled', 'SelectedLearners')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_course_release_assessments_audience_type",
                schema: "catalog",
                table: "course_release_assessments",
                sql: "audience_type IN ('AllEnrolled', 'SelectedLearners')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_assignment_versions_audience_type",
                schema: "assessment",
                table: "assignment_versions",
                sql: "audience_type IN ('AllEnrolled', 'SelectedLearners')");

            migrationBuilder.CreateIndex(
                name: "ix_assignment_audience_members_user_version",
                schema: "assessment",
                table: "assignment_audience_members",
                columns: new[] { "user_id", "assignment_version_id" });

            migrationBuilder.CreateIndex(
                name: "ix_quiz_audience_members_user_version",
                schema: "assessment",
                table: "quiz_audience_members",
                columns: new[] { "user_id", "quiz_version_id" });

            migrationBuilder.CreateIndex(
                name: "ix_submission_files_submission_created_id",
                schema: "assessment",
                table: "submission_files",
                columns: new[] { "submission_id", "created_at", "id" });

            migrationBuilder.CreateIndex(
                name: "uq_submission_files_asset_id",
                schema: "assessment",
                table: "submission_files",
                column: "asset_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_submission_files_submission_client_file",
                schema: "assessment",
                table: "submission_files",
                columns: new[] { "submission_id", "client_file_id" },
                unique: true);

            migrationBuilder.Sql(
                """
                DO $permissions$
                BEGIN
                    IF EXISTS (SELECT FROM pg_roles WHERE rolname = 'dorosak_runtime') THEN
                        GRANT SELECT, INSERT ON
                            assessment.assignment_audience_members,
                            assessment.quiz_audience_members,
                            assessment.submission_files
                            TO dorosak_runtime;
                        REVOKE UPDATE, DELETE, TRUNCATE ON
                            assessment.assignment_audience_members,
                            assessment.quiz_audience_members,
                            assessment.submission_files
                            FROM dorosak_runtime;
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
                SET maximum_compatible_migration_id = '20260808185713_Phase8AssessmentLifecycle',
                    updated_at = CURRENT_TIMESTAMP
                WHERE singleton;

                DO $permissions$
                BEGIN
                    IF EXISTS (SELECT FROM pg_roles WHERE rolname = 'dorosak_runtime') THEN
                        REVOKE ALL PRIVILEGES ON
                            assessment.assignment_audience_members,
                            assessment.quiz_audience_members,
                            assessment.submission_files
                            FROM dorosak_runtime;
                    END IF;
                END
                $permissions$;
                """);

            migrationBuilder.DropTable(
                name: "assignment_audience_members",
                schema: "assessment");

            migrationBuilder.DropTable(
                name: "quiz_audience_members",
                schema: "assessment");

            migrationBuilder.DropTable(
                name: "submission_files",
                schema: "assessment");

            migrationBuilder.DropCheckConstraint(
                name: "ck_quiz_versions_audience_type",
                schema: "assessment",
                table: "quiz_versions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_course_release_assessments_audience_type",
                schema: "catalog",
                table: "course_release_assessments");

            migrationBuilder.DropCheckConstraint(
                name: "ck_assignment_versions_audience_type",
                schema: "assessment",
                table: "assignment_versions");

            migrationBuilder.DropColumn(
                name: "audience_type",
                schema: "assessment",
                table: "quiz_versions");

            migrationBuilder.DropColumn(
                name: "audience_type",
                schema: "catalog",
                table: "course_release_assessments");

            migrationBuilder.DropColumn(
                name: "audience_type",
                schema: "assessment",
                table: "assignment_versions");
        }
    }
}
