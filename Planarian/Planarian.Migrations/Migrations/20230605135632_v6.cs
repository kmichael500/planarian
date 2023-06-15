using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planarian.Migrations.Migrations
{
    public partial class v6 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Caves_Entrances_PrimaryEntranceId",
                table: "Caves");

            migrationBuilder.DropIndex(
                name: "IX_Caves_PrimaryEntranceId",
                table: "Caves");

            migrationBuilder.DropColumn(
                name: "PrimaryEntranceId",
                table: "Caves");

            migrationBuilder.AddColumn<string>(
                name: "PrimaryEntranceCaveId",
                table: "Entrances",
                type: "nvarchar(10)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Entrances_PrimaryEntranceCaveId",
                table: "Entrances",
                column: "PrimaryEntranceCaveId",
                unique: true,
                filter: "[PrimaryEntranceCaveId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Entrances_Caves_PrimaryEntranceCaveId",
                table: "Entrances",
                column: "PrimaryEntranceCaveId",
                principalTable: "Caves",
                principalColumn: "Id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Entrances_Caves_PrimaryEntranceCaveId",
                table: "Entrances");

            migrationBuilder.DropIndex(
                name: "IX_Entrances_PrimaryEntranceCaveId",
                table: "Entrances");

            migrationBuilder.DropColumn(
                name: "PrimaryEntranceCaveId",
                table: "Entrances");

            migrationBuilder.AddColumn<string>(
                name: "PrimaryEntranceId",
                table: "Caves",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Caves_PrimaryEntranceId",
                table: "Caves",
                column: "PrimaryEntranceId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Caves_Entrances_PrimaryEntranceId",
                table: "Caves",
                column: "PrimaryEntranceId",
                principalTable: "Entrances",
                principalColumn: "Id");
        }
    }
}
