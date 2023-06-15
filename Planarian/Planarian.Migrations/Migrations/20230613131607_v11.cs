using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planarian.Migrations.Migrations
{
    public partial class v11 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Entrances_CaveId_IsPrimary",
                table: "Entrances");

            migrationBuilder.CreateIndex(
                name: "IX_Entrances_CaveId",
                table: "Entrances",
                column: "CaveId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Entrances_CaveId",
                table: "Entrances");

            migrationBuilder.CreateIndex(
                name: "IX_Entrances_CaveId_IsPrimary",
                table: "Entrances",
                columns: new[] { "CaveId", "IsPrimary" },
                unique: true,
                filter: "[IsPrimary] = 1");
        }
    }
}
