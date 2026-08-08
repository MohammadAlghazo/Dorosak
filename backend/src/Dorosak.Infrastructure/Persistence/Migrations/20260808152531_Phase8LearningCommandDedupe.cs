using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dorosak.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase8LearningCommandDedupe : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE operations.schema_compatibility
                SET maximum_compatible_migration_id = '20260808152531_Phase8LearningCommandDedupe',
                    updated_at = CURRENT_TIMESTAMP
                WHERE singleton;
                """);

            migrationBuilder.AddColumn<Guid>(
                name: "last_client_command_id",
                schema: "learning",
                table: "lesson_progress",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE operations.schema_compatibility
                SET maximum_compatible_migration_id = '20260808134406_Phase8ReleaseCatalog',
                    updated_at = CURRENT_TIMESTAMP
                WHERE singleton;
                """);

            migrationBuilder.DropColumn(
                name: "last_client_command_id",
                schema: "learning",
                table: "lesson_progress");
        }
    }
}
