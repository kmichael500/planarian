using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planarian.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class v27 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Entrances_Location",
                table: "Entrances",
                column: "Location")
                .Annotation("Npgsql:IndexMethod", "GIST");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Entrances_Location",
                table: "Entrances");
        }
    }
}
