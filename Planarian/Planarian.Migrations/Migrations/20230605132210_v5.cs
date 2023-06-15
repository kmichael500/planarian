using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planarian.Migrations.Migrations
{
    public partial class v5 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "StateId",
                table: "Caves",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Caves_StateId",
                table: "Caves",
                column: "StateId");

            migrationBuilder.AddForeignKey(
                name: "FK_Caves_States_StateId",
                table: "Caves",
                column: "StateId",
                principalTable: "States",
                principalColumn: "Id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Caves_States_StateId",
                table: "Caves");

            migrationBuilder.DropIndex(
                name: "IX_Caves_StateId",
                table: "Caves");

            migrationBuilder.DropColumn(
                name: "StateId",
                table: "Caves");
        }
    }
}
