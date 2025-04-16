using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planarian.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class v26 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CaveGeoJsons",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CaveId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    GeoJson = table.Column<string>(type: "jsonb", maxLength: 2147483647, nullable: false),
                    CreatedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ModifiedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaveGeoJsons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CaveGeoJsons_Caves_CaveId",
                        column: x => x.CaveId,
                        principalTable: "Caves",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_CaveGeoJsons_CaveId",
                table: "CaveGeoJsons",
                column: "CaveId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CaveGeoJsons");
        }
    }
}
