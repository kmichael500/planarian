using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planarian.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AccountResetCleanupOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
        }
    }
}
