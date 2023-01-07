using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planarian.Migrations.Migrations
{
    public partial class v9 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TripObjectiveTripObjectiveType");

            migrationBuilder.DropTable(
                name: "TripObjectiveTypes");

            migrationBuilder.AddColumn<string>(
                name: "TagId",
                table: "TripObjectives",
                type: "nvarchar(10)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Tags",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Key = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedByName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tags", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TripObjectiveTag",
                columns: table => new
                {
                    TagId = table.Column<string>(type: "nvarchar(10)", nullable: false),
                    TripObjectiveId = table.Column<string>(type: "nvarchar(10)", nullable: false),
                    Id = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedByName = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TripObjectiveTag", x => new { x.TagId, x.TripObjectiveId });
                    table.ForeignKey(
                        name: "FK_TripObjectiveTag_Tags_TagId",
                        column: x => x.TagId,
                        principalTable: "Tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TripObjectiveTag_TripObjectives_TripObjectiveId",
                        column: x => x.TripObjectiveId,
                        principalTable: "TripObjectives",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TripObjectives_TagId",
                table: "TripObjectives",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "IX_TripObjectiveTag_TripObjectiveId",
                table: "TripObjectiveTag",
                column: "TripObjectiveId");

            migrationBuilder.AddForeignKey(
                name: "FK_TripObjectives_Tags_TagId",
                table: "TripObjectives",
                column: "TagId",
                principalTable: "Tags",
                principalColumn: "Id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TripObjectives_Tags_TagId",
                table: "TripObjectives");

            migrationBuilder.DropTable(
                name: "TripObjectiveTag");

            migrationBuilder.DropTable(
                name: "Tags");

            migrationBuilder.DropIndex(
                name: "IX_TripObjectives_TagId",
                table: "TripObjectives");

            migrationBuilder.DropColumn(
                name: "TagId",
                table: "TripObjectives");

            migrationBuilder.CreateTable(
                name: "TripObjectiveTypes",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CreatedByName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TripObjectiveTypes", x => x.Id);
                });

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
    }
}
