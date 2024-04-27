using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planarian.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class v4 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CaveAlternateNameTag");

            migrationBuilder.AddColumn<string>(
                name: "AlternateNames",
                table: "Caves",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AlternateNames",
                table: "Caves");

            migrationBuilder.CreateTable(
                name: "CaveAlternateNameTag",
                columns: table => new
                {
                    TagTypeId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CaveId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CreatedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ModifiedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ModifiedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaveAlternateNameTag", x => new { x.TagTypeId, x.CaveId });
                    table.ForeignKey(
                        name: "FK_CaveAlternateNameTag_Caves_CaveId",
                        column: x => x.CaveId,
                        principalTable: "Caves",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CaveAlternateNameTag_TagTypes_TagTypeId",
                        column: x => x.TagTypeId,
                        principalTable: "TagTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CaveAlternateNameTag_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CaveAlternateNameTag_Users_ModifiedByUserId",
                        column: x => x.ModifiedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_CaveAlternateNameTag_CaveId",
                table: "CaveAlternateNameTag",
                column: "CaveId");

            migrationBuilder.CreateIndex(
                name: "IX_CaveAlternateNameTag_CreatedByUserId",
                table: "CaveAlternateNameTag",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CaveAlternateNameTag_ModifiedByUserId",
                table: "CaveAlternateNameTag",
                column: "ModifiedByUserId");
        }
    }
}
