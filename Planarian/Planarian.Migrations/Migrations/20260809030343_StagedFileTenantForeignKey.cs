using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planarian.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class StagedFileTenantForeignKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CaveChangeRequestStagedFiles_Files_FileId",
                table: "CaveChangeRequestStagedFiles");

            migrationBuilder.DropForeignKey(
                name: "FK_Files_Accounts_AccountId",
                table: "Files");

            migrationBuilder.DropIndex(
                name: "IX_Files_AccountId",
                table: "Files");

            migrationBuilder.DropIndex(
                name: "IX_CaveChangeRequestStagedFiles_FileId",
                table: "CaveChangeRequestStagedFiles");

            // Historic cave files predate File.AccountId being a domain invariant.
            // Their owner is unambiguous and can be recovered from the cave. Do not
            // manufacture an empty or arbitrary account for any other legacy row.
            migrationBuilder.Sql("""
                update "Files" as file
                set "AccountId" = cave."AccountId"
                from "Caves" as cave
                where file."AccountId" is null
                  and file."CaveId" = cave."Id";

                do $$
                begin
                    if exists (select 1 from "Files" where "AccountId" is null) then
                        raise exception 'Cannot migrate Files.AccountId: legacy file rows without a determinable account owner exist.';
                    end if;
                end $$;
                """);

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

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Files_AccountId_Id",
                table: "Files",
                columns: new[] { "AccountId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_CaveChangeRequestStagedFiles_AccountId_FileId",
                table: "CaveChangeRequestStagedFiles",
                columns: new[] { "AccountId", "FileId" });

            migrationBuilder.AddForeignKey(
                name: "FK_CaveChangeRequestStagedFiles_Files_AccountId_FileId",
                table: "CaveChangeRequestStagedFiles",
                columns: new[] { "AccountId", "FileId" },
                principalTable: "Files",
                principalColumns: new[] { "AccountId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Files_Accounts_AccountId",
                table: "Files",
                column: "AccountId",
                principalTable: "Accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CaveChangeRequestStagedFiles_Files_AccountId_FileId",
                table: "CaveChangeRequestStagedFiles");

            migrationBuilder.DropForeignKey(
                name: "FK_Files_Accounts_AccountId",
                table: "Files");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Files_AccountId_Id",
                table: "Files");

            migrationBuilder.DropIndex(
                name: "IX_CaveChangeRequestStagedFiles_AccountId_FileId",
                table: "CaveChangeRequestStagedFiles");

            migrationBuilder.AlterColumn<string>(
                name: "AccountId",
                table: "Files",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(10)",
                oldMaxLength: 10);

            migrationBuilder.CreateIndex(
                name: "IX_Files_AccountId",
                table: "Files",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_CaveChangeRequestStagedFiles_FileId",
                table: "CaveChangeRequestStagedFiles",
                column: "FileId");

            migrationBuilder.AddForeignKey(
                name: "FK_CaveChangeRequestStagedFiles_Files_FileId",
                table: "CaveChangeRequestStagedFiles",
                column: "FileId",
                principalTable: "Files",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Files_Accounts_AccountId",
                table: "Files",
                column: "AccountId",
                principalTable: "Accounts",
                principalColumn: "Id");
        }
    }
}
