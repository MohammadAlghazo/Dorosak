using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dorosak.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase9CommunicationsConsistency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE operations.schema_compatibility
                SET maximum_compatible_migration_id = '20260811143212_Phase9CommunicationsConsistency',
                    updated_at = CURRENT_TIMESTAMP
                WHERE singleton;
                """);

            migrationBuilder.DropForeignKey(
                name: "fk_announcement_targets_notifications_notification_user",
                schema: "communication",
                table: "announcement_targets");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_notifications_id_user_id",
                schema: "communication",
                table: "notifications");

            migrationBuilder.DropCheckConstraint(
                name: "ck_notifications_target_projection",
                schema: "communication",
                table: "notifications");

            migrationBuilder.DropIndex(
                name: "ix_announcement_targets_notification_id_user_id",
                schema: "communication",
                table: "announcement_targets");

            migrationBuilder.AddColumn<Guid>(
                name: "target_announcement_id",
                schema: "communication",
                table: "notifications",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<long>(
                name: "target_announcement_version",
                schema: "communication",
                table: "notifications",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.Sql(
                """
                UPDATE communication.notifications
                SET target_announcement_id = COALESCE(
                        announcement_id,
                        '00000000-0000-0000-0000-000000000000'::uuid),
                    target_announcement_version = COALESCE(announcement_version, 0);
                """);

            migrationBuilder.AddUniqueConstraint(
                name: "ak_notifications_id_user_announcement_version",
                schema: "communication",
                table: "notifications",
                columns: new[] { "id", "user_id", "target_announcement_id", "target_announcement_version" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_notifications_target_projection",
                schema: "communication",
                table: "notifications",
                sql: "(message_id IS NOT NULL AND announcement_id IS NULL AND announcement_version IS NULL AND target_announcement_id = '00000000-0000-0000-0000-000000000000'::uuid AND target_announcement_version = 0 AND title IS NULL AND body IS NULL) OR (message_id IS NULL AND announcement_id IS NOT NULL AND announcement_version IS NOT NULL AND target_announcement_id = announcement_id AND target_announcement_version = announcement_version AND announcement_version > 0 AND title IS NOT NULL AND body IS NOT NULL AND char_length(btrim(title)) BETWEEN 1 AND 200 AND char_length(btrim(body)) BETWEEN 1 AND 10000)");

            migrationBuilder.CreateIndex(
                name: "ix_announcement_targets_notification_id_user_id_announcement_i",
                schema: "communication",
                table: "announcement_targets",
                columns: new[] { "notification_id", "user_id", "announcement_id", "announcement_version" });

            migrationBuilder.AddForeignKey(
                name: "fk_announcement_targets_notifications_projection",
                schema: "communication",
                table: "announcement_targets",
                columns: new[] { "notification_id", "user_id", "announcement_id", "announcement_version" },
                principalSchema: "communication",
                principalTable: "notifications",
                principalColumns: new[] { "id", "user_id", "target_announcement_id", "target_announcement_version" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE operations.schema_compatibility
                SET maximum_compatible_migration_id = '20260811125936_Phase9CommunicationsHardening',
                    updated_at = CURRENT_TIMESTAMP
                WHERE singleton;
                """);

            migrationBuilder.DropForeignKey(
                name: "fk_announcement_targets_notifications_projection",
                schema: "communication",
                table: "announcement_targets");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_notifications_id_user_announcement_version",
                schema: "communication",
                table: "notifications");

            migrationBuilder.DropCheckConstraint(
                name: "ck_notifications_target_projection",
                schema: "communication",
                table: "notifications");

            migrationBuilder.DropIndex(
                name: "ix_announcement_targets_notification_id_user_id_announcement_i",
                schema: "communication",
                table: "announcement_targets");

            migrationBuilder.DropColumn(
                name: "target_announcement_id",
                schema: "communication",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "target_announcement_version",
                schema: "communication",
                table: "notifications");

            migrationBuilder.AddUniqueConstraint(
                name: "ak_notifications_id_user_id",
                schema: "communication",
                table: "notifications",
                columns: new[] { "id", "user_id" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_notifications_target_projection",
                schema: "communication",
                table: "notifications",
                sql: "(message_id IS NOT NULL AND announcement_id IS NULL AND announcement_version IS NULL AND title IS NULL AND body IS NULL) OR (message_id IS NULL AND announcement_id IS NOT NULL AND announcement_version > 0 AND char_length(btrim(title)) BETWEEN 1 AND 200 AND char_length(btrim(body)) BETWEEN 1 AND 10000)");

            migrationBuilder.CreateIndex(
                name: "ix_announcement_targets_notification_id_user_id",
                schema: "communication",
                table: "announcement_targets",
                columns: new[] { "notification_id", "user_id" });

            migrationBuilder.AddForeignKey(
                name: "fk_announcement_targets_notifications_notification_user",
                schema: "communication",
                table: "announcement_targets",
                columns: new[] { "notification_id", "user_id" },
                principalSchema: "communication",
                principalTable: "notifications",
                principalColumns: new[] { "id", "user_id" },
                onDelete: ReferentialAction.Restrict);
        }
    }
}
