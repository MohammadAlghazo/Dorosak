using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dorosak.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase9Communications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE operations.schema_compatibility
                SET maximum_compatible_migration_id = '20260811102150_Phase9Communications',
                    updated_at = CURRENT_TIMESTAMP
                WHERE singleton;
                """);

            migrationBuilder.EnsureSchema(
                name: "communication");

            migrationBuilder.CreateTable(
                name: "conversations",
                schema: "communication",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    course_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_conversations", x => x.id);
                    table.ForeignKey(
                        name: "fk_conversations_courses_course_id",
                        column: x => x.course_id,
                        principalSchema: "catalog",
                        principalTable: "courses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_conversations_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "conversation_participants",
                schema: "communication",
                columns: table => new
                {
                    conversation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    joined_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    left_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_conversation_participants", x => new { x.conversation_id, x.user_id });
                    table.CheckConstraint("ck_conversation_participants_left_at", "left_at IS NULL OR left_at >= joined_at");
                    table.ForeignKey(
                        name: "fk_conversation_participants_conversations_conversation_id",
                        column: x => x.conversation_id,
                        principalSchema: "communication",
                        principalTable: "conversations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_conversation_participants_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "messages",
                schema: "communication",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    conversation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sender_id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    body = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_messages", x => x.id);
                    table.CheckConstraint("ck_messages_body", "char_length(btrim(body)) BETWEEN 1 AND 5000");
                    table.ForeignKey(
                        name: "fk_messages_conversations_conversation_id",
                        column: x => x.conversation_id,
                        principalSchema: "communication",
                        principalTable: "conversations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_messages_participants_conversation_sender_id",
                        columns: x => new { x.conversation_id, x.sender_id },
                        principalSchema: "communication",
                        principalTable: "conversation_participants",
                        principalColumns: new[] { "conversation_id", "user_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_conversation_participants_current_user_conversation",
                schema: "communication",
                table: "conversation_participants",
                columns: new[] { "user_id", "conversation_id" },
                filter: "left_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_conversations_course_id",
                schema: "communication",
                table: "conversations",
                column: "course_id");

            migrationBuilder.CreateIndex(
                name: "ix_conversations_created_by_user_id",
                schema: "communication",
                table: "conversations",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_conversations_updated_id",
                schema: "communication",
                table: "conversations",
                columns: new[] { "updated_at", "id" },
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "ix_messages_conversation_created_id",
                schema: "communication",
                table: "messages",
                columns: new[] { "conversation_id", "created_at", "id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "uq_messages_conversation_sender_client_message",
                schema: "communication",
                table: "messages",
                columns: new[] { "conversation_id", "sender_id", "client_message_id" },
                unique: true);

            migrationBuilder.Sql(
                """
                DO $permissions$
                BEGIN
                    IF EXISTS (SELECT FROM pg_roles WHERE rolname = 'dorosak_runtime') THEN
                        GRANT USAGE ON SCHEMA communication TO dorosak_runtime;
                        GRANT SELECT, INSERT ON
                            communication.conversations,
                            communication.conversation_participants,
                            communication.messages
                            TO dorosak_runtime;
                        GRANT UPDATE (updated_at) ON communication.conversations TO dorosak_runtime;
                        REVOKE DELETE, TRUNCATE ON
                            communication.conversations,
                            communication.conversation_participants,
                            communication.messages
                            FROM dorosak_runtime;
                        REVOKE UPDATE ON
                            communication.conversation_participants,
                            communication.messages
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
                SET maximum_compatible_migration_id = '20260810212341_Phase9ModerationHardening',
                    updated_at = CURRENT_TIMESTAMP
                WHERE singleton;

                DO $permissions$
                BEGIN
                    IF EXISTS (SELECT FROM pg_roles WHERE rolname = 'dorosak_runtime') THEN
                        REVOKE ALL PRIVILEGES ON
                            communication.messages,
                            communication.conversation_participants,
                            communication.conversations
                            FROM dorosak_runtime;
                        REVOKE USAGE ON SCHEMA communication FROM dorosak_runtime;
                    END IF;
                END
                $permissions$;
                """);

            migrationBuilder.DropTable(
                name: "messages",
                schema: "communication");

            migrationBuilder.DropTable(
                name: "conversation_participants",
                schema: "communication");

            migrationBuilder.DropTable(
                name: "conversations",
                schema: "communication");

            migrationBuilder.Sql("DROP SCHEMA IF EXISTS communication;");
        }
    }
}
