using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planarian.Migrations.Migrations
{
    public partial class v10 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Caves_Accounts_AccountId",
                table: "Caves");

            migrationBuilder.DropForeignKey(
                name: "FK_GeologyTags_Caves_CaveId",
                table: "GeologyTags");

            migrationBuilder.DropForeignKey(
                name: "FK_GeologyTags_TagTypes_TagTypeId",
                table: "GeologyTags");

            migrationBuilder.DropForeignKey(
                name: "FK_Maps_Caves_CaveId",
                table: "Maps");

            migrationBuilder.DropForeignKey(
                name: "FK_Maps_TagTypes_MapStatusTagId",
                table: "Maps");

            migrationBuilder.AddForeignKey(
                name: "FK_Caves_Accounts_AccountId",
                table: "Caves",
                column: "AccountId",
                principalTable: "Accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_GeologyTags_Caves_CaveId",
                table: "GeologyTags",
                column: "CaveId",
                principalTable: "Caves",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_GeologyTags_TagTypes_TagTypeId",
                table: "GeologyTags",
                column: "TagTypeId",
                principalTable: "TagTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Maps_Caves_CaveId",
                table: "Maps",
                column: "CaveId",
                principalTable: "Caves",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Maps_TagTypes_MapStatusTagId",
                table: "Maps",
                column: "MapStatusTagId",
                principalTable: "TagTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Caves_Accounts_AccountId",
                table: "Caves");

            migrationBuilder.DropForeignKey(
                name: "FK_GeologyTags_Caves_CaveId",
                table: "GeologyTags");

            migrationBuilder.DropForeignKey(
                name: "FK_GeologyTags_TagTypes_TagTypeId",
                table: "GeologyTags");

            migrationBuilder.DropForeignKey(
                name: "FK_Maps_Caves_CaveId",
                table: "Maps");

            migrationBuilder.DropForeignKey(
                name: "FK_Maps_TagTypes_MapStatusTagId",
                table: "Maps");

            migrationBuilder.AddForeignKey(
                name: "FK_Caves_Accounts_AccountId",
                table: "Caves",
                column: "AccountId",
                principalTable: "Accounts",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_GeologyTags_Caves_CaveId",
                table: "GeologyTags",
                column: "CaveId",
                principalTable: "Caves",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_GeologyTags_TagTypes_TagTypeId",
                table: "GeologyTags",
                column: "TagTypeId",
                principalTable: "TagTypes",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Maps_Caves_CaveId",
                table: "Maps",
                column: "CaveId",
                principalTable: "Caves",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Maps_TagTypes_MapStatusTagId",
                table: "Maps",
                column: "MapStatusTagId",
                principalTable: "TagTypes",
                principalColumn: "Id");
        }
    }
}
