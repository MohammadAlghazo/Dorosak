using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dorosak.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSchemaCompatibilityMarker : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "schema_compatibility",
                schema: "operations",
                columns: table => new
                {
                    singleton = table.Column<bool>(type: "boolean", nullable: false),
                    migration_id = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_schema_compatibility", x => x.singleton);
                    table.CheckConstraint("ck_schema_compatibility_singleton", "singleton");
                });

            migrationBuilder.Sql(
                "INSERT INTO operations.schema_compatibility (singleton, migration_id, updated_at) " +
                "VALUES (TRUE, '20260806063112_AddSchemaCompatibilityMarker', CURRENT_TIMESTAMP)");
            migrationBuilder.Sql(
                """
                DO $permissions$
                BEGIN
                    IF EXISTS (SELECT FROM pg_roles WHERE rolname = 'dorosak_runtime') THEN
                        REVOKE INSERT, UPDATE, DELETE, TRUNCATE
                            ON operations.schema_compatibility
                            FROM dorosak_runtime;
                    END IF;
                END
                $permissions$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "schema_compatibility",
                schema: "operations");
        }
    }
}
