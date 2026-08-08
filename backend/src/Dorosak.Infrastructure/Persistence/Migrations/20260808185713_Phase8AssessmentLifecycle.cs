using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dorosak.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase8AssessmentLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE operations.schema_compatibility
                SET maximum_compatible_migration_id = '20260808185713_Phase8AssessmentLifecycle',
                    updated_at = CURRENT_TIMESTAMP
                WHERE singleton;
                """);

            migrationBuilder.DropCheckConstraint(
                name: "ck_quiz_attempts_status",
                schema: "assessment",
                table: "quiz_attempts");

            migrationBuilder.CreateTable(
                name: "quiz_grade_revisions",
                schema: "assessment",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    attempt_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision_number = table.Column<int>(type: "integer", nullable: false),
                    score = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    feedback = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    graded_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    graded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_quiz_grade_revisions", x => x.id);
                    table.CheckConstraint("ck_quiz_grade_revisions_number", "revision_number > 0");
                    table.CheckConstraint("ck_quiz_grade_revisions_score", "score BETWEEN 0 AND 100");
                    table.ForeignKey(
                        name: "fk_quiz_grade_revisions_quiz_attempts_attempt_id",
                        column: x => x.attempt_id,
                        principalSchema: "assessment",
                        principalTable: "quiz_attempts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_quiz_grade_revisions_users_graded_by_user_id",
                        column: x => x.graded_by_user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_quiz_attempts_status",
                schema: "assessment",
                table: "quiz_attempts",
                sql: "status IN ('InProgress', 'Expired', 'Submitted', 'PendingManualGrade', 'Graded')");

            migrationBuilder.CreateIndex(
                name: "ix_quiz_grade_revisions_graded_by_user_id",
                schema: "assessment",
                table: "quiz_grade_revisions",
                column: "graded_by_user_id");

            migrationBuilder.CreateIndex(
                name: "uq_quiz_grade_revisions_attempt_number",
                schema: "assessment",
                table: "quiz_grade_revisions",
                columns: new[] { "attempt_id", "revision_number" },
                unique: true);

            migrationBuilder.Sql(
                """
                DO $permissions$
                BEGIN
                    IF EXISTS (SELECT FROM pg_roles WHERE rolname = 'dorosak_runtime') THEN
                        GRANT SELECT, INSERT ON
                            assessment.grade_revisions,
                            assessment.quiz_grade_revisions
                            TO dorosak_runtime;
                        REVOKE UPDATE, DELETE, TRUNCATE ON
                            assessment.grade_revisions,
                            assessment.quiz_grade_revisions
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
                SET maximum_compatible_migration_id = '20260808152531_Phase8LearningCommandDedupe',
                    updated_at = CURRENT_TIMESTAMP
                WHERE singleton;
                """);

            migrationBuilder.Sql(
                """
                DO $permissions$
                BEGIN
                    IF EXISTS (SELECT FROM pg_roles WHERE rolname = 'dorosak_runtime') THEN
                        REVOKE ALL PRIVILEGES ON assessment.quiz_grade_revisions FROM dorosak_runtime;
                        GRANT SELECT, INSERT, UPDATE, DELETE ON assessment.grade_revisions TO dorosak_runtime;
                    END IF;
                END
                $permissions$;
                """);
            migrationBuilder.DropTable(
                name: "quiz_grade_revisions",
                schema: "assessment");

            migrationBuilder.DropCheckConstraint(
                name: "ck_quiz_attempts_status",
                schema: "assessment",
                table: "quiz_attempts");

            migrationBuilder.AddCheckConstraint(
                name: "ck_quiz_attempts_status",
                schema: "assessment",
                table: "quiz_attempts",
                sql: "status IN ('InProgress', 'Submitted', 'PendingManualGrade', 'Graded')");
        }
    }
}
