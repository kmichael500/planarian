using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planarian.Migrations.Migrations
{
    public partial class v20 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TripObjectives_Trip_TripId",
                table: "TripObjectives");

            migrationBuilder.DropTable(
                name: "Trip");

            migrationBuilder.RenameColumn(
                name: "TripId",
                table: "TripObjectives",
                newName: "ProjectId");

            migrationBuilder.RenameIndex(
                name: "IX_TripObjectives_TripId",
                table: "TripObjectives",
                newName: "IX_TripObjectives_ProjectId");

            migrationBuilder.AddForeignKey(
                name: "FK_TripObjectives_Projects_ProjectId",
                table: "TripObjectives",
                column: "ProjectId",
                principalTable: "Projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TripObjectives_Projects_ProjectId",
                table: "TripObjectives");

            migrationBuilder.RenameColumn(
                name: "ProjectId",
                table: "TripObjectives",
                newName: "TripId");

            migrationBuilder.RenameIndex(
                name: "IX_TripObjectives_ProjectId",
                table: "TripObjectives",
                newName: "IX_TripObjectives_TripId");

            migrationBuilder.CreateTable(
                name: "Trip",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    ProjectId = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CreatedByName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TripDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Trip", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Trip_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Trip_ProjectId",
                table: "Trip",
                column: "ProjectId");

            migrationBuilder.AddForeignKey(
                name: "FK_TripObjectives_Trip_TripId",
                table: "TripObjectives",
                column: "TripId",
                principalTable: "Trip",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
