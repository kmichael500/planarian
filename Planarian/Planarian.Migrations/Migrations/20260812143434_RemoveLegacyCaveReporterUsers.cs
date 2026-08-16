using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planarian.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class RemoveLegacyCaveReporterUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Caves_Users_ReportedByUserId",
                table: "Caves");

            migrationBuilder.DropForeignKey(
                name: "FK_Entrances_Users_ReportedByUserId",
                table: "Entrances");

            migrationBuilder.DropIndex(
                name: "IX_Entrances_ReportedByUserId",
                table: "Entrances");

            migrationBuilder.DropIndex(
                name: "IX_Caves_ReportedByUserId",
                table: "Caves");

            migrationBuilder.DropColumn(
                name: "ReportedByUserId",
                table: "Entrances");

            migrationBuilder.DropColumn(
                name: "ReportedByUserId",
                table: "Caves");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReportedByUserId",
                table: "Entrances",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReportedByUserId",
                table: "Caves",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Entrances_ReportedByUserId",
                table: "Entrances",
                column: "ReportedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Caves_ReportedByUserId",
                table: "Caves",
                column: "ReportedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Caves_Users_ReportedByUserId",
                table: "Caves",
                column: "ReportedByUserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Entrances_Users_ReportedByUserId",
                table: "Entrances",
                column: "ReportedByUserId",
                principalTable: "Users",
                principalColumn: "Id");
        }
    }
}
