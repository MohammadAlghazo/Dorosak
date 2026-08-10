using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dorosak.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase9ReportsModeration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE operations.schema_compatibility
                SET maximum_compatible_migration_id = '20260810131301_Phase9ReportsModeration',
                    updated_at = CURRENT_TIMESTAMP
                WHERE singleton;
                """);

            migrationBuilder.CreateTable(
                name: "content_reports",
                schema: "engagement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    reporter_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    course_id = table.Column<Guid>(type: "uuid", nullable: true),
                    review_id = table.Column<Guid>(type: "uuid", nullable: true),
                    comment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reported_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reason = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    details = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_content_reports", x => x.id);
                    table.CheckConstraint("ck_content_reports_exact_target", "num_nonnulls(course_id, review_id, comment_id, reported_user_id) = 1");
                    table.CheckConstraint("ck_content_reports_reason", "reason IN ('Spam', 'Harassment', 'HateSpeech', 'Misinformation', 'Copyright', 'PersonalData', 'Other')");
                    table.CheckConstraint("ck_content_reports_status", "status IN ('Open', 'InReview', 'Resolved', 'Dismissed')");
                    table.ForeignKey(
                        name: "fk_content_reports_comments_comment_id",
                        column: x => x.comment_id,
                        principalSchema: "engagement",
                        principalTable: "comments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_content_reports_courses_course_id",
                        column: x => x.course_id,
                        principalSchema: "catalog",
                        principalTable: "courses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_content_reports_reviews_review_id",
                        column: x => x.review_id,
                        principalSchema: "engagement",
                        principalTable: "course_reviews",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_content_reports_users_reported_user_id",
                        column: x => x.reported_user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_content_reports_users_reporter_user_id",
                        column: x => x.reporter_user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "moderation_cases",
                schema: "engagement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    report_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    assigned_to_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    version = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_moderation_cases", x => x.id);
                    table.CheckConstraint("ck_moderation_cases_status", "status IN ('Open', 'InReview', 'Resolved', 'Dismissed')");
                    table.ForeignKey(
                        name: "fk_moderation_cases_reports_report_id",
                        column: x => x.report_id,
                        principalSchema: "engagement",
                        principalTable: "content_reports",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_moderation_cases_users_assigned_to_user_id",
                        column: x => x.assigned_to_user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "moderation_actions",
                schema: "engagement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_moderation_actions", x => x.id);
                    table.CheckConstraint("ck_moderation_actions_type", "action_type IN ('StartReview', 'HideContent', 'RestoreContent', 'Resolve', 'Dismiss')");
                    table.ForeignKey(
                        name: "fk_moderation_actions_cases_case_id",
                        column: x => x.case_id,
                        principalSchema: "engagement",
                        principalTable: "moderation_cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_moderation_actions_users_actor_user_id",
                        column: x => x.actor_user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_content_reports_comment_id",
                schema: "engagement",
                table: "content_reports",
                column: "comment_id");

            migrationBuilder.CreateIndex(
                name: "ix_content_reports_course_id",
                schema: "engagement",
                table: "content_reports",
                column: "course_id");

            migrationBuilder.CreateIndex(
                name: "ix_content_reports_reported_user_id",
                schema: "engagement",
                table: "content_reports",
                column: "reported_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_content_reports_reporter_created_id",
                schema: "engagement",
                table: "content_reports",
                columns: new[] { "reporter_user_id", "created_at", "id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "ix_content_reports_review_id",
                schema: "engagement",
                table: "content_reports",
                column: "review_id");

            migrationBuilder.CreateIndex(
                name: "ix_content_reports_status_created_id",
                schema: "engagement",
                table: "content_reports",
                columns: new[] { "status", "created_at", "id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "uq_content_reports_open_comment",
                schema: "engagement",
                table: "content_reports",
                columns: new[] { "reporter_user_id", "comment_id" },
                unique: true,
                filter: "comment_id IS NOT NULL AND status IN ('Open', 'InReview')");

            migrationBuilder.CreateIndex(
                name: "uq_content_reports_open_course",
                schema: "engagement",
                table: "content_reports",
                columns: new[] { "reporter_user_id", "course_id" },
                unique: true,
                filter: "course_id IS NOT NULL AND status IN ('Open', 'InReview')");

            migrationBuilder.CreateIndex(
                name: "uq_content_reports_open_review",
                schema: "engagement",
                table: "content_reports",
                columns: new[] { "reporter_user_id", "review_id" },
                unique: true,
                filter: "review_id IS NOT NULL AND status IN ('Open', 'InReview')");

            migrationBuilder.CreateIndex(
                name: "uq_content_reports_open_user",
                schema: "engagement",
                table: "content_reports",
                columns: new[] { "reporter_user_id", "reported_user_id" },
                unique: true,
                filter: "reported_user_id IS NOT NULL AND status IN ('Open', 'InReview')");

            migrationBuilder.CreateIndex(
                name: "ix_moderation_actions_actor_user_id",
                schema: "engagement",
                table: "moderation_actions",
                column: "actor_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_moderation_actions_case_created_id",
                schema: "engagement",
                table: "moderation_actions",
                columns: new[] { "case_id", "created_at", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_moderation_cases_assigned_to_user_id",
                schema: "engagement",
                table: "moderation_cases",
                column: "assigned_to_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_moderation_cases_status_created_id",
                schema: "engagement",
                table: "moderation_cases",
                columns: new[] { "status", "created_at", "id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "uq_moderation_cases_report_id",
                schema: "engagement",
                table: "moderation_cases",
                column: "report_id",
                unique: true);

            migrationBuilder.Sql(
                """
                DO $permissions$
                BEGIN
                    IF EXISTS (SELECT FROM pg_roles WHERE rolname = 'dorosak_runtime') THEN
                        GRANT USAGE ON SCHEMA engagement TO dorosak_runtime;
                        GRANT SELECT, INSERT, UPDATE ON
                            engagement.content_reports,
                            engagement.moderation_cases
                            TO dorosak_runtime;
                        REVOKE DELETE, TRUNCATE ON
                            engagement.content_reports,
                            engagement.moderation_cases
                            FROM dorosak_runtime;
                        GRANT SELECT, INSERT ON engagement.moderation_actions TO dorosak_runtime;
                        REVOKE UPDATE, DELETE, TRUNCATE ON engagement.moderation_actions FROM dorosak_runtime;
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
                SET maximum_compatible_migration_id = '20260810004326_Phase9Discussions',
                    updated_at = CURRENT_TIMESTAMP
                WHERE singleton;
                DO $permissions$
                BEGIN
                    IF EXISTS (SELECT FROM pg_roles WHERE rolname = 'dorosak_runtime') THEN
                        REVOKE ALL PRIVILEGES ON
                            engagement.moderation_actions,
                            engagement.moderation_cases,
                            engagement.content_reports
                            FROM dorosak_runtime;
                    END IF;
                END
                $permissions$;
                """);

            migrationBuilder.DropTable(
                name: "moderation_actions",
                schema: "engagement");

            migrationBuilder.DropTable(
                name: "moderation_cases",
                schema: "engagement");

            migrationBuilder.DropTable(
                name: "content_reports",
                schema: "engagement");
        }
    }
}
