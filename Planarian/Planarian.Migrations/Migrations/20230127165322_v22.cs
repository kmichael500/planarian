using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planarian.Migrations.Migrations
{
    public partial class v22 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Lead_TripObjectives_TripObjectiveId",
                table: "Lead");

            migrationBuilder.DropForeignKey(
                name: "FK_Photos_TripObjectives_TripObjectiveId",
                table: "Photos");

            migrationBuilder.DropTable(
                name: "TripObjectiveMembers");

            migrationBuilder.DropTable(
                name: "TripObjectiveTag");

            migrationBuilder.DropTable(
                name: "TripObjectives");

            migrationBuilder.RenameColumn(
                name: "TripObjectiveId",
                table: "Photos",
                newName: "TripId");

            migrationBuilder.RenameIndex(
                name: "IX_Photos_TripObjectiveId",
                table: "Photos",
                newName: "IX_Photos_TripId");

            migrationBuilder.RenameColumn(
                name: "TripObjectiveId",
                table: "Lead",
                newName: "TripId");

            migrationBuilder.RenameIndex(
                name: "IX_Lead_TripObjectiveId",
                table: "Lead",
                newName: "IX_Lead_TripId");

            migrationBuilder.CreateTable(
                name: "Trips",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    ProjectId = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    TripReport = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TagId = table.Column<string>(type: "nvarchar(10)", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedByName = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Trips", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Trips_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Trips_Tags_TagId",
                        column: x => x.TagId,
                        principalTable: "Tags",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TripMembers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    TripId = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedByName = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TripMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TripMembers_Trips_TripId",
                        column: x => x.TripId,
                        principalTable: "Trips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TripMembers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TripTags",
                columns: table => new
                {
                    TagId = table.Column<string>(type: "nvarchar(10)", nullable: false),
                    TripId = table.Column<string>(type: "nvarchar(10)", nullable: false),
                    Id = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedByName = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TripTags", x => new { x.TagId, x.TripId });
                    table.ForeignKey(
                        name: "FK_TripTags_Tags_TagId",
                        column: x => x.TagId,
                        principalTable: "Tags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TripTags_Trips_TripId",
                        column: x => x.TripId,
                        principalTable: "Trips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TripMembers_TripId",
                table: "TripMembers",
                column: "TripId");

            migrationBuilder.CreateIndex(
                name: "IX_TripMembers_UserId",
                table: "TripMembers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Trips_ProjectId",
                table: "Trips",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Trips_TagId",
                table: "Trips",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "IX_TripTags_TripId",
                table: "TripTags",
                column: "TripId");

            migrationBuilder.AddForeignKey(
                name: "FK_Lead_Trips_TripId",
                table: "Lead",
                column: "TripId",
                principalTable: "Trips",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Photos_Trips_TripId",
                table: "Photos",
                column: "TripId",
                principalTable: "Trips",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Lead_Trips_TripId",
                table: "Lead");

            migrationBuilder.DropForeignKey(
                name: "FK_Photos_Trips_TripId",
                table: "Photos");

            migrationBuilder.DropTable(
                name: "TripMembers");

            migrationBuilder.DropTable(
                name: "TripTags");

            migrationBuilder.DropTable(
                name: "Trips");

            migrationBuilder.RenameColumn(
                name: "TripId",
                table: "Photos",
                newName: "TripObjectiveId");

            migrationBuilder.RenameIndex(
                name: "IX_Photos_TripId",
                table: "Photos",
                newName: "IX_Photos_TripObjectiveId");

            migrationBuilder.RenameColumn(
                name: "TripId",
                table: "Lead",
                newName: "TripObjectiveId");

            migrationBuilder.RenameIndex(
                name: "IX_Lead_TripId",
                table: "Lead",
                newName: "IX_Lead_TripObjectiveId");

            migrationBuilder.CreateTable(
                name: "TripObjectives",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    ProjectId = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CreatedByName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ModifiedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TagId = table.Column<string>(type: "nvarchar(10)", nullable: true),
                    TripReport = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TripObjectives", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TripObjectives_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TripObjectives_Tags_TagId",
                        column: x => x.TagId,
                        principalTable: "Tags",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TripObjectiveMembers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    TripObjectiveId = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CreatedByName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedOn = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TripObjectiveMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TripObjectiveMembers_TripObjectives_TripObjectiveId",
                        column: x => x.TripObjectiveId,
                        principalTable: "TripObjectives",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TripObjectiveMembers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TripObjectiveTag",
                columns: table => new
                {
                    TagId = table.Column<string>(type: "nvarchar(10)", nullable: false),
                    TripObjectiveId = table.Column<string>(type: "nvarchar(10)", nullable: false),
                    CreatedByName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Id = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    ModifiedOn = table.Column<DateTime>(type: "datetime2", nullable: true)
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
                name: "IX_TripObjectiveMembers_TripObjectiveId",
                table: "TripObjectiveMembers",
                column: "TripObjectiveId");

            migrationBuilder.CreateIndex(
                name: "IX_TripObjectiveMembers_UserId",
                table: "TripObjectiveMembers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_TripObjectives_ProjectId",
                table: "TripObjectives",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_TripObjectives_TagId",
                table: "TripObjectives",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "IX_TripObjectiveTag_TripObjectiveId",
                table: "TripObjectiveTag",
                column: "TripObjectiveId");

            migrationBuilder.AddForeignKey(
                name: "FK_Lead_TripObjectives_TripObjectiveId",
                table: "Lead",
                column: "TripObjectiveId",
                principalTable: "TripObjectives",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Photos_TripObjectives_TripObjectiveId",
                table: "Photos",
                column: "TripObjectiveId",
                principalTable: "TripObjectives",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
