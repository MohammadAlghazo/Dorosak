using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dorosak.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPendingEmailChange : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "pending_email",
                schema: "identity",
                table: "users",
                type: "character varying(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE operations.schema_compatibility
                SET maximum_compatible_migration_id = '20260807113555_AddPendingEmailChange',
                    updated_at = CURRENT_TIMESTAMP
                WHERE singleton
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE operations.schema_compatibility
                SET maximum_compatible_migration_id = '20260806223946_Phase5IdentitySecurity',
                    updated_at = CURRENT_TIMESTAMP
                WHERE singleton
                """);

            migrationBuilder.DropColumn(
                name: "pending_email",
                schema: "identity",
                table: "users");
        }
    }
}
