using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dorosak.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase10DemoSubscriptionsCertificates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "credentials");

            migrationBuilder.CreateTable(
                name: "certificates",
                schema: "credentials",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    completion_enrollment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    learner_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    course_id = table.Column<Guid>(type: "uuid", nullable: false),
                    release_id = table.Column<Guid>(type: "uuid", nullable: false),
                    learner_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    course_title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    locale = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    verification_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    issued_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revoked_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    revocation_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_certificates", x => x.id);
                    table.CheckConstraint("ck_certificates_locale", "locale IN ('ar', 'en')");
                    table.CheckConstraint("ck_certificates_revocation", "(status = 'Active' AND revoked_at IS NULL AND revoked_by_user_id IS NULL AND revocation_reason IS NULL) OR (status = 'Revoked' AND revoked_at IS NOT NULL AND revoked_by_user_id IS NOT NULL AND revocation_reason IS NOT NULL)");
                    table.CheckConstraint("ck_certificates_status", "status IN ('Active', 'Revoked')");
                    table.ForeignKey(
                        name: "fk_certificates_course_completions_completion_enrollment_id",
                        column: x => x.completion_enrollment_id,
                        principalSchema: "learning",
                        principalTable: "course_completions",
                        principalColumn: "enrollment_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_certificates_users_learner_user_id",
                        column: x => x.learner_user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_certificates_users_revoked_by_user_id",
                        column: x => x.revoked_by_user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "demo_subscriptions",
                schema: "commerce",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    activated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    cancelled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_demo_subscriptions", x => x.id);
                    table.CheckConstraint("ck_demo_subscriptions_cancelled_at", "(status = 'Active' AND cancelled_at IS NULL) OR (status = 'Cancelled' AND cancelled_at IS NOT NULL)");
                    table.CheckConstraint("ck_demo_subscriptions_plan", "plan_code = 'portfolio-demo'");
                    table.CheckConstraint("ck_demo_subscriptions_status", "status IN ('Active', 'Cancelled')");
                    table.ForeignKey(
                        name: "fk_demo_subscriptions_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_certificates_learner_issued_id",
                schema: "credentials",
                table: "certificates",
                columns: new[] { "learner_user_id", "issued_at", "id" },
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "ix_certificates_revoked_by_user_id",
                schema: "credentials",
                table: "certificates",
                column: "revoked_by_user_id");

            migrationBuilder.CreateIndex(
                name: "uq_certificates_completion_enrollment_id",
                schema: "credentials",
                table: "certificates",
                column: "completion_enrollment_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_certificates_verification_code",
                schema: "credentials",
                table: "certificates",
                column: "verification_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_demo_subscriptions_user_id",
                schema: "commerce",
                table: "demo_subscriptions",
                column: "user_id",
                unique: true);

            migrationBuilder.Sql(
                """
                DO $permissions$
                BEGIN
                    IF EXISTS (SELECT FROM pg_roles WHERE rolname = 'dorosak_runtime') THEN
                        GRANT USAGE ON SCHEMA commerce, credentials TO dorosak_runtime;
                        GRANT SELECT, INSERT ON commerce.demo_subscriptions TO dorosak_runtime;
                        REVOKE DELETE, TRUNCATE ON commerce.demo_subscriptions FROM dorosak_runtime;
                        GRANT UPDATE (status, activated_at, updated_at, cancelled_at)
                            ON commerce.demo_subscriptions TO dorosak_runtime;

                        GRANT SELECT, INSERT ON credentials.certificates TO dorosak_runtime;
                        REVOKE DELETE, TRUNCATE ON credentials.certificates FROM dorosak_runtime;
                        GRANT UPDATE (status, revoked_at, revoked_by_user_id, revocation_reason)
                            ON credentials.certificates TO dorosak_runtime;
                    END IF;
                END
                $permissions$;

                CREATE OR REPLACE FUNCTION credentials.guard_certificate_immutability()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $guard$
                BEGIN
                    IF NEW.id IS DISTINCT FROM OLD.id
                       OR NEW.completion_enrollment_id IS DISTINCT FROM OLD.completion_enrollment_id
                       OR NEW.learner_user_id IS DISTINCT FROM OLD.learner_user_id
                       OR NEW.course_id IS DISTINCT FROM OLD.course_id
                       OR NEW.release_id IS DISTINCT FROM OLD.release_id
                       OR NEW.learner_name IS DISTINCT FROM OLD.learner_name
                       OR NEW.course_title IS DISTINCT FROM OLD.course_title
                       OR NEW.locale IS DISTINCT FROM OLD.locale
                       OR NEW.completed_at IS DISTINCT FROM OLD.completed_at
                       OR NEW.verification_code IS DISTINCT FROM OLD.verification_code
                       OR NEW.issued_at IS DISTINCT FROM OLD.issued_at THEN
                        RAISE EXCEPTION 'Certificate issue data is immutable.' USING ERRCODE = '23514';
                    END IF;
                    IF OLD.status = 'Revoked' AND NEW IS DISTINCT FROM OLD THEN
                        RAISE EXCEPTION 'A revoked certificate is immutable.' USING ERRCODE = '23514';
                    END IF;
                    IF OLD.status = 'Active' AND NEW.status <> 'Revoked' THEN
                        RAISE EXCEPTION 'Invalid certificate transition.' USING ERRCODE = '23514';
                    END IF;
                    RETURN NEW;
                END
                $guard$;

                CREATE TRIGGER trg_certificates_immutable
                BEFORE UPDATE ON credentials.certificates
                FOR EACH ROW EXECUTE FUNCTION credentials.guard_certificate_immutability();

                REVOKE ALL ON FUNCTION credentials.guard_certificate_immutability() FROM PUBLIC;

                UPDATE operations.schema_compatibility
                SET maximum_compatible_migration_id = '20260812141430_Phase10DemoSubscriptionsCertificates',
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
                SET maximum_compatible_migration_id = '20260811180529_Phase9MessageReporting',
                    updated_at = CURRENT_TIMESTAMP
                WHERE singleton;

                DROP TRIGGER IF EXISTS trg_certificates_immutable ON credentials.certificates;
                DROP FUNCTION IF EXISTS credentials.guard_certificate_immutability();

                DO $permissions$
                BEGIN
                    IF EXISTS (SELECT FROM pg_roles WHERE rolname = 'dorosak_runtime') THEN
                        REVOKE ALL PRIVILEGES ON commerce.demo_subscriptions FROM dorosak_runtime;
                        REVOKE ALL PRIVILEGES ON credentials.certificates FROM dorosak_runtime;
                    END IF;
                END
                $permissions$;
                """);

            migrationBuilder.DropTable(
                name: "certificates",
                schema: "credentials");

            migrationBuilder.DropTable(
                name: "demo_subscriptions",
                schema: "commerce");
        }
    }
}
