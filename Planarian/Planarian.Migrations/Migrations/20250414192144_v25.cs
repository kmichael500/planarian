using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;

#nullable disable

namespace Planarian.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class v25 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<NpgsqlTsVector>(
                name: "NarrativeSearchVector",
                table: "Caves",
                type: "tsvector",
                nullable: false)
                .Annotation("Npgsql:TsVectorConfig", "english")
                .Annotation("Npgsql:TsVectorProperties", new[] { "Narrative" });

            migrationBuilder.CreateIndex(
                name: "IX_Caves_NarrativeSearchVector",
                table: "Caves",
                column: "NarrativeSearchVector")
                .Annotation("Npgsql:IndexMethod", "GIN");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Caves_NarrativeSearchVector",
                table: "Caves");

            migrationBuilder.DropColumn(
                name: "NarrativeSearchVector",
                table: "Caves");
        }
    }
}
