using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dorosak.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase7MediaExitGate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE operations.schema_compatibility
                SET maximum_compatible_migration_id = '20260808044726_Phase7MediaExitGate',
                    updated_at = CURRENT_TIMESTAMP
                WHERE singleton
                """);

            migrationBuilder.DropCheckConstraint(
                name: "ck_caption_tracks_bytes",
                schema: "media",
                table: "caption_tracks");

            migrationBuilder.AddColumn<string>(
                name: "sha256",
                schema: "media",
                table: "media_variants",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "0000000000000000000000000000000000000000000000000000000000000000");

            migrationBuilder.AlterColumn<string>(
                name: "e_tag",
                schema: "media",
                table: "caption_tracks",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(512)",
                oldMaxLength: 512);

            migrationBuilder.AlterColumn<long>(
                name: "bytes",
                schema: "media",
                table: "caption_tracks",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ready_at",
                schema: "media",
                table: "caption_tracks",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "rejected_at",
                schema: "media",
                table: "caption_tracks",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "rejection_code",
                schema: "media",
                table: "caption_tracks",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "sha256",
                schema: "media",
                table: "caption_tracks",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "source_media_asset_id",
                schema: "media",
                table: "caption_tracks",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "state",
                schema: "media",
                table: "caption_tracks",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Pending");

            migrationBuilder.AddColumn<string>(
                name: "storage_container",
                schema: "media",
                table: "caption_tracks",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "storage_provider",
                schema: "media",
                table: "caption_tracks",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "version_id",
                schema: "media",
                table: "caption_tracks",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_media_variants_sha256",
                schema: "media",
                table: "media_variants",
                sql: "sha256 ~ '^[0-9a-f]{64}$'");

            migrationBuilder.CreateIndex(
                name: "uq_caption_tracks_source_media_asset_id",
                schema: "media",
                table: "caption_tracks",
                column: "source_media_asset_id",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_caption_tracks_bytes",
                schema: "media",
                table: "caption_tracks",
                sql: "bytes IS NULL OR bytes > 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_caption_tracks_state",
                schema: "media",
                table: "caption_tracks",
                sql: "state IN ('Pending', 'Ready', 'Rejected')");

            migrationBuilder.AddForeignKey(
                name: "fk_caption_tracks_media_assets_source_media_asset_id",
                schema: "media",
                table: "caption_tracks",
                column: "source_media_asset_id",
                principalSchema: "media",
                principalTable: "media_assets",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE operations.schema_compatibility
                SET maximum_compatible_migration_id = '20260808014905_Phase7MediaStorageMetadata',
                    updated_at = CURRENT_TIMESTAMP
                WHERE singleton
                """);

            migrationBuilder.DropForeignKey(
                name: "fk_caption_tracks_media_assets_source_media_asset_id",
                schema: "media",
                table: "caption_tracks");

            migrationBuilder.DropCheckConstraint(
                name: "ck_media_variants_sha256",
                schema: "media",
                table: "media_variants");

            migrationBuilder.DropIndex(
                name: "uq_caption_tracks_source_media_asset_id",
                schema: "media",
                table: "caption_tracks");

            migrationBuilder.DropCheckConstraint(
                name: "ck_caption_tracks_bytes",
                schema: "media",
                table: "caption_tracks");

            migrationBuilder.DropCheckConstraint(
                name: "ck_caption_tracks_state",
                schema: "media",
                table: "caption_tracks");

            migrationBuilder.DropColumn(
                name: "sha256",
                schema: "media",
                table: "media_variants");

            migrationBuilder.DropColumn(
                name: "ready_at",
                schema: "media",
                table: "caption_tracks");

            migrationBuilder.DropColumn(
                name: "rejected_at",
                schema: "media",
                table: "caption_tracks");

            migrationBuilder.DropColumn(
                name: "rejection_code",
                schema: "media",
                table: "caption_tracks");

            migrationBuilder.DropColumn(
                name: "sha256",
                schema: "media",
                table: "caption_tracks");

            migrationBuilder.DropColumn(
                name: "source_media_asset_id",
                schema: "media",
                table: "caption_tracks");

            migrationBuilder.DropColumn(
                name: "state",
                schema: "media",
                table: "caption_tracks");

            migrationBuilder.DropColumn(
                name: "storage_container",
                schema: "media",
                table: "caption_tracks");

            migrationBuilder.DropColumn(
                name: "storage_provider",
                schema: "media",
                table: "caption_tracks");

            migrationBuilder.DropColumn(
                name: "version_id",
                schema: "media",
                table: "caption_tracks");

            migrationBuilder.Sql("DELETE FROM media.caption_tracks WHERE bytes IS NULL OR e_tag IS NULL");

            migrationBuilder.AlterColumn<string>(
                name: "e_tag",
                schema: "media",
                table: "caption_tracks",
                type: "character varying(512)",
                maxLength: 512,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(512)",
                oldMaxLength: 512,
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "bytes",
                schema: "media",
                table: "caption_tracks",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_caption_tracks_bytes",
                schema: "media",
                table: "caption_tracks",
                sql: "bytes > 0");
        }
    }
}
