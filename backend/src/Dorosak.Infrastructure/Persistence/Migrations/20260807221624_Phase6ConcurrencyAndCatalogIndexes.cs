using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dorosak.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase6ConcurrencyAndCatalogIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE operations.schema_compatibility
                SET maximum_compatible_migration_id = '20260807221624_Phase6ConcurrencyAndCatalogIndexes',
                    updated_at = CURRENT_TIMESTAMP
                WHERE singleton
                """);

            migrationBuilder.CreateIndex(
                name: "ix_teacher_applications_submitted_id",
                schema: "profiles",
                table: "teacher_applications",
                columns: new[] { "submitted_at", "id" },
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "ix_publication_reviews_requested_id",
                schema: "authoring",
                table: "publication_reviews",
                columns: new[] { "requested_at", "id" },
                descending: new bool[0]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE operations.schema_compatibility
                SET maximum_compatible_migration_id = '20260807182959_Phase6CatalogAuthoring',
                    updated_at = CURRENT_TIMESTAMP
                WHERE singleton
                """);

            migrationBuilder.DropIndex(
                name: "ix_teacher_applications_submitted_id",
                schema: "profiles",
                table: "teacher_applications");

            migrationBuilder.DropIndex(
                name: "ix_publication_reviews_requested_id",
                schema: "authoring",
                table: "publication_reviews");
        }
    }
}
