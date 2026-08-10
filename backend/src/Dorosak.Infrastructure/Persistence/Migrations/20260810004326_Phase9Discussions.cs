using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dorosak.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase9Discussions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE operations.schema_compatibility
                SET maximum_compatible_migration_id = '20260810004326_Phase9Discussions',
                    updated_at = CURRENT_TIMESTAMP
                WHERE singleton;
                """);

            migrationBuilder.CreateTable(
                name: "discussion_threads",
                schema: "engagement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    course_id = table.Column<Guid>(type: "uuid", nullable: false),
                    release_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lesson_id = table.Column<Guid>(type: "uuid", nullable: true),
                    author_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    body = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    edited_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    removed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_discussion_threads", x => x.id);
                    table.CheckConstraint("ck_discussion_threads_status", "status IN ('Published', 'Hidden', 'Removed')");
                    table.ForeignKey(
                        name: "fk_discussion_threads_courses_course_id",
                        column: x => x.course_id,
                        principalSchema: "catalog",
                        principalTable: "courses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_discussion_threads_lessons_lesson_id",
                        columns: x => new { x.lesson_id, x.release_id },
                        principalSchema: "catalog",
                        principalTable: "course_release_lessons",
                        principalColumns: new[] { "id", "release_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_discussion_threads_releases_release_id",
                        columns: x => new { x.release_id, x.course_id },
                        principalSchema: "catalog",
                        principalTable: "course_releases",
                        principalColumns: new[] { "id", "course_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_discussion_threads_users_author_user_id",
                        column: x => x.author_user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "comments",
                schema: "engagement",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    thread_id = table.Column<Guid>(type: "uuid", nullable: false),
                    parent_comment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    parent_depth = table.Column<int>(type: "integer", nullable: true),
                    author_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    depth = table.Column<int>(type: "integer", nullable: false),
                    body = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    edited_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    removed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_comments", x => x.id);
                    table.UniqueConstraint("ak_comments_id_thread_depth", x => new { x.id, x.thread_id, x.depth });
                    table.CheckConstraint("ck_comments_depth", "depth BETWEEN 0 AND 2");
                    table.CheckConstraint("ck_comments_parent_depth", "(depth = 0 AND parent_comment_id IS NULL AND parent_depth IS NULL) OR (depth BETWEEN 1 AND 2 AND parent_comment_id IS NOT NULL AND parent_depth = depth - 1)");
                    table.CheckConstraint("ck_comments_status", "status IN ('Published', 'Hidden', 'Removed')");
                    table.ForeignKey(
                        name: "fk_comments_parent_thread",
                        columns: x => new { x.parent_comment_id, x.thread_id, x.parent_depth },
                        principalSchema: "engagement",
                        principalTable: "comments",
                        principalColumns: new[] { "id", "thread_id", "depth" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_comments_threads_thread_id",
                        column: x => x.thread_id,
                        principalSchema: "engagement",
                        principalTable: "discussion_threads",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_comments_users_author_user_id",
                        column: x => x.author_user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "comment_likes",
                schema: "engagement",
                columns: table => new
                {
                    comment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_comment_likes", x => new { x.comment_id, x.user_id });
                    table.ForeignKey(
                        name: "fk_comment_likes_comments_comment_id",
                        column: x => x.comment_id,
                        principalSchema: "engagement",
                        principalTable: "comments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_comment_likes_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_comment_likes_user_created",
                schema: "engagement",
                table: "comment_likes",
                columns: new[] { "user_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_comments_author_user_id",
                schema: "engagement",
                table: "comments",
                column: "author_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_comments_parent_thread",
                schema: "engagement",
                table: "comments",
                columns: new[] { "parent_comment_id", "thread_id", "parent_depth" });

            migrationBuilder.CreateIndex(
                name: "ix_comments_thread_created_id",
                schema: "engagement",
                table: "comments",
                columns: new[] { "thread_id", "created_at", "id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "ix_discussion_threads_author_user_id",
                schema: "engagement",
                table: "discussion_threads",
                column: "author_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_discussion_threads_lesson_id_release_id",
                schema: "engagement",
                table: "discussion_threads",
                columns: new[] { "lesson_id", "release_id" });

            migrationBuilder.CreateIndex(
                name: "ix_discussion_threads_release_id_course_id",
                schema: "engagement",
                table: "discussion_threads",
                columns: new[] { "release_id", "course_id" });

            migrationBuilder.CreateIndex(
                name: "ix_discussion_threads_scope_created_id",
                schema: "engagement",
                table: "discussion_threads",
                columns: new[] { "course_id", "release_id", "lesson_id", "created_at", "id" },
                descending: new[] { false, false, false, true, true });

            migrationBuilder.Sql(
                """
                DO $permissions$
                BEGIN
                    IF EXISTS (SELECT FROM pg_roles WHERE rolname = 'dorosak_runtime') THEN
                        GRANT USAGE ON SCHEMA engagement TO dorosak_runtime;
                        GRANT SELECT, INSERT, UPDATE ON
                            engagement.discussion_threads,
                            engagement.comments
                            TO dorosak_runtime;
                        REVOKE DELETE, TRUNCATE ON
                            engagement.discussion_threads,
                            engagement.comments
                            FROM dorosak_runtime;
                        GRANT SELECT, INSERT, DELETE ON engagement.comment_likes TO dorosak_runtime;
                        REVOKE UPDATE, TRUNCATE ON engagement.comment_likes FROM dorosak_runtime;
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
                SET maximum_compatible_migration_id = '20260809195115_Phase9CourseReviews',
                    updated_at = CURRENT_TIMESTAMP
                WHERE singleton;
                DO $permissions$
                BEGIN
                    IF EXISTS (SELECT FROM pg_roles WHERE rolname = 'dorosak_runtime') THEN
                        REVOKE ALL PRIVILEGES ON
                            engagement.comment_likes,
                            engagement.comments,
                            engagement.discussion_threads
                            FROM dorosak_runtime;
                    END IF;
                END
                $permissions$;
                """);

            migrationBuilder.DropTable(
                name: "comment_likes",
                schema: "engagement");

            migrationBuilder.DropTable(
                name: "comments",
                schema: "engagement");

            migrationBuilder.DropTable(
                name: "discussion_threads",
                schema: "engagement");
        }
    }
}
