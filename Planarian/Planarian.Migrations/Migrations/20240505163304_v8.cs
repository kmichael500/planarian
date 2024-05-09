using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planarian.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class v8 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Counties_AccountId_DisplayId",
                table: "Counties");

            migrationBuilder.DropIndex(
                name: "IX_Counties_DisplayId",
                table: "Counties");

            migrationBuilder.CreateIndex(
                name: "IX_Counties_AccountId_StateId_DisplayId",
                table: "Counties",
                columns: new[] { "AccountId", "StateId", "DisplayId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Counties_AccountId_StateId_DisplayId",
                table: "Counties");

            migrationBuilder.CreateIndex(
                name: "IX_Counties_AccountId_DisplayId",
                table: "Counties",
                columns: new[] { "AccountId", "DisplayId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Counties_DisplayId",
                table: "Counties",
                column: "DisplayId");
        }
    }
}
