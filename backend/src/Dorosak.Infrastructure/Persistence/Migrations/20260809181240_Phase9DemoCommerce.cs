using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dorosak.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase9DemoCommerce : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE operations.schema_compatibility
                SET maximum_compatible_migration_id = '20260809181240_Phase9DemoCommerce',
                    updated_at = CURRENT_TIMESTAMP
                WHERE singleton;
                """);

            migrationBuilder.DropCheckConstraint(
                name: "ck_entitlements_source",
                schema: "learning",
                table: "entitlements");

            migrationBuilder.EnsureSchema(
                name: "commerce");

            migrationBuilder.CreateTable(
                name: "demo_orders",
                schema: "commerce",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    course_id = table.Column<Guid>(type: "uuid", nullable: false),
                    currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    total_credits = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_demo_orders", x => x.id);
                    table.CheckConstraint("ck_demo_orders_currency", "currency = 'DEMO'");
                    table.CheckConstraint("ck_demo_orders_status", "status IN ('Pending', 'Completed', 'Failed')");
                    table.CheckConstraint("ck_demo_orders_total", "total_credits > 0");
                    table.ForeignKey(
                        name: "fk_demo_orders_courses_course_id",
                        column: x => x.course_id,
                        principalSchema: "catalog",
                        principalTable: "courses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_demo_orders_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "demo_payments",
                schema: "commerce",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    provider_reference = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    amount_credits = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_demo_payments", x => x.id);
                    table.CheckConstraint("ck_demo_payments_amount", "amount_credits > 0");
                    table.CheckConstraint("ck_demo_payments_currency", "currency = 'DEMO'");
                    table.CheckConstraint("ck_demo_payments_provider", "provider = 'DemoProvider'");
                    table.CheckConstraint("ck_demo_payments_status", "status IN ('Succeeded', 'Failed')");
                    table.ForeignKey(
                        name: "fk_demo_payments_demo_orders_order_id",
                        column: x => x.order_id,
                        principalSchema: "commerce",
                        principalTable: "demo_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_entitlements_source",
                schema: "learning",
                table: "entitlements",
                sql: "source IN ('Free', 'Demo')");

            migrationBuilder.CreateIndex(
                name: "ix_demo_orders_course_id",
                schema: "commerce",
                table: "demo_orders",
                column: "course_id");

            migrationBuilder.CreateIndex(
                name: "ix_demo_orders_user_created_id",
                schema: "commerce",
                table: "demo_orders",
                columns: new[] { "user_id", "created_at", "id" },
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "uq_demo_payments_order_id",
                schema: "commerce",
                table: "demo_payments",
                column: "order_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_demo_payments_provider_reference",
                schema: "commerce",
                table: "demo_payments",
                column: "provider_reference",
                unique: true);

            migrationBuilder.Sql(
                """
                DO $permissions$
                BEGIN
                    IF EXISTS (SELECT FROM pg_roles WHERE rolname = 'dorosak_runtime') THEN
                        GRANT USAGE ON SCHEMA commerce TO dorosak_runtime;
                        GRANT SELECT, INSERT ON
                            commerce.demo_orders,
                            commerce.demo_payments
                            TO dorosak_runtime;
                        REVOKE UPDATE, DELETE, TRUNCATE ON
                            commerce.demo_orders,
                            commerce.demo_payments
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
                SET maximum_compatible_migration_id = '20260809123709_Phase9EngagementAssignmentAttachments',
                    updated_at = CURRENT_TIMESTAMP
                WHERE singleton;

                DO $permissions$
                BEGIN
                    IF EXISTS (SELECT FROM pg_roles WHERE rolname = 'dorosak_runtime') THEN
                        REVOKE ALL PRIVILEGES ON
                            commerce.demo_orders,
                            commerce.demo_payments
                            FROM dorosak_runtime;
                    END IF;
                END
                $permissions$;
                """);

            migrationBuilder.DropTable(
                name: "demo_payments",
                schema: "commerce");

            migrationBuilder.DropTable(
                name: "demo_orders",
                schema: "commerce");

            migrationBuilder.DropCheckConstraint(
                name: "ck_entitlements_source",
                schema: "learning",
                table: "entitlements");

            migrationBuilder.AddCheckConstraint(
                name: "ck_entitlements_source",
                schema: "learning",
                table: "entitlements",
                sql: "source = 'Free'");
        }
    }
}
