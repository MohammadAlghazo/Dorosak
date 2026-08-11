using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dorosak.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase9MessageReporting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_content_reports_exact_target",
                schema: "engagement",
                table: "content_reports");

            migrationBuilder.AddColumn<string>(
                name: "message_body_snapshot",
                schema: "engagement",
                table: "content_reports",
                type: "character varying(5000)",
                maxLength: 5000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "message_conversation_id_snapshot",
                schema: "engagement",
                table: "content_reports",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "message_course_id_snapshot",
                schema: "engagement",
                table: "content_reports",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "message_course_title_snapshot",
                schema: "engagement",
                table: "content_reports",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "message_created_at_snapshot",
                schema: "engagement",
                table: "content_reports",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "message_id",
                schema: "engagement",
                table: "content_reports",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "message_sender_name_snapshot",
                schema: "engagement",
                table: "content_reports",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "message_sender_user_id_snapshot",
                schema: "engagement",
                table: "content_reports",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "message_sequence_snapshot",
                schema: "engagement",
                table: "content_reports",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_content_reports_message_id",
                schema: "engagement",
                table: "content_reports",
                column: "message_id");

            migrationBuilder.CreateIndex(
                name: "uq_content_reports_open_message",
                schema: "engagement",
                table: "content_reports",
                columns: new[] { "reporter_user_id", "message_id" },
                unique: true,
                filter: "message_id IS NOT NULL AND status IN ('Open', 'InReview')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_content_reports_exact_target",
                schema: "engagement",
                table: "content_reports",
                sql: "num_nonnulls(course_id, review_id, comment_id, reported_user_id, message_id) = 1");

            migrationBuilder.AddCheckConstraint(
                name: "ck_content_reports_message_snapshot",
                schema: "engagement",
                table: "content_reports",
                sql: "(message_id IS NULL AND message_body_snapshot IS NULL AND message_sender_user_id_snapshot IS NULL AND message_sender_name_snapshot IS NULL AND message_course_id_snapshot IS NULL AND message_course_title_snapshot IS NULL AND message_conversation_id_snapshot IS NULL AND message_sequence_snapshot IS NULL AND message_created_at_snapshot IS NULL) OR (message_id IS NOT NULL AND message_body_snapshot IS NOT NULL AND char_length(btrim(message_body_snapshot)) BETWEEN 1 AND 5000 AND message_sender_user_id_snapshot IS NOT NULL AND message_sender_user_id_snapshot <> reporter_user_id AND message_sender_name_snapshot IS NOT NULL AND char_length(btrim(message_sender_name_snapshot)) BETWEEN 1 AND 100 AND message_course_id_snapshot IS NOT NULL AND message_course_title_snapshot IS NOT NULL AND char_length(btrim(message_course_title_snapshot)) BETWEEN 1 AND 200 AND message_conversation_id_snapshot IS NOT NULL AND message_sequence_snapshot > 0 AND message_created_at_snapshot IS NOT NULL)");

            migrationBuilder.AddForeignKey(
                name: "fk_content_reports_messages_message_id",
                schema: "engagement",
                table: "content_reports",
                column: "message_id",
                principalSchema: "communication",
                principalTable: "messages",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql(
                """
                DO $permissions$
                BEGIN
                    IF EXISTS (SELECT FROM pg_roles WHERE rolname = 'dorosak_runtime') THEN
                        GRANT USAGE ON SCHEMA engagement, communication TO dorosak_runtime;
                        GRANT SELECT, INSERT ON engagement.content_reports TO dorosak_runtime;
                        GRANT SELECT ON communication.conversations, communication.messages TO dorosak_runtime;
                        REVOKE UPDATE (
                            message_id,
                            message_body_snapshot,
                            message_sender_user_id_snapshot,
                            message_sender_name_snapshot,
                            message_course_id_snapshot,
                            message_course_title_snapshot,
                            message_conversation_id_snapshot,
                            message_sequence_snapshot,
                            message_created_at_snapshot)
                            ON engagement.content_reports FROM dorosak_runtime;
                    END IF;
                END
                $permissions$;

                UPDATE operations.schema_compatibility
                SET maximum_compatible_migration_id = '20260811180529_Phase9MessageReporting',
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
                SET maximum_compatible_migration_id = '20260811143212_Phase9CommunicationsConsistency',
                    updated_at = CURRENT_TIMESTAMP
                WHERE singleton;
                """);

            migrationBuilder.DropForeignKey(
                name: "fk_content_reports_messages_message_id",
                schema: "engagement",
                table: "content_reports");

            migrationBuilder.DropIndex(
                name: "ix_content_reports_message_id",
                schema: "engagement",
                table: "content_reports");

            migrationBuilder.DropIndex(
                name: "uq_content_reports_open_message",
                schema: "engagement",
                table: "content_reports");

            migrationBuilder.DropCheckConstraint(
                name: "ck_content_reports_exact_target",
                schema: "engagement",
                table: "content_reports");

            migrationBuilder.DropCheckConstraint(
                name: "ck_content_reports_message_snapshot",
                schema: "engagement",
                table: "content_reports");

            migrationBuilder.DropColumn(
                name: "message_body_snapshot",
                schema: "engagement",
                table: "content_reports");

            migrationBuilder.DropColumn(
                name: "message_conversation_id_snapshot",
                schema: "engagement",
                table: "content_reports");

            migrationBuilder.DropColumn(
                name: "message_course_id_snapshot",
                schema: "engagement",
                table: "content_reports");

            migrationBuilder.DropColumn(
                name: "message_course_title_snapshot",
                schema: "engagement",
                table: "content_reports");

            migrationBuilder.DropColumn(
                name: "message_created_at_snapshot",
                schema: "engagement",
                table: "content_reports");

            migrationBuilder.DropColumn(
                name: "message_id",
                schema: "engagement",
                table: "content_reports");

            migrationBuilder.DropColumn(
                name: "message_sender_name_snapshot",
                schema: "engagement",
                table: "content_reports");

            migrationBuilder.DropColumn(
                name: "message_sender_user_id_snapshot",
                schema: "engagement",
                table: "content_reports");

            migrationBuilder.DropColumn(
                name: "message_sequence_snapshot",
                schema: "engagement",
                table: "content_reports");

            migrationBuilder.AddCheckConstraint(
                name: "ck_content_reports_exact_target",
                schema: "engagement",
                table: "content_reports",
                sql: "num_nonnulls(course_id, review_id, comment_id, reported_user_id) = 1");
        }
    }
}
