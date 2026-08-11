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
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "target_announcement_version",
                schema: "communication",
                table: "notifications",
                type: "bigint",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE communication.notifications AS notification
                SET announcement_version = COALESCE(
                        notification.announcement_version,
                        (
                            SELECT target.announcement_version
                            FROM communication.announcement_targets AS target
                            WHERE target.notification_id = notification.id
                            LIMIT 1
                        ),
                        announcement.version),
                    title = COALESCE(NULLIF(btrim(notification.title), ''), announcement.title),
                    body = COALESCE(NULLIF(btrim(notification.body), ''), announcement.body)
                FROM communication.announcements AS announcement
                WHERE notification.message_id IS NULL
                  AND notification.announcement_id = announcement.id;

                UPDATE communication.notifications
                SET announcement_version = NULL,
                    title = NULL,
                    body = NULL,
                    target_announcement_id = '00000000-0000-0000-0000-000000000000'::uuid,
                    target_announcement_version = 0
                WHERE message_id IS NOT NULL
                  AND announcement_id IS NULL;

                UPDATE communication.notifications
                SET target_announcement_id = announcement_id,
                    target_announcement_version = announcement_version
                WHERE message_id IS NULL
                  AND announcement_id IS NOT NULL
                  AND announcement_version IS NOT NULL
                  AND announcement_version > 0
                  AND title IS NOT NULL
                  AND body IS NOT NULL
                  AND char_length(btrim(title)) BETWEEN 1 AND 200
                  AND char_length(btrim(body)) BETWEEN 1 AND 10000;

                DELETE FROM communication.announcement_targets AS target
                USING communication.notifications AS notification
                WHERE target.notification_id = notification.id
                  AND NOT (
                    notification.message_id IS NULL
                    AND notification.announcement_id IS NOT NULL
                    AND notification.announcement_version IS NOT NULL
                    AND notification.announcement_version > 0
                    AND notification.title IS NOT NULL
                    AND notification.body IS NOT NULL
                    AND char_length(btrim(notification.title)) BETWEEN 1 AND 200
                    AND char_length(btrim(notification.body)) BETWEEN 1 AND 10000
                  );

                DELETE FROM communication.notifications
                WHERE NOT (
                    (message_id IS NOT NULL AND announcement_id IS NULL)
                    OR
                    (message_id IS NULL
                        AND announcement_id IS NOT NULL
                        AND announcement_version IS NOT NULL
                        AND announcement_version > 0
                        AND title IS NOT NULL
                        AND body IS NOT NULL
                        AND char_length(btrim(title)) BETWEEN 1 AND 200
                        AND char_length(btrim(body)) BETWEEN 1 AND 10000)
                );

                DELETE FROM communication.announcement_targets AS target
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM communication.notifications AS notification
                    WHERE notification.id = target.notification_id
                      AND notification.user_id = target.user_id
                      AND notification.announcement_id = target.announcement_id
                      AND notification.announcement_version = target.announcement_version
                );

                INSERT INTO communication.announcement_targets (
                    announcement_id,
                    user_id,
                    announcement_version,
                    notification_id,
                    created_at)
                SELECT notification.announcement_id,
                       notification.user_id,
                       notification.announcement_version,
                       notification.id,
                       notification.created_at
                FROM communication.notifications AS notification
                WHERE notification.message_id IS NULL
                  AND notification.announcement_id IS NOT NULL
                  AND notification.announcement_version IS NOT NULL
                  AND NOT EXISTS (
                    SELECT 1
                    FROM communication.announcement_targets AS target
                    WHERE target.notification_id = notification.id
                  )
                ON CONFLICT DO NOTHING;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "target_announcement_id",
                schema: "communication",
                table: "notifications",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "target_announcement_version",
                schema: "communication",
                table: "notifications",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

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

            migrationBuilder.Sql(
                """
                UPDATE operations.schema_compatibility
                SET minimum_compatible_migration_id = '20260811143212_Phase9CommunicationsConsistency',
                    maximum_compatible_migration_id = '20260811143212_Phase9CommunicationsConsistency',
                    updated_at = CURRENT_TIMESTAMP
                WHERE singleton;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE operations.schema_compatibility
                SET minimum_compatible_migration_id = '20260806063112_AddSchemaCompatibilityMarker',
                    maximum_compatible_migration_id = '20260811125936_Phase9CommunicationsHardening',
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
