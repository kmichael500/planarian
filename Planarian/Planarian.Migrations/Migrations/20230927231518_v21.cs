using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planarian.Migrations.Migrations
{
    public partial class v21 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Key",
                table: "TagTypes",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "AccountId",
                table: "TagTypes",
                type: "nvarchar(10)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TagTypes_AccountId",
                table: "TagTypes",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_TagTypes_Key",
                table: "TagTypes",
                column: "Key");

            migrationBuilder.AddForeignKey(
                name: "FK_TagTypes_Accounts_AccountId",
                table: "TagTypes",
                column: "AccountId",
                principalTable: "Accounts",
                principalColumn: "Id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TagTypes_Accounts_AccountId",
                table: "TagTypes");

            migrationBuilder.DropIndex(
                name: "IX_TagTypes_AccountId",
                table: "TagTypes");

            migrationBuilder.DropIndex(
                name: "IX_TagTypes_Key",
                table: "TagTypes");

            migrationBuilder.AlterColumn<string>(
                name: "Key",
                table: "TagTypes",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldMaxLength: 450);

            migrationBuilder.AlterColumn<string>(
                name: "AccountId",
                table: "TagTypes",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(10)",
                oldNullable: true);
        }
    }
}
