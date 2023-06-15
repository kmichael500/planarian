using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planarian.Migrations.Migrations
{
    public partial class v7 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Counties_AccountId",
                table: "Counties");

            migrationBuilder.RenameColumn(
                name: "CaveNumber",
                table: "Caves",
                newName: "CountyNumber");

            migrationBuilder.CreateIndex(
                name: "IX_Counties_AccountId_DisplayId",
                table: "Counties",
                columns: new[] { "AccountId", "DisplayId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Caves_CountyNumber_CountyId",
                table: "Caves",
                columns: new[] { "CountyNumber", "CountyId" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Counties_AccountId_DisplayId",
                table: "Counties");

            migrationBuilder.DropIndex(
                name: "IX_Caves_CountyNumber_CountyId",
                table: "Caves");

            migrationBuilder.RenameColumn(
                name: "CountyNumber",
                table: "Caves",
                newName: "CaveNumber");

            migrationBuilder.CreateIndex(
                name: "IX_Counties_AccountId",
                table: "Counties",
                column: "AccountId");
        }
    }
}
