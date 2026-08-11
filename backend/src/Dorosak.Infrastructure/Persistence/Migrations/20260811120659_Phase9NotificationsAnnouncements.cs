using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dorosak.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase9NotificationsAnnouncements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE operations.schema_compatibility
                SET maximum_compatible_migration_id = '20260811120659_Phase9NotificationsAnnouncements',
                    updated_at = CURRENT_TIMESTAMP
                WHERE singleton;
                """);

            migrationBuilder.AddColumn<long>(
                name: "sequence",
                schema: "communication",
                table: "messages",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "last_sequence",
                schema: "communication",
                table: "conversations",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.Sql(
                """
                WITH ranked_messages AS (
                    SELECT id,
                           row_number() OVER (
                               PARTITION BY conversation_id
                               ORDER BY created_at, id) AS sequence
                    FROM communication.messages
                )
                UPDATE communication.messages AS message
                SET sequence = ranked.sequence
                FROM ranked_messages AS ranked
                WHERE message.id = ranked.id;

                UPDATE communication.conversations AS conversation
                SET last_sequence = COALESCE((
                    SELECT max(message.sequence)
                    FROM communication.messages AS message
                    WHERE message.conversation_id = conversation.id), 0);
                """);

            migrationBuilder.AlterColumn<long>(
                name: "sequence",
                schema: "communication",
                table: "messages",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "announcements",
                schema: "communication",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    course_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    body = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by_user_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_announcements", x => x.id);
                    table.CheckConstraint("ck_announcements_body", "char_length(btrim(body)) BETWEEN 1 AND 10000");
                    table.CheckConstraint("ck_announcements_deleted", "(deleted_at IS NULL AND deleted_by_user_id IS NULL) OR (deleted_at IS NOT NULL AND deleted_by_user_id IS NOT NULL)");
                    table.CheckConstraint("ck_announcements_title", "char_length(btrim(title)) BETWEEN 1 AND 200");
                    table.CheckConstraint("ck_announcements_version", "version > 0");
                    table.ForeignKey(
                        name: "fk_announcements_courses_course_id",
                        column: x => x.course_id,
                        principalSchema: "catalog",
                        principalTable: "courses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_announcements_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_announcements_users_deleted_by_user_id",
                        column: x => x.deleted_by_user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "notification_sequences",
                schema: "communication",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    last_sequence = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notification_sequences", x => x.user_id);
                    table.CheckConstraint("ck_notification_sequences_last_sequence", "last_sequence >= 0");
                    table.ForeignKey(
                        name: "fk_notification_sequences_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "notifications",
                schema: "communication",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sequence = table.Column<long>(type: "bigint", nullable: false),
                    message_id = table.Column<Guid>(type: "uuid", nullable: true),
                    announcement_id = table.Column<Guid>(type: "uuid", nullable: true),
                    announcement_version = table.Column<long>(type: "bigint", nullable: true),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    body = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    is_read = table.Column<bool>(type: "boolean", nullable: false),
                    read_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notifications", x => x.id);
                    table.UniqueConstraint("ak_notifications_id_user_id", x => new { x.id, x.user_id });
                    table.CheckConstraint("ck_notifications_read_state", "is_read = (read_at IS NOT NULL)");
                    table.CheckConstraint("ck_notifications_sequence", "sequence > 0");
                    table.CheckConstraint("ck_notifications_target_projection", "(message_id IS NOT NULL AND announcement_id IS NULL AND announcement_version IS NULL AND title IS NULL AND body IS NULL) OR (message_id IS NULL AND announcement_id IS NOT NULL AND announcement_version > 0 AND char_length(btrim(title)) BETWEEN 1 AND 200 AND char_length(btrim(body)) BETWEEN 1 AND 10000)");
                    table.ForeignKey(
                        name: "fk_notifications_announcements_announcement_id",
                        column: x => x.announcement_id,
                        principalSchema: "communication",
                        principalTable: "announcements",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_notifications_messages_message_id",
                        column: x => x.message_id,
                        principalSchema: "communication",
                        principalTable: "messages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_notifications_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "announcement_targets",
                schema: "communication",
                columns: table => new
                {
                    announcement_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    announcement_version = table.Column<long>(type: "bigint", nullable: false),
                    notification_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_announcement_targets", x => new { x.announcement_id, x.user_id, x.announcement_version });
                    table.CheckConstraint("ck_announcement_targets_version", "announcement_version > 0");
                    table.ForeignKey(
                        name: "fk_announcement_targets_announcements_announcement_id",
                        column: x => x.announcement_id,
                        principalSchema: "communication",
                        principalTable: "announcements",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_announcement_targets_notifications_notification_user",
                        columns: x => new { x.notification_id, x.user_id },
                        principalSchema: "communication",
                        principalTable: "notifications",
                        principalColumns: new[] { "id", "user_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_announcement_targets_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "uq_messages_conversation_sequence",
                schema: "communication",
                table: "messages",
                columns: new[] { "conversation_id", "sequence" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_messages_sequence",
                schema: "communication",
                table: "messages",
                sql: "sequence > 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_conversations_last_sequence",
                schema: "communication",
                table: "conversations",
                sql: "last_sequence >= 0");

            migrationBuilder.CreateIndex(
                name: "ix_announcement_targets_notification_id_user_id",
                schema: "communication",
                table: "announcement_targets",
                columns: new[] { "notification_id", "user_id" });

            migrationBuilder.CreateIndex(
                name: "ix_announcement_targets_user_created_at",
                schema: "communication",
                table: "announcement_targets",
                columns: new[] { "user_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "uq_announcement_targets_notification_id",
                schema: "communication",
                table: "announcement_targets",
                column: "notification_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_announcements_course_created_id",
                schema: "communication",
                table: "announcements",
                columns: new[] { "course_id", "created_at", "id" },
                descending: new[] { false, true, true },
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_announcements_created_by_user_id",
                schema: "communication",
                table: "announcements",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_announcements_deleted_by_user_id",
                schema: "communication",
                table: "announcements",
                column: "deleted_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_announcement_id",
                schema: "communication",
                table: "notifications",
                column: "announcement_id");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_message_id",
                schema: "communication",
                table: "notifications",
                column: "message_id");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_user_read_created_id",
                schema: "communication",
                table: "notifications",
                columns: new[] { "user_id", "is_read", "created_at", "id" },
                descending: new[] { false, false, true, true });

            migrationBuilder.CreateIndex(
                name: "uq_notifications_user_sequence",
                schema: "communication",
                table: "notifications",
                columns: new[] { "user_id", "sequence" },
                unique: true);

            migrationBuilder.Sql(
                """
                DO $permissions$
                BEGIN
                    IF EXISTS (SELECT FROM pg_roles WHERE rolname = 'dorosak_runtime') THEN
                        GRANT SELECT, INSERT ON
                            communication.announcements,
                            communication.notification_sequences,
                            communication.notifications,
                            communication.announcement_targets
                            TO dorosak_runtime;

                        REVOKE UPDATE, DELETE, TRUNCATE ON
                            communication.announcement_targets,
                            communication.notification_sequences,
                            communication.notifications,
                            communication.announcements
                            FROM dorosak_runtime;

                        GRANT UPDATE (last_sequence)
                            ON communication.notification_sequences
                            TO dorosak_runtime;
                        GRANT UPDATE (is_read, read_at)
                            ON communication.notifications
                            TO dorosak_runtime;
                        GRANT UPDATE (title, body, version, updated_at, deleted_at, deleted_by_user_id)
                            ON communication.announcements
                            TO dorosak_runtime;
                        GRANT UPDATE (last_sequence)
                            ON communication.conversations
                            TO dorosak_runtime;
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
                SET maximum_compatible_migration_id = '20260811102150_Phase9Communications',
                    updated_at = CURRENT_TIMESTAMP
                WHERE singleton;

                DO $permissions$
                BEGIN
                    IF EXISTS (SELECT FROM pg_roles WHERE rolname = 'dorosak_runtime') THEN
                        REVOKE ALL PRIVILEGES ON
                            communication.announcement_targets,
                            communication.notifications,
                            communication.notification_sequences,
                            communication.announcements
                            FROM dorosak_runtime;
                        REVOKE UPDATE (last_sequence)
                            ON communication.conversations
                            FROM dorosak_runtime;
                    END IF;
                END
                $permissions$;
                """);

            migrationBuilder.DropTable(
                name: "announcement_targets",
                schema: "communication");

            migrationBuilder.DropTable(
                name: "notification_sequences",
                schema: "communication");

            migrationBuilder.DropTable(
                name: "notifications",
                schema: "communication");

            migrationBuilder.DropTable(
                name: "announcements",
                schema: "communication");

            migrationBuilder.DropIndex(
                name: "uq_messages_conversation_sequence",
                schema: "communication",
                table: "messages");

            migrationBuilder.DropCheckConstraint(
                name: "ck_messages_sequence",
                schema: "communication",
                table: "messages");

            migrationBuilder.DropCheckConstraint(
                name: "ck_conversations_last_sequence",
                schema: "communication",
                table: "conversations");

            migrationBuilder.DropColumn(
                name: "sequence",
                schema: "communication",
                table: "messages");

            migrationBuilder.DropColumn(
                name: "last_sequence",
                schema: "communication",
                table: "conversations");
        }
    }
}
