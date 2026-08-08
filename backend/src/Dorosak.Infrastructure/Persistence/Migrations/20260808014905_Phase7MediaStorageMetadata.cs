using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Dorosak.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase7MediaStorageMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE operations.schema_compatibility
                SET maximum_compatible_migration_id = '20260808014905_Phase7MediaStorageMetadata',
                    updated_at = CURRENT_TIMESTAMP
                WHERE singleton
                """);

            migrationBuilder.AddColumn<string>(
                name: "storage_container",
                schema: "media",
                table: "media_variants",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "storage_provider",
                schema: "media",
                table: "media_variants",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "storage_container",
                schema: "media",
                table: "media_assets",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "storage_provider",
                schema: "media",
                table: "media_assets",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            // Seed order changes Student permissions. Temporarily move values away from the unique key
            // so EF's deterministic per-row seed updates cannot collide while IDs are shifted.
            migrationBuilder.Sql(
                "UPDATE identity.role_claims SET claim_value = '__phase7_seed_' || id::text");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 1,
                column: "claim_value",
                value: "Media.UploadOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 2,
                column: "claim_value",
                value: "Media.ReadOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 3,
                column: "claim_value",
                value: "Profile.ReadOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 4,
                column: "claim_value",
                value: "Profile.UpdateOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 5,
                column: "claim_value",
                value: "Security.ManageOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 6,
                column: "claim_value",
                value: "Sessions.ManageOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 7,
                column: "claim_value",
                value: "TeacherApplication.CreateOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 8,
                column: "claim_value",
                value: "Enrollment.CreateOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 9,
                column: "claim_value",
                value: "Enrollment.ReadOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 10,
                column: "claim_value",
                value: "Learning.AccessOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 11,
                column: "claim_value",
                value: "Progress.UpdateOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 12,
                column: "claim_value",
                value: "Quiz.AttemptOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 13,
                column: "claim_value",
                value: "Assignment.SubmitOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 14,
                column: "claim_value",
                value: "Review.ManageOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 15,
                column: "claim_value",
                value: "Discussion.Participate");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 16,
                column: "claim_value",
                value: "Comment.ManageOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 17,
                column: "claim_value",
                value: "Message.SendAsSelf");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 18,
                column: "claim_value",
                value: "Conversation.ReadOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 19,
                column: "claim_value",
                value: "Notification.ReadOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 20,
                column: "claim_value",
                value: "Certificate.ReadOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 21,
                column: "claim_value",
                value: "Certificate.VerifyPublic");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 22,
                column: "claim_value",
                value: "Order.ReadOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 23,
                columns: new[] { "claim_value", "role_id" },
                values: new object[] { "Checkout.CreateOwn", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546001") });

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 24,
                columns: new[] { "claim_value", "role_id" },
                values: new object[] { "Subscription.ManageOwn", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546001") });

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 25,
                column: "claim_value",
                value: "Media.UploadOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 26,
                column: "claim_value",
                value: "Media.ReadOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 27,
                column: "claim_value",
                value: "Profile.ReadOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 28,
                column: "claim_value",
                value: "Profile.UpdateOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 29,
                column: "claim_value",
                value: "Security.ManageOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 30,
                column: "claim_value",
                value: "Sessions.ManageOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 31,
                column: "claim_value",
                value: "TeacherApplication.CreateOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 32,
                column: "claim_value",
                value: "Enrollment.CreateOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 33,
                column: "claim_value",
                value: "Enrollment.ReadOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 34,
                column: "claim_value",
                value: "Learning.AccessOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 35,
                column: "claim_value",
                value: "Progress.UpdateOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 36,
                column: "claim_value",
                value: "Quiz.AttemptOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 37,
                column: "claim_value",
                value: "Assignment.SubmitOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 38,
                column: "claim_value",
                value: "Review.ManageOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 39,
                column: "claim_value",
                value: "Discussion.Participate");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 40,
                column: "claim_value",
                value: "Comment.ManageOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 41,
                column: "claim_value",
                value: "Message.SendAsSelf");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 42,
                column: "claim_value",
                value: "Conversation.ReadOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 43,
                column: "claim_value",
                value: "Notification.ReadOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 44,
                column: "claim_value",
                value: "Certificate.ReadOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 45,
                column: "claim_value",
                value: "Certificate.VerifyPublic");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 46,
                column: "claim_value",
                value: "Order.ReadOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 47,
                column: "claim_value",
                value: "Checkout.CreateOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 48,
                column: "claim_value",
                value: "Subscription.ManageOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 49,
                column: "claim_value",
                value: "Course.Create");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 50,
                column: "claim_value",
                value: "Course.ReadOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 51,
                column: "claim_value",
                value: "Course.UpdateOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 52,
                column: "claim_value",
                value: "Course.DeleteOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 53,
                column: "claim_value",
                value: "Course.SubmitOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 54,
                column: "claim_value",
                value: "Learning.ViewCourseLearners");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 55,
                column: "claim_value",
                value: "Submission.GradeCourse");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 56,
                column: "claim_value",
                value: "Assessment.ManageCourse");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 57,
                column: "claim_value",
                value: "Announcement.ManageCourse");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 58,
                columns: new[] { "claim_value", "role_id" },
                values: new object[] { "Commerce.ReadEarningsOwn", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546002") });

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 59,
                columns: new[] { "claim_value", "role_id" },
                values: new object[] { "Commerce.ManagePayoutAccountOwn", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546002") });

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 60,
                column: "claim_value",
                value: "Media.UploadOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 61,
                column: "claim_value",
                value: "Media.ReadOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 62,
                column: "claim_value",
                value: "Profile.ReadOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 63,
                column: "claim_value",
                value: "Profile.UpdateOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 64,
                column: "claim_value",
                value: "Security.ManageOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 65,
                column: "claim_value",
                value: "Sessions.ManageOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 66,
                column: "claim_value",
                value: "TeacherApplication.CreateOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 67,
                column: "claim_value",
                value: "Enrollment.CreateOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 68,
                column: "claim_value",
                value: "Enrollment.ReadOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 69,
                column: "claim_value",
                value: "Learning.AccessOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 70,
                column: "claim_value",
                value: "Progress.UpdateOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 71,
                column: "claim_value",
                value: "Quiz.AttemptOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 72,
                column: "claim_value",
                value: "Assignment.SubmitOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 73,
                column: "claim_value",
                value: "Review.ManageOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 74,
                column: "claim_value",
                value: "Discussion.Participate");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 75,
                column: "claim_value",
                value: "Comment.ManageOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 76,
                column: "claim_value",
                value: "Message.SendAsSelf");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 77,
                column: "claim_value",
                value: "Conversation.ReadOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 78,
                column: "claim_value",
                value: "Notification.ReadOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 79,
                column: "claim_value",
                value: "Certificate.ReadOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 80,
                column: "claim_value",
                value: "Certificate.VerifyPublic");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 81,
                column: "claim_value",
                value: "Order.ReadOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 82,
                column: "claim_value",
                value: "Checkout.CreateOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 83,
                column: "claim_value",
                value: "Subscription.ManageOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 84,
                column: "claim_value",
                value: "Course.Create");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 85,
                column: "claim_value",
                value: "Course.ReadOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 86,
                column: "claim_value",
                value: "Course.UpdateOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 87,
                column: "claim_value",
                value: "Course.DeleteOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 88,
                column: "claim_value",
                value: "Course.SubmitOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 89,
                column: "claim_value",
                value: "Learning.ViewCourseLearners");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 90,
                column: "claim_value",
                value: "Submission.GradeCourse");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 91,
                column: "claim_value",
                value: "Assessment.ManageCourse");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 92,
                column: "claim_value",
                value: "Announcement.ManageCourse");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 93,
                column: "claim_value",
                value: "Commerce.ReadEarningsOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 94,
                column: "claim_value",
                value: "Commerce.ManagePayoutAccountOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 95,
                column: "claim_value",
                value: "TeacherApplication.ReviewAny");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 96,
                column: "claim_value",
                value: "Course.ReviewAny");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 97,
                column: "claim_value",
                value: "Course.PublishAny");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 98,
                column: "claim_value",
                value: "Course.ManageAny");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 99,
                column: "claim_value",
                value: "Media.ManageAny");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 100,
                column: "claim_value",
                value: "Moderation.ReviewAny");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 101,
                column: "claim_value",
                value: "Certificate.RevokeAny");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 102,
                column: "claim_value",
                value: "Commerce.ManageOffers");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 103,
                column: "claim_value",
                value: "Commerce.ManageOrders");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 104,
                column: "claim_value",
                value: "Commerce.ManageRefunds");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 105,
                column: "claim_value",
                value: "User.ReadAny");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 106,
                column: "claim_value",
                value: "User.ManageAny");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 107,
                column: "claim_value",
                value: "Role.ManageAny");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 108,
                column: "claim_value",
                value: "Catalog.ManageTaxonomy");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 109,
                column: "claim_value",
                value: "Cms.Manage");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 110,
                column: "claim_value",
                value: "Settings.Manage");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 111,
                column: "claim_value",
                value: "FeatureFlag.Manage");

            migrationBuilder.InsertData(
                schema: "identity",
                table: "role_claims",
                columns: new[] { "id", "claim_type", "claim_value", "role_id" },
                values: new object[,]
                {
                    { 112, "permission", "Analytics.Read", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546003") },
                    { 113, "permission", "Audit.Read", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546003") }
                });

            migrationBuilder.Sql(
                """
                DO $permissions$
                BEGIN
                    IF EXISTS (SELECT FROM pg_roles WHERE rolname = 'dorosak_runtime') THEN
                        GRANT USAGE ON SCHEMA media TO dorosak_runtime;
                        GRANT SELECT, INSERT, UPDATE, DELETE
                            ON ALL TABLES IN SCHEMA media
                            TO dorosak_runtime;
                        REVOKE UPDATE, DELETE, TRUNCATE ON operations.audit_logs FROM dorosak_runtime;
                        GRANT SELECT, INSERT ON operations.audit_logs TO dorosak_runtime;
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
                SET maximum_compatible_migration_id = '20260808011129_Phase7Media',
                    updated_at = CURRENT_TIMESTAMP
                WHERE singleton
                """);

            migrationBuilder.Sql(
                "UPDATE identity.role_claims SET claim_value = '__phase7_seed_down_' || id::text");

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 112);

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 113);

            migrationBuilder.DropColumn(
                name: "storage_container",
                schema: "media",
                table: "media_variants");

            migrationBuilder.DropColumn(
                name: "storage_provider",
                schema: "media",
                table: "media_variants");

            migrationBuilder.DropColumn(
                name: "storage_container",
                schema: "media",
                table: "media_assets");

            migrationBuilder.DropColumn(
                name: "storage_provider",
                schema: "media",
                table: "media_assets");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 1,
                column: "claim_value",
                value: "Profile.ReadOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 2,
                column: "claim_value",
                value: "Profile.UpdateOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 3,
                column: "claim_value",
                value: "Security.ManageOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 4,
                column: "claim_value",
                value: "Sessions.ManageOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 5,
                column: "claim_value",
                value: "TeacherApplication.CreateOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 6,
                column: "claim_value",
                value: "Enrollment.CreateOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 7,
                column: "claim_value",
                value: "Enrollment.ReadOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 8,
                column: "claim_value",
                value: "Learning.AccessOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 9,
                column: "claim_value",
                value: "Progress.UpdateOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 10,
                column: "claim_value",
                value: "Quiz.AttemptOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 11,
                column: "claim_value",
                value: "Assignment.SubmitOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 12,
                column: "claim_value",
                value: "Review.ManageOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 13,
                column: "claim_value",
                value: "Discussion.Participate");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 14,
                column: "claim_value",
                value: "Comment.ManageOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 15,
                column: "claim_value",
                value: "Message.SendAsSelf");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 16,
                column: "claim_value",
                value: "Conversation.ReadOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 17,
                column: "claim_value",
                value: "Notification.ReadOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 18,
                column: "claim_value",
                value: "Certificate.ReadOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 19,
                column: "claim_value",
                value: "Certificate.VerifyPublic");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 20,
                column: "claim_value",
                value: "Order.ReadOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 21,
                column: "claim_value",
                value: "Checkout.CreateOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 22,
                column: "claim_value",
                value: "Subscription.ManageOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 23,
                columns: new[] { "claim_value", "role_id" },
                values: new object[] { "Profile.ReadOwn", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546002") });

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 24,
                columns: new[] { "claim_value", "role_id" },
                values: new object[] { "Profile.UpdateOwn", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546002") });

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 25,
                column: "claim_value",
                value: "Security.ManageOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 26,
                column: "claim_value",
                value: "Sessions.ManageOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 27,
                column: "claim_value",
                value: "TeacherApplication.CreateOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 28,
                column: "claim_value",
                value: "Enrollment.CreateOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 29,
                column: "claim_value",
                value: "Enrollment.ReadOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 30,
                column: "claim_value",
                value: "Learning.AccessOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 31,
                column: "claim_value",
                value: "Progress.UpdateOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 32,
                column: "claim_value",
                value: "Quiz.AttemptOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 33,
                column: "claim_value",
                value: "Assignment.SubmitOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 34,
                column: "claim_value",
                value: "Review.ManageOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 35,
                column: "claim_value",
                value: "Discussion.Participate");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 36,
                column: "claim_value",
                value: "Comment.ManageOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 37,
                column: "claim_value",
                value: "Message.SendAsSelf");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 38,
                column: "claim_value",
                value: "Conversation.ReadOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 39,
                column: "claim_value",
                value: "Notification.ReadOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 40,
                column: "claim_value",
                value: "Certificate.ReadOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 41,
                column: "claim_value",
                value: "Certificate.VerifyPublic");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 42,
                column: "claim_value",
                value: "Order.ReadOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 43,
                column: "claim_value",
                value: "Checkout.CreateOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 44,
                column: "claim_value",
                value: "Subscription.ManageOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 45,
                column: "claim_value",
                value: "Course.Create");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 46,
                column: "claim_value",
                value: "Course.ReadOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 47,
                column: "claim_value",
                value: "Course.UpdateOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 48,
                column: "claim_value",
                value: "Course.DeleteOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 49,
                column: "claim_value",
                value: "Course.SubmitOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 50,
                column: "claim_value",
                value: "Media.UploadOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 51,
                column: "claim_value",
                value: "Media.ReadOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 52,
                column: "claim_value",
                value: "Learning.ViewCourseLearners");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 53,
                column: "claim_value",
                value: "Submission.GradeCourse");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 54,
                column: "claim_value",
                value: "Assessment.ManageCourse");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 55,
                column: "claim_value",
                value: "Announcement.ManageCourse");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 56,
                column: "claim_value",
                value: "Commerce.ReadEarningsOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 57,
                column: "claim_value",
                value: "Commerce.ManagePayoutAccountOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 58,
                columns: new[] { "claim_value", "role_id" },
                values: new object[] { "Profile.ReadOwn", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546003") });

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 59,
                columns: new[] { "claim_value", "role_id" },
                values: new object[] { "Profile.UpdateOwn", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546003") });

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 60,
                column: "claim_value",
                value: "Security.ManageOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 61,
                column: "claim_value",
                value: "Sessions.ManageOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 62,
                column: "claim_value",
                value: "TeacherApplication.CreateOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 63,
                column: "claim_value",
                value: "Enrollment.CreateOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 64,
                column: "claim_value",
                value: "Enrollment.ReadOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 65,
                column: "claim_value",
                value: "Learning.AccessOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 66,
                column: "claim_value",
                value: "Progress.UpdateOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 67,
                column: "claim_value",
                value: "Quiz.AttemptOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 68,
                column: "claim_value",
                value: "Assignment.SubmitOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 69,
                column: "claim_value",
                value: "Review.ManageOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 70,
                column: "claim_value",
                value: "Discussion.Participate");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 71,
                column: "claim_value",
                value: "Comment.ManageOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 72,
                column: "claim_value",
                value: "Message.SendAsSelf");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 73,
                column: "claim_value",
                value: "Conversation.ReadOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 74,
                column: "claim_value",
                value: "Notification.ReadOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 75,
                column: "claim_value",
                value: "Certificate.ReadOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 76,
                column: "claim_value",
                value: "Certificate.VerifyPublic");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 77,
                column: "claim_value",
                value: "Order.ReadOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 78,
                column: "claim_value",
                value: "Checkout.CreateOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 79,
                column: "claim_value",
                value: "Subscription.ManageOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 80,
                column: "claim_value",
                value: "Course.Create");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 81,
                column: "claim_value",
                value: "Course.ReadOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 82,
                column: "claim_value",
                value: "Course.UpdateOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 83,
                column: "claim_value",
                value: "Course.DeleteOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 84,
                column: "claim_value",
                value: "Course.SubmitOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 85,
                column: "claim_value",
                value: "Media.UploadOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 86,
                column: "claim_value",
                value: "Media.ReadOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 87,
                column: "claim_value",
                value: "Learning.ViewCourseLearners");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 88,
                column: "claim_value",
                value: "Submission.GradeCourse");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 89,
                column: "claim_value",
                value: "Assessment.ManageCourse");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 90,
                column: "claim_value",
                value: "Announcement.ManageCourse");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 91,
                column: "claim_value",
                value: "Commerce.ReadEarningsOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 92,
                column: "claim_value",
                value: "Commerce.ManagePayoutAccountOwn");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 93,
                column: "claim_value",
                value: "TeacherApplication.ReviewAny");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 94,
                column: "claim_value",
                value: "Course.ReviewAny");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 95,
                column: "claim_value",
                value: "Course.PublishAny");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 96,
                column: "claim_value",
                value: "Course.ManageAny");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 97,
                column: "claim_value",
                value: "Media.ManageAny");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 98,
                column: "claim_value",
                value: "Moderation.ReviewAny");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 99,
                column: "claim_value",
                value: "Certificate.RevokeAny");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 100,
                column: "claim_value",
                value: "Commerce.ManageOffers");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 101,
                column: "claim_value",
                value: "Commerce.ManageOrders");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 102,
                column: "claim_value",
                value: "Commerce.ManageRefunds");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 103,
                column: "claim_value",
                value: "User.ReadAny");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 104,
                column: "claim_value",
                value: "User.ManageAny");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 105,
                column: "claim_value",
                value: "Role.ManageAny");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 106,
                column: "claim_value",
                value: "Catalog.ManageTaxonomy");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 107,
                column: "claim_value",
                value: "Cms.Manage");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 108,
                column: "claim_value",
                value: "Settings.Manage");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 109,
                column: "claim_value",
                value: "FeatureFlag.Manage");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 110,
                column: "claim_value",
                value: "Analytics.Read");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "role_claims",
                keyColumn: "id",
                keyValue: 111,
                column: "claim_value",
                value: "Audit.Read");
        }
    }
}
