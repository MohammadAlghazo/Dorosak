using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dorosak.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class VersionIdempotencyResponses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "response_schema_version",
                schema: "operations",
                table: "idempotency_records",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddCheckConstraint(
                name: "ck_idempotency_records_response_schema_version",
                schema: "operations",
                table: "idempotency_records",
                sql: "response_schema_version > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_idempotency_records_response_schema_version",
                schema: "operations",
                table: "idempotency_records");

            migrationBuilder.DropColumn(
                name: "response_schema_version",
                schema: "operations",
                table: "idempotency_records");
        }
    }
}
