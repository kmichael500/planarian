using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planarian.Migrations.Migrations
{
    public partial class v18 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Counties_DisplayId",
                table: "Counties",
                column: "DisplayId");

            migrationBuilder.CreateIndex(
                name: "IX_Caves_CountyNumber",
                table: "Caves",
                column: "CountyNumber");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Counties_DisplayId",
                table: "Counties");

            migrationBuilder.DropIndex(
                name: "IX_Caves_CountyNumber",
                table: "Caves");
        }
    }
}
