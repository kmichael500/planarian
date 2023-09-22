using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planarian.Migrations.Migrations
{
    public partial class v17 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Caves_DepthFeet",
                table: "Caves",
                column: "DepthFeet");

            migrationBuilder.CreateIndex(
                name: "IX_Caves_LengthFeet",
                table: "Caves",
                column: "LengthFeet");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Caves_DepthFeet",
                table: "Caves");

            migrationBuilder.DropIndex(
                name: "IX_Caves_LengthFeet",
                table: "Caves");
        }
    }
}
