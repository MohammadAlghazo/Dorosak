using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dorosak.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase9ModerationHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE operations.schema_compatibility
                SET maximum_compatible_migration_id = '20260810212341_Phase9ModerationHardening',
                    updated_at = CURRENT_TIMESTAMP
                WHERE singleton;
                """);

            migrationBuilder.CreateIndex(
                name: "ix_moderation_cases_created_id",
                schema: "engagement",
                table: "moderation_cases",
                columns: new[] { "created_at", "id" },
                descending: new bool[0]);

            migrationBuilder.Sql(
                """
                DO $permissions$
                BEGIN
                    IF EXISTS (SELECT FROM pg_roles WHERE rolname = 'dorosak_runtime') THEN
                        REVOKE UPDATE ON
                            engagement.content_reports,
                            engagement.moderation_cases
                            FROM dorosak_runtime;
                        GRANT UPDATE (status, updated_at, closed_at)
                            ON engagement.content_reports TO dorosak_runtime;
                        GRANT UPDATE (status, assigned_to_user_id, version, updated_at, closed_at)
                            ON engagement.moderation_cases TO dorosak_runtime;
                    END IF;
                END
                $permissions$;
                """);

            migrationBuilder.CreateIndex(
                name: "ix_content_reports_created_id",
                schema: "engagement",
                table: "content_reports",
                columns: new[] { "created_at", "id" },
                descending: new bool[0]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE operations.schema_compatibility
                SET maximum_compatible_migration_id = '20260810131301_Phase9ReportsModeration',
                    updated_at = CURRENT_TIMESTAMP
                WHERE singleton;
                DO $permissions$
                BEGIN
                    IF EXISTS (SELECT FROM pg_roles WHERE rolname = 'dorosak_runtime') THEN
                        REVOKE UPDATE (status, updated_at, closed_at)
                            ON engagement.content_reports FROM dorosak_runtime;
                        REVOKE UPDATE (status, assigned_to_user_id, version, updated_at, closed_at)
                            ON engagement.moderation_cases FROM dorosak_runtime;
                        GRANT UPDATE ON
                            engagement.content_reports,
                            engagement.moderation_cases
                            TO dorosak_runtime;
                    END IF;
                END
                $permissions$;
                """);

            migrationBuilder.DropIndex(
                name: "ix_moderation_cases_created_id",
                schema: "engagement",
                table: "moderation_cases");

            migrationBuilder.DropIndex(
                name: "ix_content_reports_created_id",
                schema: "engagement",
                table: "content_reports");
        }
    }
}
