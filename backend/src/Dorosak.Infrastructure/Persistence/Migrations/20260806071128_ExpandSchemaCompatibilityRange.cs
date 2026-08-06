using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dorosak.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExpandSchemaCompatibilityRange : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "minimum_compatible_migration_id",
                schema: "operations",
                table: "schema_compatibility",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "maximum_compatible_migration_id",
                schema: "operations",
                table: "schema_compatibility",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE operations.schema_compatibility
                SET minimum_compatible_migration_id = migration_id,
                    maximum_compatible_migration_id = '20260806071128_ExpandSchemaCompatibilityRange',
                    updated_at = CURRENT_TIMESTAMP
                WHERE singleton
                """);

            migrationBuilder.AlterColumn<string>(
                name: "minimum_compatible_migration_id",
                schema: "operations",
                table: "schema_compatibility",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(150)",
                oldMaxLength: 150,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "maximum_compatible_migration_id",
                schema: "operations",
                table: "schema_compatibility",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(150)",
                oldMaxLength: 150,
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_schema_compatibility_range",
                schema: "operations",
                table: "schema_compatibility",
                sql: "minimum_compatible_migration_id <= maximum_compatible_migration_id");

            migrationBuilder.Sql(
                "ALTER TABLE operations.idempotency_records ALTER COLUMN response_schema_version SET DEFAULT 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_schema_compatibility_range",
                schema: "operations",
                table: "schema_compatibility");

            migrationBuilder.DropColumn(
                name: "minimum_compatible_migration_id",
                schema: "operations",
                table: "schema_compatibility");

            migrationBuilder.DropColumn(
                name: "maximum_compatible_migration_id",
                schema: "operations",
                table: "schema_compatibility");
        }
    }
}
