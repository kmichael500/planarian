using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planarian.Migrations.Migrations
{
    public partial class v3 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TripObjectives_TripObjectiveTypes_TripObjectiveTypeId",
                table: "TripObjectives");

            migrationBuilder.DropIndex(
                name: "IX_TripObjectives_TripObjectiveTypeId",
                table: "TripObjectives");

            migrationBuilder.CreateTable(
                name: "TripObjectiveTripObjectiveType",
                columns: table => new
                {
                    TripObjectiveTypesId = table.Column<string>(type: "nvarchar(10)", nullable: false),
                    TripObjectivesId = table.Column<string>(type: "nvarchar(10)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TripObjectiveTripObjectiveType", x => new { x.TripObjectiveTypesId, x.TripObjectivesId });
                    table.ForeignKey(
                        name: "FK_TripObjectiveTripObjectiveType_TripObjectives_TripObjectivesId",
                        column: x => x.TripObjectivesId,
                        principalTable: "TripObjectives",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TripObjectiveTripObjectiveType_TripObjectiveTypes_TripObjectiveTypesId",
                        column: x => x.TripObjectiveTypesId,
                        principalTable: "TripObjectiveTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TripObjectiveTripObjectiveType_TripObjectivesId",
                table: "TripObjectiveTripObjectiveType",
                column: "TripObjectivesId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TripObjectiveTripObjectiveType");

            migrationBuilder.CreateIndex(
                name: "IX_TripObjectives_TripObjectiveTypeId",
                table: "TripObjectives",
                column: "TripObjectiveTypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_TripObjectives_TripObjectiveTypes_TripObjectiveTypeId",
                table: "TripObjectives",
                column: "TripObjectiveTypeId",
                principalTable: "TripObjectiveTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
