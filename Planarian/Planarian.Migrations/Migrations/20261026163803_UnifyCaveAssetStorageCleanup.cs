using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planarian.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class UnifyCaveAssetStorageCleanup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_AccountResetCleanupTargets",
                table: "AccountResetCleanupTargets");

            migrationBuilder.DropIndex(
                name: "IX_AccountResetCleanupTargets_AccountId",
                table: "AccountResetCleanupTargets");

            migrationBuilder.DropIndex(
                name: "IX_AccountResetCleanupTargets_AccountId_StoragePartition_Stora~",
                table: "AccountResetCleanupTargets");

            migrationBuilder.RenameTable(
                name: "AccountResetCleanupTargets",
                newName: "StorageCleanupTargets");

            migrationBuilder.AddColumn<string>(
                name: "Purpose",
                table: "StorageCleanupTargets",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "AccountReset");

            migrationBuilder.AddPrimaryKey(
                name: "PK_StorageCleanupTargets",
                table: "StorageCleanupTargets",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_StorageCleanupTargets_AccountId_Purpose",
                table: "StorageCleanupTargets",
                columns: new[] { "AccountId", "Purpose" });

            migrationBuilder.CreateIndex(
                name: "IX_StorageCleanupTargets_AccountId_StoragePartition_StorageKey~",
                table: "StorageCleanupTargets",
                columns: new[] { "AccountId", "StoragePartition", "StorageKey", "IsContainer", "Purpose" },
                unique: true);

            // Existing completion-branch retained File objects become ordinary post-commit
            // asset-removal cleanup debt. Preserve their storage addresses so upgrading the
            // pre-release branch does not strand bytes merely because the retention policy changed.
            migrationBuilder.Sql("""
                DO $$
                DECLARE
                    retained record;
                    candidate_id text;
                    salt integer;
                BEGIN
                    FOR retained IN
                        SELECT * FROM "RetainedCaveFileObjects"
                        ORDER BY "AccountId" COLLATE "C", "FileId" COLLATE "C"
                    LOOP
                        IF EXISTS (
                            SELECT 1 FROM "StorageCleanupTargets" target
                            WHERE target."AccountId" = retained."AccountId"
                              AND target."StoragePartition" = retained."StoragePartition"
                              AND target."StorageKey" = retained."StorageKey"
                              AND target."IsContainer" = false
                              AND target."Purpose" = 'CaveAssetRemoval'
                        ) THEN
                            CONTINUE;
                        END IF;

                        salt := 0;
                        LOOP
                            candidate_id := substring(md5(
                                retained."Id" || ':' || retained."AccountId" || ':' ||
                                retained."StoragePartition" || ':' || retained."StorageKey" || ':' || salt::text
                            ) from 1 for 10);
                            EXIT WHEN NOT EXISTS (
                                SELECT 1 FROM "StorageCleanupTargets" target
                                WHERE target."Id" = candidate_id
                            );
                            salt := salt + 1;
                        END LOOP;

                        INSERT INTO "StorageCleanupTargets"
                            ("Id", "AccountId", "StoragePartition", "StorageKey", "IsContainer", "Purpose",
                             "CreatedByUserId", "ModifiedByUserId", "CreatedOn", "ModifiedOn")
                        VALUES
                            (candidate_id, retained."AccountId", retained."StoragePartition", retained."StorageKey",
                             false, 'CaveAssetRemoval', retained."CreatedByUserId", retained."ModifiedByUserId",
                             retained."CreatedOn", retained."ModifiedOn");
                    END LOOP;
                END $$;
                """);

            migrationBuilder.DropTable(
                name: "RetainedCaveFileObjects");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Ordinary File-cleanup targets are the only durable addresses for bytes awaiting
            // deletion. Refuse to erase or reinterpret that cleanup debt during a downgrade.
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (
                        SELECT 1 FROM "StorageCleanupTargets"
                        WHERE "Purpose" IN ('CaveAssetRemoval', 'UnpublishedFileRemoval')
                    ) THEN
                        RAISE EXCEPTION 'Cannot downgrade with pending File storage cleanup';
                    END IF;
                END $$;
                """);

            migrationBuilder.DropPrimaryKey(
                name: "PK_StorageCleanupTargets",
                table: "StorageCleanupTargets");

            migrationBuilder.DropIndex(
                name: "IX_StorageCleanupTargets_AccountId_Purpose",
                table: "StorageCleanupTargets");

            migrationBuilder.DropIndex(
                name: "IX_StorageCleanupTargets_AccountId_StoragePartition_StorageKey~",
                table: "StorageCleanupTargets");

            migrationBuilder.DropColumn(
                name: "Purpose",
                table: "StorageCleanupTargets");

            migrationBuilder.RenameTable(
                name: "StorageCleanupTargets",
                newName: "AccountResetCleanupTargets");

            migrationBuilder.AddPrimaryKey(
                name: "PK_AccountResetCleanupTargets",
                table: "AccountResetCleanupTargets",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_AccountResetCleanupTargets_AccountId",
                table: "AccountResetCleanupTargets",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountResetCleanupTargets_AccountId_StoragePartition_Stora~",
                table: "AccountResetCleanupTargets",
                columns: new[] { "AccountId", "StoragePartition", "StorageKey", "IsContainer" },
                unique: true);

            migrationBuilder.CreateTable(
                name: "RetainedCaveFileObjects",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    AccountId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CaveId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CreatedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FileId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ModifiedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ModifiedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    StorageKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    StoragePartition = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RetainedCaveFileObjects", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RetainedCaveFileObjects_AccountId_CaveId",
                table: "RetainedCaveFileObjects",
                columns: new[] { "AccountId", "CaveId" });

            migrationBuilder.CreateIndex(
                name: "IX_RetainedCaveFileObjects_AccountId_FileId",
                table: "RetainedCaveFileObjects",
                columns: new[] { "AccountId", "FileId" },
                unique: true);
        }
    }
}
