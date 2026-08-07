using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Dorosak.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase5IdentitySecurity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "identity");

            migrationBuilder.EnsureSchema(
                name: "profiles");

            migrationBuilder.CreateTable(
                name: "data_protection_keys",
                schema: "operations",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    friendly_name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    xml = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_data_protection_keys", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "roles",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    normalized_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    concurrency_stamp = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_identity_roles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    display_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    security_version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    authorization_version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    protected_mfa_secret = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    protected_pending_mfa_secret = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    last_mfa_time_step = table.Column<long>(type: "bigint", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    user_name = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    normalized_user_name = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    normalized_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    email_confirmed = table.Column<bool>(type: "boolean", nullable: false),
                    password_hash = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    security_stamp = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    concurrency_stamp = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    phone_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    phone_number_confirmed = table.Column<bool>(type: "boolean", nullable: false),
                    two_factor_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    lockout_end = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    lockout_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    access_failed_count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_identity_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "role_claims",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    claim_type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    claim_value = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_identity_role_claims", x => x.id);
                    table.ForeignKey(
                        name: "fk_role_claims_roles_role_id",
                        column: x => x.role_id,
                        principalSchema: "identity",
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "mfa_challenges",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    consumed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mfa_challenges", x => x.id);
                    table.CheckConstraint("ck_mfa_challenges_attempt_count", "attempt_count >= 0");
                    table.CheckConstraint("ck_mfa_challenges_expiration", "expires_at > created_at");
                    table.ForeignKey(
                        name: "fk_mfa_challenges_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "mfa_recovery_codes",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    used_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mfa_recovery_codes", x => x.id);
                    table.ForeignKey(
                        name: "fk_mfa_recovery_codes_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "profiles",
                schema: "profiles",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    display_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_profiles", x => x.user_id);
                    table.ForeignKey(
                        name: "fk_profiles_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "refresh_sessions",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    family_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_used_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    authenticated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    idle_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    absolute_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revocation_reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    device_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ip_address_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    authentication_methods = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    authorization_version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_refresh_sessions", x => x.id);
                    table.CheckConstraint("ck_refresh_sessions_authorization_version", "authorization_version > 0");
                    table.CheckConstraint("ck_refresh_sessions_expiration", "idle_expires_at <= absolute_expires_at");
                    table.ForeignKey(
                        name: "fk_refresh_sessions_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "security_events",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    session_id = table.Column<Guid>(type: "uuid", nullable: true),
                    type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ip_address_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    metadata = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_security_events", x => x.id);
                    table.ForeignKey(
                        name: "fk_security_events_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "user_claims",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    claim_type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    claim_value = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_identity_user_claims", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_claims_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_logins",
                schema: "identity",
                columns: table => new
                {
                    login_provider = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    provider_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    provider_display_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_identity_user_logins", x => new { x.login_provider, x.provider_key });
                    table.ForeignKey(
                        name: "fk_user_logins_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_roles",
                schema: "identity",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_identity_user_roles", x => new { x.user_id, x.role_id });
                    table.ForeignKey(
                        name: "fk_user_roles_roles_role_id",
                        column: x => x.role_id,
                        principalSchema: "identity",
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_user_roles_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_tokens",
                schema: "identity",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    login_provider = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    value = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_identity_user_tokens", x => new { x.user_id, x.login_provider, x.name });
                    table.ForeignKey(
                        name: "fk_user_tokens_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    family_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    consumed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    replaced_by_token_id = table.Column<Guid>(type: "uuid", nullable: true),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revocation_reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_refresh_tokens", x => x.id);
                    table.CheckConstraint("ck_refresh_tokens_expiration", "expires_at > created_at");
                    table.ForeignKey(
                        name: "fk_refresh_tokens_sessions_session_id",
                        column: x => x.session_id,
                        principalSchema: "identity",
                        principalTable: "refresh_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                schema: "identity",
                table: "roles",
                columns: new[] { "id", "concurrency_stamp", "created_at", "name", "normalized_name" },
                values: new object[,]
                {
                    { new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546001"), "018f3f0e43807b1b8f8db8ea9c546001", new DateTimeOffset(new DateTime(2026, 8, 6, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Student", "STUDENT" },
                    { new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546002"), "018f3f0e43807b1b8f8db8ea9c546002", new DateTimeOffset(new DateTime(2026, 8, 6, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Teacher", "TEACHER" },
                    { new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546003"), "018f3f0e43807b1b8f8db8ea9c546003", new DateTimeOffset(new DateTime(2026, 8, 6, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Admin", "ADMIN" }
                });

            migrationBuilder.InsertData(
                schema: "identity",
                table: "role_claims",
                columns: new[] { "id", "claim_type", "claim_value", "role_id" },
                values: new object[,]
                {
                    { 1, "permission", "Profile.ReadOwn", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546001") },
                    { 2, "permission", "Profile.UpdateOwn", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546001") },
                    { 3, "permission", "Security.ManageOwn", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546001") },
                    { 4, "permission", "Sessions.ManageOwn", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546001") },
                    { 5, "permission", "TeacherApplication.CreateOwn", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546001") },
                    { 6, "permission", "Enrollment.CreateOwn", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546001") },
                    { 7, "permission", "Enrollment.ReadOwn", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546001") },
                    { 8, "permission", "Learning.AccessOwn", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546001") },
                    { 9, "permission", "Progress.UpdateOwn", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546001") },
                    { 10, "permission", "Quiz.AttemptOwn", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546001") },
                    { 11, "permission", "Assignment.SubmitOwn", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546001") },
                    { 12, "permission", "Review.ManageOwn", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546001") },
                    { 13, "permission", "Discussion.Participate", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546001") },
                    { 14, "permission", "Comment.ManageOwn", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546001") },
                    { 15, "permission", "Message.SendAsSelf", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546001") },
                    { 16, "permission", "Conversation.ReadOwn", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546001") },
                    { 17, "permission", "Notification.ReadOwn", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546001") },
                    { 18, "permission", "Certificate.ReadOwn", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546001") },
                    { 19, "permission", "Certificate.VerifyPublic", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546001") },
                    { 20, "permission", "Order.ReadOwn", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546001") },
                    { 21, "permission", "Checkout.CreateOwn", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546001") },
                    { 22, "permission", "Subscription.ManageOwn", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546001") },
                    { 23, "permission", "Profile.ReadOwn", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546002") },
                    { 24, "permission", "Profile.UpdateOwn", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546002") },
                    { 25, "permission", "Security.ManageOwn", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546002") },
                    { 26, "permission", "Sessions.ManageOwn", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546002") },
                    { 27, "permission", "TeacherApplication.CreateOwn", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546002") },
                    { 28, "permission", "Enrollment.CreateOwn", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546002") },
                    { 29, "permission", "Enrollment.ReadOwn", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546002") },
                    { 30, "permission", "Learning.AccessOwn", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546002") },
                    { 31, "permission", "Progress.UpdateOwn", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546002") },
                    { 32, "permission", "Quiz.AttemptOwn", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546002") },
                    { 33, "permission", "Assignment.SubmitOwn", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546002") },
                    { 34, "permission", "Review.ManageOwn", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546002") },
                    { 35, "permission", "Discussion.Participate", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546002") },
                    { 36, "permission", "Comment.ManageOwn", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546002") },
                    { 37, "permission", "Message.SendAsSelf", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546002") },
                    { 38, "permission", "Conversation.ReadOwn", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546002") },
                    { 39, "permission", "Notification.ReadOwn", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546002") },
                    { 40, "permission", "Certificate.ReadOwn", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546002") },
                    { 41, "permission", "Certificate.VerifyPublic", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546002") },
                    { 42, "permission", "Order.ReadOwn", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546002") },
                    { 43, "permission", "Checkout.CreateOwn", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546002") },
                    { 44, "permission", "Subscription.ManageOwn", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546002") },
                    { 45, "permission", "Course.Create", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546002") },
                    { 46, "permission", "Course.ReadOwn", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546002") },
                    { 47, "permission", "Course.UpdateOwn", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546002") },
                    { 48, "permission", "Course.DeleteOwn", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546002") },
                    { 49, "permission", "Course.SubmitOwn", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546002") },
                    { 50, "permission", "Media.UploadOwn", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546002") },
                    { 51, "permission", "Media.ReadOwn", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546002") },
                    { 52, "permission", "Learning.ViewCourseLearners", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546002") },
                    { 53, "permission", "Submission.GradeCourse", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546002") },
                    { 54, "permission", "Assessment.ManageCourse", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546002") },
                    { 55, "permission", "Announcement.ManageCourse", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546002") },
                    { 56, "permission", "Commerce.ReadEarningsOwn", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546002") },
                    { 57, "permission", "Commerce.ManagePayoutAccountOwn", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546002") },
                    { 58, "permission", "Profile.ReadOwn", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546003") },
                    { 59, "permission", "Profile.UpdateOwn", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546003") },
                    { 60, "permission", "Security.ManageOwn", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546003") },
                    { 61, "permission", "Sessions.ManageOwn", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546003") },
                    { 62, "permission", "TeacherApplication.CreateOwn", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546003") },
                    { 63, "permission", "Enrollment.CreateOwn", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546003") },
                    { 64, "permission", "Enrollment.ReadOwn", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546003") },
                    { 65, "permission", "Learning.AccessOwn", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546003") },
                    { 66, "permission", "Progress.UpdateOwn", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546003") },
                    { 67, "permission", "Quiz.AttemptOwn", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546003") },
                    { 68, "permission", "Assignment.SubmitOwn", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546003") },
                    { 69, "permission", "Review.ManageOwn", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546003") },
                    { 70, "permission", "Discussion.Participate", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546003") },
                    { 71, "permission", "Comment.ManageOwn", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546003") },
                    { 72, "permission", "Message.SendAsSelf", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546003") },
                    { 73, "permission", "Conversation.ReadOwn", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546003") },
                    { 74, "permission", "Notification.ReadOwn", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546003") },
                    { 75, "permission", "Certificate.ReadOwn", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546003") },
                    { 76, "permission", "Certificate.VerifyPublic", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546003") },
                    { 77, "permission", "Order.ReadOwn", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546003") },
                    { 78, "permission", "Checkout.CreateOwn", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546003") },
                    { 79, "permission", "Subscription.ManageOwn", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546003") },
                    { 80, "permission", "Course.Create", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546003") },
                    { 81, "permission", "Course.ReadOwn", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546003") },
                    { 82, "permission", "Course.UpdateOwn", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546003") },
                    { 83, "permission", "Course.DeleteOwn", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546003") },
                    { 84, "permission", "Course.SubmitOwn", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546003") },
                    { 85, "permission", "Media.UploadOwn", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546003") },
                    { 86, "permission", "Media.ReadOwn", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546003") },
                    { 87, "permission", "Learning.ViewCourseLearners", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546003") },
                    { 88, "permission", "Submission.GradeCourse", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546003") },
                    { 89, "permission", "Assessment.ManageCourse", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546003") },
                    { 90, "permission", "Announcement.ManageCourse", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546003") },
                    { 91, "permission", "Commerce.ReadEarningsOwn", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546003") },
                    { 92, "permission", "Commerce.ManagePayoutAccountOwn", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546003") },
                    { 93, "permission", "TeacherApplication.ReviewAny", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546003") },
                    { 94, "permission", "Course.ReviewAny", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546003") },
                    { 95, "permission", "Course.PublishAny", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546003") },
                    { 96, "permission", "Course.ManageAny", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546003") },
                    { 97, "permission", "Media.ManageAny", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546003") },
                    { 98, "permission", "Moderation.ReviewAny", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546003") },
                    { 99, "permission", "Certificate.RevokeAny", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546003") },
                    { 100, "permission", "Commerce.ManageOffers", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546003") },
                    { 101, "permission", "Commerce.ManageOrders", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546003") },
                    { 102, "permission", "Commerce.ManageRefunds", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546003") },
                    { 103, "permission", "User.ReadAny", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546003") },
                    { 104, "permission", "User.ManageAny", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546003") },
                    { 105, "permission", "Role.ManageAny", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546003") },
                    { 106, "permission", "Catalog.ManageTaxonomy", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546003") },
                    { 107, "permission", "Cms.Manage", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546003") },
                    { 108, "permission", "Settings.Manage", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546003") },
                    { 109, "permission", "FeatureFlag.Manage", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546003") },
                    { 110, "permission", "Analytics.Read", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546003") },
                    { 111, "permission", "Audit.Read", new Guid("018f3f0e-4380-7b1b-8f8d-b8ea9c546003") }
                });

            migrationBuilder.CreateIndex(
                name: "ix_mfa_challenges_user_id",
                schema: "identity",
                table: "mfa_challenges",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "uq_mfa_challenges_token_hash",
                schema: "identity",
                table: "mfa_challenges",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_mfa_recovery_codes_user_hash",
                schema: "identity",
                table: "mfa_recovery_codes",
                columns: new[] { "user_id", "code_hash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_refresh_sessions_active_user",
                schema: "identity",
                table: "refresh_sessions",
                columns: new[] { "user_id", "revoked_at", "absolute_expires_at" });

            migrationBuilder.CreateIndex(
                name: "ix_refresh_sessions_family_id",
                schema: "identity",
                table: "refresh_sessions",
                column: "family_id");

            migrationBuilder.CreateIndex(
                name: "ix_refresh_sessions_user_id",
                schema: "identity",
                table: "refresh_sessions",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_family_id",
                schema: "identity",
                table: "refresh_tokens",
                column: "family_id");

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_session_id",
                schema: "identity",
                table: "refresh_tokens",
                column: "session_id");

            migrationBuilder.CreateIndex(
                name: "uq_refresh_tokens_token_hash",
                schema: "identity",
                table: "refresh_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_identity_role_claims_role_id",
                schema: "identity",
                table: "role_claims",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "uq_identity_role_claims_role_type_value",
                schema: "identity",
                table: "role_claims",
                columns: new[] { "role_id", "claim_type", "claim_value" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_identity_roles_normalized_name",
                schema: "identity",
                table: "roles",
                column: "normalized_name",
                unique: true,
                filter: "normalized_name IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_security_events_session_id",
                schema: "identity",
                table: "security_events",
                column: "session_id");

            migrationBuilder.CreateIndex(
                name: "ix_security_events_user_occurred_at",
                schema: "identity",
                table: "security_events",
                columns: new[] { "user_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_identity_user_claims_user_id",
                schema: "identity",
                table: "user_claims",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_identity_user_logins_user_id",
                schema: "identity",
                table: "user_logins",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_identity_user_roles_role_id",
                schema: "identity",
                table: "user_roles",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "uq_identity_users_normalized_email",
                schema: "identity",
                table: "users",
                column: "normalized_email",
                unique: true,
                filter: "normalized_email IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "uq_identity_users_normalized_user_name",
                schema: "identity",
                table: "users",
                column: "normalized_user_name",
                unique: true,
                filter: "normalized_user_name IS NOT NULL");

            migrationBuilder.Sql(
                """
                UPDATE operations.schema_compatibility
                SET maximum_compatible_migration_id = '20260806223946_Phase5IdentitySecurity',
                    updated_at = CURRENT_TIMESTAMP
                WHERE singleton
                """);

            migrationBuilder.Sql(
                """
                DO $permissions$
                BEGIN
                    IF EXISTS (SELECT FROM pg_roles WHERE rolname = 'dorosak_runtime') THEN
                        REVOKE UPDATE, DELETE, TRUNCATE
                            ON identity.security_events
                            FROM dorosak_runtime;
                        GRANT SELECT, INSERT
                            ON identity.security_events
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
                SET maximum_compatible_migration_id = '20260806071128_ExpandSchemaCompatibilityRange',
                    updated_at = CURRENT_TIMESTAMP
                WHERE singleton
                """);

            migrationBuilder.DropTable(
                name: "data_protection_keys",
                schema: "operations");

            migrationBuilder.DropTable(
                name: "mfa_challenges",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "mfa_recovery_codes",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "profiles",
                schema: "profiles");

            migrationBuilder.DropTable(
                name: "refresh_tokens",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "role_claims",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "security_events",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "user_claims",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "user_logins",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "user_roles",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "user_tokens",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "refresh_sessions",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "roles",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "users",
                schema: "identity");
        }
    }
}
