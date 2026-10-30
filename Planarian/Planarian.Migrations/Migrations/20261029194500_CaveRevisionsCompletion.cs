using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planarian.Migrations.Migrations
{
    /// <summary>
    /// Adds Cave revision and proposal persistence, File metadata and cleanup,
    /// authoring defaults, and tenant-safe permission contracts.
    /// </summary>
    public partial class CaveRevisionsCompletion : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Cave revision and File schema

            migrationBuilder.DropForeignKey(
                name: "FK_Caves_Users_ReportedByUserId",
                table: "Caves");

            migrationBuilder.DropForeignKey(
                name: "FK_Entrances_Users_ReportedByUserId",
                table: "Entrances");

            migrationBuilder.DropForeignKey(
                name: "FK_Files_Accounts_AccountId",
                table: "Files");

            migrationBuilder.DropIndex(
                name: "IX_Files_AccountId",
                table: "Files");

            migrationBuilder.DropIndex(
                name: "IX_Entrances_ReportedByUserId",
                table: "Entrances");

            migrationBuilder.DropIndex(
                name: "IX_Caves_AccountId",
                table: "Caves");

            migrationBuilder.DropIndex(
                name: "IX_Caves_ReportedByUserId",
                table: "Caves");

            // Historic Cave files predate File.AccountId being a domain invariant.
            // Recover ownership only when the owning Cave makes it unambiguous, and
            // fail closed rather than manufacturing a tenant for any remaining row.
            migrationBuilder.Sql("""
                UPDATE "Files" AS file
                SET "AccountId" = cave."AccountId"
                FROM "Caves" AS cave
                WHERE file."AccountId" IS NULL
                  AND file."CaveId" = cave."Id";

                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM "Files" AS file
                        JOIN "Caves" AS cave ON cave."Id" = file."CaveId"
                        WHERE file."AccountId" IS DISTINCT FROM cave."AccountId"
                    ) THEN
                        RAISE EXCEPTION 'Cannot migrate Files.AccountId: a Cave-owned file account does not match its owning Cave account.';
                    END IF;

                    IF EXISTS (SELECT 1 FROM "Files" WHERE "AccountId" IS NULL) THEN
                        RAISE EXCEPTION 'Cannot migrate Files.AccountId: legacy file rows without a determinable account owner exist.';
                    END IF;
                END $$;
                """);

            // Main v32 already widened DisplayName to 1000 characters. Preserve that
            // user-facing name as Name, derive Extension from the stored filename,
            // and only then remove the legacy FileName column.
            migrationBuilder.RenameColumn(
                name: "DisplayName",
                table: "Files",
                newName: "Name");

            migrationBuilder.AddColumn<string>(
                name: "Extension",
                table: "Files",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "Files"
                SET "Extension" = CASE
                        WHEN position('.' in reverse("FileName")) > 1
                             AND length("FileName") - position('.' in reverse("FileName")) + 1 > 1
                        THEN substring("FileName" from length("FileName") - position('.' in reverse("FileName")) + 1)
                        ELSE ''
                    END,
                    "Name" = CASE
                        WHEN NULLIF(btrim("Name"), '') IS NOT NULL THEN "Name"
                        WHEN position('.' in reverse("FileName")) > 1
                             AND length("FileName") - position('.' in reverse("FileName")) + 1 > 1
                        THEN substring("FileName" from 1 for length("FileName") - position('.' in reverse("FileName")))
                        ELSE "FileName"
                    END;
                """);

            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM "Files"
                        WHERE length("Name") + length("Extension") > 1000
                    ) THEN
                        RAISE EXCEPTION 'Cannot migrate Files: Name + Extension would exceed 1000 characters.';
                    END IF;
                    IF EXISTS (
                        SELECT 1 FROM "Files"
                        WHERE btrim("Name", ' ' || chr(9) || chr(10) || chr(11) || chr(12) || chr(13) ||
                            chr(133) || chr(160) || chr(5760) || chr(8192) || chr(8193) || chr(8194) ||
                            chr(8195) || chr(8196) || chr(8197) || chr(8198) || chr(8199) || chr(8200) ||
                            chr(8201) || chr(8202) || chr(8232) || chr(8233) || chr(8239) || chr(8287) ||
                            chr(12288)) = ''
                           OR "Name" IN ('.', '..')
                           OR "Name" LIKE '%/%'
                           OR "Name" LIKE '%\\%'
                           OR "Name" ~ '[[:cntrl:]]'
                    ) THEN
                        RAISE EXCEPTION 'Cannot migrate Files: legacy DisplayName contains a file name that is invalid under the new Name policy.';
                    END IF;
                    IF EXISTS (
                        SELECT 1 FROM "Files"
                        WHERE "Extension" <> '' AND (
                            length("Extension") = 1
                            OR left("Extension", 1) <> '.'
                            OR "Extension" LIKE '%/%'
                            OR "Extension" LIKE '%\\%'
                            OR "Extension" ~ '[[:cntrl:]]'
                        )
                    ) THEN
                        RAISE EXCEPTION 'Cannot migrate Files: legacy FileName contains an invalid file extension under the new Extension policy.';
                    END IF;
                END $$;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Files",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Extension",
                table: "Files",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            // Preserve only legacy filename values that cannot be reconstructed from
            // the new Name + Extension representation. This migration-owned table is
            // intentionally outside the application model and exists only so an
            // untouched pre-upgrade row can be downgraded without losing its v32
            // storage filename. The migrated Name/Extension pair lets Down distinguish
            // an untouched row from metadata intentionally edited after the upgrade.
            // A later migration can remove this table once v32 database downgrade
            // support is intentionally retired.
            migrationBuilder.CreateTable(
                name: "__CaveRevisionsCompletionLegacyFileNames",
                columns: table => new
                {
                    FileId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    OriginalFileName = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    MigratedName = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    MigratedExtension = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK___CaveRevisionsCompletionLegacyFileNames", x => x.FileId);
                });

            migrationBuilder.Sql("""
                INSERT INTO "__CaveRevisionsCompletionLegacyFileNames"
                    ("FileId", "OriginalFileName", "MigratedName", "MigratedExtension")
                SELECT "Id", "FileName", "Name", "Extension"
                FROM "Files"
                WHERE "FileName" <> "Name" || "Extension";
                """);

            migrationBuilder.DropColumn(
                name: "FileName",
                table: "Files");

            migrationBuilder.DropColumn(
                name: "ReportedByUserId",
                table: "Entrances");

            migrationBuilder.DropColumn(
                name: "ReportedByUserId",
                table: "Caves");

            migrationBuilder.AlterColumn<string>(
                name: "AccountId",
                table: "Files",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(10)",
                oldMaxLength: 10,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CurrentRevisionId",
                table: "Caves",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "Caves",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Files_AccountId_Id",
                table: "Files",
                columns: new[] { "AccountId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Counties_AccountId_Id",
                table: "Counties",
                columns: new[] { "AccountId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Caves_AccountId_Id",
                table: "Caves",
                columns: new[] { "AccountId", "Id" });

            migrationBuilder.CreateTable(
                name: "CaveImportBatches",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    AccountId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    SourceFileName = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SyncExisting = table.Column<bool>(type: "boolean", nullable: false),
                    Kind = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SourceRecordCount = table.Column<int>(type: "integer", nullable: false),
                    InsertedCount = table.Column<int>(type: "integer", nullable: false),
                    UpdatedCount = table.Column<int>(type: "integer", nullable: false),
                    DeletedCount = table.Column<int>(type: "integer", nullable: false),
                    NoChangeCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ModifiedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaveImportBatches", x => x.Id);
                    table.UniqueConstraint("AK_CaveImportBatches_AccountId_Id", x => new { x.AccountId, x.Id });
                    table.ForeignKey(
                        name: "FK_CaveImportBatches_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RetainedCaveFileObjects",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    AccountId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CaveId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    FileId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    StoragePartition = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ModifiedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RetainedCaveFileObjects", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CaveChangeRequestReviewEvents",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    AccountId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ChangeRequestId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ProposalVersionId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ActorUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Message = table.Column<string>(type: "text", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ModifiedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaveChangeRequestReviewEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CaveChangeRequestReviewEvents_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CaveChangeRequestReviewEvents_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CaveChangeRequests",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    AccountId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CaveId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    BaseRevisionId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CurrentProposalVersionId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ApprovedRevisionId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Status = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ReviewerUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ReviewedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewerNotes = table.Column<string>(type: "text", nullable: true),
                    BaseStateId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    BaseCountyId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ProposedStateId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ProposedCountyId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ModifiedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaveChangeRequests", x => x.Id);
                    table.UniqueConstraint("AK_CaveChangeRequests_AccountId_CaveId_Id", x => new { x.AccountId, x.CaveId, x.Id });
                    table.UniqueConstraint("AK_CaveChangeRequests_AccountId_Id", x => new { x.AccountId, x.Id });
                    table.ForeignKey(
                        name: "FK_CaveChangeRequests_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CaveChangeRequests_Counties_AccountId_BaseCountyId",
                        columns: x => new { x.AccountId, x.BaseCountyId },
                        principalTable: "Counties",
                        principalColumns: new[] { "AccountId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CaveChangeRequests_Counties_AccountId_ProposedCountyId",
                        columns: x => new { x.AccountId, x.ProposedCountyId },
                        principalTable: "Counties",
                        principalColumns: new[] { "AccountId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CaveChangeRequests_States_BaseStateId",
                        column: x => x.BaseStateId,
                        principalTable: "States",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CaveChangeRequests_States_ProposedStateId",
                        column: x => x.ProposedStateId,
                        principalTable: "States",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CaveChangeRequestStagedFiles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    AccountId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ChangeRequestId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    FileId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CreatedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ModifiedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaveChangeRequestStagedFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CaveChangeRequestStagedFiles_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CaveChangeRequestStagedFiles_CaveChangeRequests_AccountId_C~",
                        columns: x => new { x.AccountId, x.ChangeRequestId },
                        principalTable: "CaveChangeRequests",
                        principalColumns: new[] { "AccountId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CaveChangeRequestStagedFiles_Files_AccountId_FileId",
                        columns: x => new { x.AccountId, x.FileId },
                        principalTable: "Files",
                        principalColumns: new[] { "AccountId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CaveRevisions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    AccountId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CaveId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    PreviousRevisionId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Source = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Operation = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SnapshotSchemaVersion = table.Column<int>(type: "integer", nullable: false),
                    SnapshotJson = table.Column<string>(type: "jsonb", nullable: false),
                    ChangeRequestId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ImportBatchId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CreatedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ModifiedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaveRevisions", x => x.Id);
                    table.UniqueConstraint("AK_CaveRevisions_AccountId_CaveId_Id", x => new { x.AccountId, x.CaveId, x.Id });
                    table.UniqueConstraint("AK_CaveRevisions_AccountId_Id", x => new { x.AccountId, x.Id });
                    table.ForeignKey(
                        name: "FK_CaveRevisions_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CaveRevisions_CaveChangeRequests_AccountId_CaveId_ChangeReq~",
                        columns: x => new { x.AccountId, x.CaveId, x.ChangeRequestId },
                        principalTable: "CaveChangeRequests",
                        principalColumns: new[] { "AccountId", "CaveId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CaveRevisions_CaveImportBatches_AccountId_ImportBatchId",
                        columns: x => new { x.AccountId, x.ImportBatchId },
                        principalTable: "CaveImportBatches",
                        principalColumns: new[] { "AccountId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CaveRevisions_CaveRevisions_AccountId_CaveId_PreviousRevisi~",
                        columns: x => new { x.AccountId, x.CaveId, x.PreviousRevisionId },
                        principalTable: "CaveRevisions",
                        principalColumns: new[] { "AccountId", "CaveId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CaveProposalVersions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    AccountId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ChangeRequestId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CaveId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    BaseRevisionId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    PreviousProposalVersionId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    SchemaVersion = table.Column<int>(type: "integer", nullable: false),
                    ProposalJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ModifiedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaveProposalVersions", x => x.Id);
                    table.UniqueConstraint("AK_CaveProposalVersions_AccountId_ChangeRequestId_Id", x => new { x.AccountId, x.ChangeRequestId, x.Id });
                    table.UniqueConstraint("AK_CaveProposalVersions_AccountId_Id", x => new { x.AccountId, x.Id });
                    table.ForeignKey(
                        name: "FK_CaveProposalVersions_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CaveProposalVersions_CaveChangeRequests_AccountId_CaveId_Ch~",
                        columns: x => new { x.AccountId, x.CaveId, x.ChangeRequestId },
                        principalTable: "CaveChangeRequests",
                        principalColumns: new[] { "AccountId", "CaveId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CaveProposalVersions_CaveProposalVersions_AccountId_ChangeR~",
                        columns: x => new { x.AccountId, x.ChangeRequestId, x.PreviousProposalVersionId },
                        principalTable: "CaveProposalVersions",
                        principalColumns: new[] { "AccountId", "ChangeRequestId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CaveProposalVersions_CaveRevisions_AccountId_CaveId_BaseRev~",
                        columns: x => new { x.AccountId, x.CaveId, x.BaseRevisionId },
                        principalTable: "CaveRevisions",
                        principalColumns: new[] { "AccountId", "CaveId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Files_ValidNameAndExtension",
                table: "Files",
                sql: "length(\"Name\") + length(\"Extension\") <= 1000 AND btrim(\"Name\", ' ' || chr(9) || chr(10) || chr(11) || chr(12) || chr(13) || chr(133) || chr(160) || chr(5760) || chr(8192) || chr(8193) || chr(8194) || chr(8195) || chr(8196) || chr(8197) || chr(8198) || chr(8199) || chr(8200) || chr(8201) || chr(8202) || chr(8232) || chr(8233) || chr(8239) || chr(8287) || chr(12288)) <> '' AND \"Name\" NOT IN ('.', '..') AND \"Name\" NOT LIKE '%/%' AND \"Name\" NOT LIKE '%\\\\%' AND \"Name\" !~ '[[:cntrl:]]' AND (\"Extension\" = '' OR (length(\"Extension\") > 1 AND left(\"Extension\", 1) = '.' AND \"Extension\" NOT LIKE '%/%' AND \"Extension\" NOT LIKE '%\\\\%' AND \"Extension\" !~ '[[:cntrl:]]'))");

            migrationBuilder.CreateIndex(
                name: "IX_Caves_AccountId_Id_CurrentRevisionId",
                table: "Caves",
                columns: new[] { "AccountId", "Id", "CurrentRevisionId" });

            migrationBuilder.CreateIndex(
                name: "IX_CaveChangeRequestReviewEvents_AccountId_ChangeRequestId_Cre~",
                table: "CaveChangeRequestReviewEvents",
                columns: new[] { "AccountId", "ChangeRequestId", "CreatedOn" });

            migrationBuilder.CreateIndex(
                name: "IX_CaveChangeRequestReviewEvents_AccountId_ChangeRequestId_Pro~",
                table: "CaveChangeRequestReviewEvents",
                columns: new[] { "AccountId", "ChangeRequestId", "ProposalVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_CaveChangeRequestReviewEvents_ActorUserId",
                table: "CaveChangeRequestReviewEvents",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CaveChangeRequests_AccountId_BaseCountyId",
                table: "CaveChangeRequests",
                columns: new[] { "AccountId", "BaseCountyId" });

            migrationBuilder.CreateIndex(
                name: "IX_CaveChangeRequests_AccountId_CaveId_ApprovedRevisionId",
                table: "CaveChangeRequests",
                columns: new[] { "AccountId", "CaveId", "ApprovedRevisionId" });

            migrationBuilder.CreateIndex(
                name: "IX_CaveChangeRequests_AccountId_CaveId_BaseRevisionId",
                table: "CaveChangeRequests",
                columns: new[] { "AccountId", "CaveId", "BaseRevisionId" });

            migrationBuilder.CreateIndex(
                name: "IX_CaveChangeRequests_AccountId_Id_CurrentProposalVersionId",
                table: "CaveChangeRequests",
                columns: new[] { "AccountId", "Id", "CurrentProposalVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_CaveChangeRequests_AccountId_ProposedCountyId",
                table: "CaveChangeRequests",
                columns: new[] { "AccountId", "ProposedCountyId" });

            migrationBuilder.CreateIndex(
                name: "IX_CaveChangeRequests_AccountId_Status_CreatedOn",
                table: "CaveChangeRequests",
                columns: new[] { "AccountId", "Status", "CreatedOn" });

            migrationBuilder.CreateIndex(
                name: "IX_CaveChangeRequests_BaseStateId",
                table: "CaveChangeRequests",
                column: "BaseStateId");

            migrationBuilder.CreateIndex(
                name: "IX_CaveChangeRequests_CaveId_Status",
                table: "CaveChangeRequests",
                columns: new[] { "CaveId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CaveChangeRequests_ProposedStateId",
                table: "CaveChangeRequests",
                column: "ProposedStateId");

            migrationBuilder.CreateIndex(
                name: "IX_CaveChangeRequestStagedFiles_AccountId_ChangeRequestId",
                table: "CaveChangeRequestStagedFiles",
                columns: new[] { "AccountId", "ChangeRequestId" });

            migrationBuilder.CreateIndex(
                name: "IX_CaveChangeRequestStagedFiles_AccountId_FileId",
                table: "CaveChangeRequestStagedFiles",
                columns: new[] { "AccountId", "FileId" });

            migrationBuilder.CreateIndex(
                name: "IX_CaveChangeRequestStagedFiles_ChangeRequestId_FileId",
                table: "CaveChangeRequestStagedFiles",
                columns: new[] { "ChangeRequestId", "FileId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CaveImportBatches_AccountId_CreatedOn",
                table: "CaveImportBatches",
                columns: new[] { "AccountId", "CreatedOn" });

            migrationBuilder.CreateIndex(
                name: "IX_CaveProposalVersions_AccountId_CaveId_BaseRevisionId",
                table: "CaveProposalVersions",
                columns: new[] { "AccountId", "CaveId", "BaseRevisionId" });

            migrationBuilder.CreateIndex(
                name: "IX_CaveProposalVersions_AccountId_CaveId_ChangeRequestId",
                table: "CaveProposalVersions",
                columns: new[] { "AccountId", "CaveId", "ChangeRequestId" });

            migrationBuilder.CreateIndex(
                name: "IX_CaveProposalVersions_AccountId_ChangeRequestId_PreviousProp~",
                table: "CaveProposalVersions",
                columns: new[] { "AccountId", "ChangeRequestId", "PreviousProposalVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_CaveProposalVersions_ChangeRequestId_CreatedOn",
                table: "CaveProposalVersions",
                columns: new[] { "ChangeRequestId", "CreatedOn" });

            migrationBuilder.CreateIndex(
                name: "IX_CaveRevisions_AccountId_CaveId_ChangeRequestId",
                table: "CaveRevisions",
                columns: new[] { "AccountId", "CaveId", "ChangeRequestId" });

            migrationBuilder.CreateIndex(
                name: "IX_CaveRevisions_AccountId_CaveId_CreatedOn",
                table: "CaveRevisions",
                columns: new[] { "AccountId", "CaveId", "CreatedOn" });

            migrationBuilder.CreateIndex(
                name: "IX_CaveRevisions_AccountId_CaveId_PreviousRevisionId",
                table: "CaveRevisions",
                columns: new[] { "AccountId", "CaveId", "PreviousRevisionId" });

            migrationBuilder.CreateIndex(
                name: "IX_CaveRevisions_AccountId_ImportBatchId",
                table: "CaveRevisions",
                columns: new[] { "AccountId", "ImportBatchId" });

            migrationBuilder.CreateIndex(
                name: "IX_CaveRevisions_ChangeRequestId",
                table: "CaveRevisions",
                column: "ChangeRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_CaveRevisions_ImportBatchId",
                table: "CaveRevisions",
                column: "ImportBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_CaveRevisions_PreviousRevisionId",
                table: "CaveRevisions",
                column: "PreviousRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_RetainedCaveFileObjects_AccountId_CaveId",
                table: "RetainedCaveFileObjects",
                columns: new[] { "AccountId", "CaveId" });

            migrationBuilder.CreateIndex(
                name: "IX_RetainedCaveFileObjects_AccountId_FileId",
                table: "RetainedCaveFileObjects",
                columns: new[] { "AccountId", "FileId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Caves_CaveRevisions_AccountId_Id_CurrentRevisionId",
                table: "Caves",
                columns: new[] { "AccountId", "Id", "CurrentRevisionId" },
                principalTable: "CaveRevisions",
                principalColumns: new[] { "AccountId", "CaveId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Files_Accounts_AccountId",
                table: "Files",
                column: "AccountId",
                principalTable: "Accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CaveChangeRequestReviewEvents_CaveChangeRequests_AccountId_~",
                table: "CaveChangeRequestReviewEvents",
                columns: new[] { "AccountId", "ChangeRequestId" },
                principalTable: "CaveChangeRequests",
                principalColumns: new[] { "AccountId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CaveChangeRequestReviewEvents_CaveProposalVersions_AccountI~",
                table: "CaveChangeRequestReviewEvents",
                columns: new[] { "AccountId", "ChangeRequestId", "ProposalVersionId" },
                principalTable: "CaveProposalVersions",
                principalColumns: new[] { "AccountId", "ChangeRequestId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CaveChangeRequests_CaveProposalVersions_AccountId_Id_Curren~",
                table: "CaveChangeRequests",
                columns: new[] { "AccountId", "Id", "CurrentProposalVersionId" },
                principalTable: "CaveProposalVersions",
                principalColumns: new[] { "AccountId", "ChangeRequestId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CaveChangeRequests_CaveRevisions_AccountId_CaveId_ApprovedR~",
                table: "CaveChangeRequests",
                columns: new[] { "AccountId", "CaveId", "ApprovedRevisionId" },
                principalTable: "CaveRevisions",
                principalColumns: new[] { "AccountId", "CaveId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CaveChangeRequests_CaveRevisions_AccountId_CaveId_BaseRevis~",
                table: "CaveChangeRequests",
                columns: new[] { "AccountId", "CaveId", "BaseRevisionId" },
                principalTable: "CaveRevisions",
                principalColumns: new[] { "AccountId", "CaveId", "Id" },
                onDelete: ReferentialAction.Restrict);

            // Entrance feature defaults

            // Track exact rows inserted by this migration. The deterministic FeatureSetting
            // ID alone is not provenance: a preexisting setting can legitimately already
            // have that ID, in which case Up must leave it alone and Down must not delete it.
            migrationBuilder.CreateTable(
                name: "__BackfillEntranceOtherFeatureSettingRows",
                columns: table => new
                {
                    AccountId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    FeatureSettingId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK___BackfillEntranceOtherFeatureSettingRows", x => x.AccountId);
                });

            migrationBuilder.Sql("""
                WITH inserted AS (
                    INSERT INTO "FeatureSettings" ("Id", "AccountId", "Key", "IsEnabled", "IsDefault", "CreatedOn")
                    SELECT substr(md5(account."Id" || ':EnabledFieldEntranceOtherTags'), 1, 10),
                           account."Id", 'EnabledFieldEntranceOtherTags', true, false, NOW()
                    FROM "Accounts" AS account
                    WHERE NOT EXISTS (
                        SELECT 1 FROM "FeatureSettings" AS setting
                        WHERE setting."AccountId" = account."Id"
                          AND setting."Key" = 'EnabledFieldEntranceOtherTags')
                    RETURNING "Id", "AccountId"
                )
                INSERT INTO "__BackfillEntranceOtherFeatureSettingRows" ("AccountId", "FeatureSettingId")
                SELECT "AccountId", "Id"
                FROM inserted;
                """);

            // Durable account cleanup

            migrationBuilder.CreateTable(
                name: "AccountResetCleanupTargets",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    AccountId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    StoragePartition = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsContainer = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ModifiedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountResetCleanupTargets", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountResetCleanupTargets_AccountId",
                table: "AccountResetCleanupTargets",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountResetCleanupTargets_AccountId_StoragePartition_Stora~",
                table: "AccountResetCleanupTargets",
                columns: new[] { "AccountId", "StoragePartition", "StorageKey", "IsContainer" },
                unique: true);

            // File and account storage cleanup

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

            // Preserve File storage addresses as durable cleanup debt before dropping
            // temporary retained-object metadata.
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

            // New Cave proposals

            migrationBuilder.AlterColumn<string>(
                name: "BaseRevisionId",
                table: "CaveProposalVersions",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(10)",
                oldMaxLength: 10);

            // Permission projection

            // UserCavePermissions
            migrationBuilder.Sql("""
            DROP VIEW IF EXISTS "UserCavePermissions";
            """);

            migrationBuilder.Sql("""
            CREATE
            OR REPLACE VIEW "UserCavePermissions" AS
            SELECT cp."AccountId",
                   cp."UserId",
                   c."Id" AS "CaveId",
                   NULL::character varying AS "CountyId",
                   p."Key"                 AS "PermissionKey",
                   p."Id"                  AS "PermissionId",
                   c."StateId"             AS "StateId"
            FROM "CavePermission" cp
                JOIN "Caves" c
            ON c."AccountId"::text = cp."AccountId"::text AND
                (cp."CaveId" IS NOT NULL AND cp."CaveId"::text = c."Id"::text OR
                cp."CountyId" IS NOT NULL AND cp."CountyId"::text = c."CountyId"::text OR
                cp."StateId" IS NOT NULL AND cp."StateId"::text = c."StateId"::text OR
                cp."CaveId" IS NULL AND cp."CountyId" IS NULL AND cp."StateId" IS NULL)
                JOIN "Permissions" p ON cp."PermissionId"::text = p."Id"::text
            WHERE p."PermissionType"::text = 'Cave'::text
              AND p."Key"::text IN ('View'::text, 'Manager'::text)
            UNION
            SELECT cp."AccountId",
                   cp."UserId",
                   NULL::character varying AS "CaveId",
                   ct."Id"                 AS "CountyId",
                   p."Key"                 AS "PermissionKey",
                   p."Id"                  AS "PermissionId",
                   ct."StateId"            AS "StateId"
            FROM "CavePermission" cp
                JOIN "Counties" ct
            ON ct."AccountId"::text = cp."AccountId"::text AND
                (cp."CountyId" IS NOT NULL AND cp."CountyId"::text = ct."Id"::text OR
                cp."StateId" IS NOT NULL AND cp."StateId"::text = ct."StateId"::text OR
                cp."CaveId" IS NULL AND cp."CountyId" IS NULL AND cp."StateId" IS NULL)
                JOIN "Permissions" p ON cp."PermissionId"::text = p."Id"::text
            WHERE p."PermissionType"::text = 'Cave'::text
              AND p."Key"::text IN ('View'::text, 'Manager'::text)
            UNION
            SELECT up."AccountId",
                   up."UserId",
                   c."Id" AS "CaveId",
                   NULL::character varying AS "CountyId",
                   p."Key"                 AS "PermissionKey",
                   p."Id"                  AS "PermissionId",
                   c."StateId"             AS "StateId"
            FROM "UserPermissions" up
                JOIN "Caves" c
            ON c."AccountId"::text = up."AccountId"::text
                CROSS JOIN "Permissions" p
            WHERE (up."PermissionId"::text IN (SELECT "Permissions"."Id"
                FROM "Permissions"
                WHERE "Permissions"."Key"::text = 'Admin'::text
                  AND "Permissions"."PermissionType"::text = 'User'::text))
              AND p."PermissionType"::text = 'Cave'::text
              AND p."Key"::text IN ('View'::text, 'Manager'::text)
            UNION
            SELECT up."AccountId",
                   up."UserId",
                   NULL::character varying AS "CaveId",
                   ct."Id"                 AS "CountyId",
                   p."Key"                 AS "PermissionKey",
                   p."Id"                  AS "PermissionId",
                   ct."StateId"            AS "StateId"
            FROM "UserPermissions" up
                JOIN "Counties" ct
            ON ct."AccountId"::text = up."AccountId"::text
                CROSS JOIN "Permissions" p
            WHERE (up."PermissionId"::text IN (SELECT "Permissions"."Id"
                FROM "Permissions"
                WHERE "Permissions"."Key"::text = 'Admin'::text
                  AND "Permissions"."PermissionType"::text = 'User'::text))
              AND p."PermissionType"::text = 'Cave'::text
              AND p."Key"::text IN ('View'::text, 'Manager'::text);
            """);

            // Unique permission identity

            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM "Permissions"
                        GROUP BY "PermissionType", "Key"
                        HAVING COUNT(*) > 1
                    ) THEN
                        RAISE EXCEPTION
                            'Cannot enforce permission identity because duplicate (PermissionType, Key) rows exist.';
                    END IF;
                END
                $$;
                """);

            migrationBuilder.DropIndex(
                name: "IX_Permissions_PermissionType_Key",
                table: "Permissions");

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_PermissionType_Key",
                table: "Permissions",
                columns: new[] { "PermissionType", "Key" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Unique permission identity

            migrationBuilder.DropIndex(
                name: "IX_Permissions_PermissionType_Key",
                table: "Permissions");

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_PermissionType_Key",
                table: "Permissions",
                columns: new[] { "PermissionType", "Key" });

            // Permission projection

            // UserCavePermissions
            migrationBuilder.Sql("""
            DROP VIEW IF EXISTS "UserCavePermissions";
            """);

            migrationBuilder.Sql("""
            CREATE
            OR REPLACE VIEW "UserCavePermissions" AS
            SELECT cp."AccountId",
                   cp."UserId",
                   c."Id" AS "CaveId",
                   NULL::character varying AS "CountyId",
                   p."Key"                 AS "PermissionKey",
                   p."Id"                  AS "PermissionId",
                   c."StateId"             AS "StateId"
            FROM "CavePermission" cp
                JOIN "Caves" c
            ON c."AccountId"::text = cp."AccountId"::text AND
                (cp."CaveId" IS NOT NULL AND cp."CaveId"::text = c."Id"::text OR
                cp."CountyId" IS NOT NULL AND cp."CountyId"::text = c."CountyId"::text OR
                cp."StateId" IS NOT NULL AND cp."StateId"::text = c."StateId"::text OR
                cp."CaveId" IS NULL AND cp."CountyId" IS NULL AND cp."StateId" IS NULL)
                JOIN "Permissions" p ON cp."PermissionId"::text = p."Id"::text
            WHERE p."PermissionType"::text = 'Cave'::text
            UNION
            SELECT cp."AccountId",
                   cp."UserId",
                   NULL::character varying AS "CaveId",
                   ct."Id"                 AS "CountyId",
                   p."Key"                 AS "PermissionKey",
                   p."Id"                  AS "PermissionId",
                   ct."StateId"            AS "StateId"
            FROM "CavePermission" cp
                JOIN "Counties" ct
            ON ct."AccountId"::text = cp."AccountId"::text AND
                (cp."CountyId" IS NOT NULL AND cp."CountyId"::text = ct."Id"::text OR
                cp."StateId" IS NOT NULL AND cp."StateId"::text = ct."StateId"::text OR
                cp."CaveId" IS NULL AND cp."CountyId" IS NULL AND cp."StateId" IS NULL)
                JOIN "Permissions" p ON cp."PermissionId"::text = p."Id"::text
            WHERE p."PermissionType"::text = 'Cave'::text
            UNION
            SELECT up."AccountId",
                   up."UserId",
                   c."Id" AS "CaveId",
                   NULL::character varying AS "CountyId",
                   p."Key"                 AS "PermissionKey",
                   p."Id"                  AS "PermissionId",
                   c."StateId"             AS "StateId"
            FROM "UserPermissions" up
                JOIN "Caves" c
            ON c."AccountId"::text = up."AccountId"::text
                CROSS JOIN "Permissions" p
            WHERE (up."PermissionId"::text IN (SELECT "Permissions"."Id"
                FROM "Permissions"
                WHERE "Permissions"."Key"::text = 'Admin'::text))
              AND p."PermissionType"::text = 'Cave'::text
            UNION
            SELECT up."AccountId",
                   up."UserId",
                   NULL::character varying AS "CaveId",
                   ct."Id"                 AS "CountyId",
                   p."Key"                 AS "PermissionKey",
                   p."Id"                  AS "PermissionId",
                   ct."StateId"            AS "StateId"
            FROM "UserPermissions" up
                JOIN "Counties" ct
            ON ct."AccountId"::text = up."AccountId"::text
                CROSS JOIN "Permissions" p
            WHERE (up."PermissionId"::text IN (SELECT "Permissions"."Id"
                FROM "Permissions"
                WHERE "Permissions"."Key"::text = 'Admin'::text))
              AND p."PermissionType"::text = 'Cave'::text;
            """);

            // New Cave proposals

            // A creation proposal has no published base revision, so it cannot be represented by
            // the pre-feature schema. Fail closed with an actionable message instead of relying on
            // an incidental NOT NULL violation from ALTER TABLE.
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM "CaveProposalVersions"
                        WHERE "BaseRevisionId" IS NULL
                    ) THEN
                        RAISE EXCEPTION
                            'Cannot downgrade CaveRevisionsCompletion while new-Cave proposal versions exist.';
                    END IF;
                END
                $$;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "BaseRevisionId",
                table: "CaveProposalVersions",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(10)",
                oldMaxLength: 10,
                oldNullable: true);

            // File and account storage cleanup

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

            // Durable account cleanup

            // Downgrading with pending targets would erase the only durable addresses
            // of blobs still awaiting deletion. Require cleanup first.
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM "AccountResetCleanupTargets") THEN
                        RAISE EXCEPTION 'Cannot downgrade with pending account reset cleanup';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropTable(
                name: "AccountResetCleanupTargets");

            // Entrance feature defaults

            migrationBuilder.Sql("""
                DELETE FROM "FeatureSettings" AS setting
                USING "__BackfillEntranceOtherFeatureSettingRows" AS inserted
                WHERE setting."AccountId" = inserted."AccountId"
                  AND setting."Id" = inserted."FeatureSettingId"
                  AND setting."Key" = 'EnabledFieldEntranceOtherTags';
                """);

            migrationBuilder.DropTable(
                name: "__BackfillEntranceOtherFeatureSettingRows");

            // Cave revision and File schema

            migrationBuilder.DropForeignKey(
                name: "FK_Caves_CaveRevisions_AccountId_Id_CurrentRevisionId",
                table: "Caves");

            migrationBuilder.DropForeignKey(
                name: "FK_Files_Accounts_AccountId",
                table: "Files");

            migrationBuilder.DropForeignKey(
                name: "FK_CaveProposalVersions_CaveChangeRequests_AccountId_CaveId_Ch~",
                table: "CaveProposalVersions");

            migrationBuilder.DropForeignKey(
                name: "FK_CaveRevisions_CaveChangeRequests_AccountId_CaveId_ChangeReq~",
                table: "CaveRevisions");

            migrationBuilder.DropTable(
                name: "CaveChangeRequestReviewEvents");

            migrationBuilder.DropTable(
                name: "CaveChangeRequestStagedFiles");

            migrationBuilder.DropTable(
                name: "RetainedCaveFileObjects");

            migrationBuilder.DropTable(
                name: "CaveChangeRequests");

            migrationBuilder.DropTable(
                name: "CaveProposalVersions");

            migrationBuilder.DropTable(
                name: "CaveRevisions");

            migrationBuilder.DropTable(
                name: "CaveImportBatches");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Files_AccountId_Id",
                table: "Files");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Files_ValidNameAndExtension",
                table: "Files");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Counties_AccountId_Id",
                table: "Counties");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Caves_AccountId_Id",
                table: "Caves");

            migrationBuilder.DropIndex(
                name: "IX_Caves_AccountId_Id_CurrentRevisionId",
                table: "Caves");

            // Reconstruct the v32 filename without leaving a permanent default.
            // New/edited rows use the current Name + Extension. For a pre-upgrade
            // row whose migrated metadata is still untouched, restore the original
            // filename captured by Up().
            migrationBuilder.AddColumn<string>(
                name: "FileName",
                table: "Files",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "Files"
                SET "FileName" = "Name" || "Extension";

                UPDATE "Files" AS file
                SET "FileName" = legacy."OriginalFileName"
                FROM "__CaveRevisionsCompletionLegacyFileNames" AS legacy
                WHERE legacy."FileId" = file."Id"
                  AND file."Name" = legacy."MigratedName"
                  AND file."Extension" = legacy."MigratedExtension";
                """);

            migrationBuilder.AlterColumn<string>(
                name: "FileName",
                table: "Files",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.DropTable(
                name: "__CaveRevisionsCompletionLegacyFileNames");

            migrationBuilder.DropColumn(
                name: "Extension",
                table: "Files");

            migrationBuilder.RenameColumn(
                name: "Name",
                table: "Files",
                newName: "DisplayName");

            migrationBuilder.AlterColumn<string>(
                name: "DisplayName",
                table: "Files",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000);

            migrationBuilder.DropColumn(
                name: "CurrentRevisionId",
                table: "Caves");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "Caves");

            migrationBuilder.AlterColumn<string>(
                name: "AccountId",
                table: "Files",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(10)",
                oldMaxLength: 10);

            migrationBuilder.AddColumn<string>(
                name: "ReportedByUserId",
                table: "Caves",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReportedByUserId",
                table: "Entrances",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Files_AccountId",
                table: "Files",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Entrances_ReportedByUserId",
                table: "Entrances",
                column: "ReportedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Caves_AccountId",
                table: "Caves",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Caves_ReportedByUserId",
                table: "Caves",
                column: "ReportedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Caves_Users_ReportedByUserId",
                table: "Caves",
                column: "ReportedByUserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Entrances_Users_ReportedByUserId",
                table: "Entrances",
                column: "ReportedByUserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Files_Accounts_AccountId",
                table: "Files",
                column: "AccountId",
                principalTable: "Accounts",
                principalColumn: "Id");
        }
    }
}
