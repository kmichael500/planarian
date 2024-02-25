using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planarian.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class v3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EntranceHydrologyFrequencyTags");

            migrationBuilder.DropColumn(
                name: "ReportedByName",
                table: "Entrances");

            migrationBuilder.DropColumn(
                name: "ReportedByName",
                table: "Caves");

            migrationBuilder.RenameColumn(
                name: "PitFeet",
                table: "Entrances",
                newName: "PitDepthFeet");

            migrationBuilder.CreateTable(
                name: "ArcheologyTag",
                columns: table => new
                {
                    TagTypeId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CaveId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CreatedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ModifiedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArcheologyTag", x => new { x.TagTypeId, x.CaveId });
                    table.ForeignKey(
                        name: "FK_ArcheologyTag_Caves_CaveId",
                        column: x => x.CaveId,
                        principalTable: "Caves",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ArcheologyTag_TagTypes_TagTypeId",
                        column: x => x.TagTypeId,
                        principalTable: "TagTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ArcheologyTag_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ArcheologyTag_Users_ModifiedByUserId",
                        column: x => x.ModifiedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "BiologyTag",
                columns: table => new
                {
                    TagTypeId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CaveId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CreatedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ModifiedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BiologyTag", x => new { x.TagTypeId, x.CaveId });
                    table.ForeignKey(
                        name: "FK_BiologyTag_Caves_CaveId",
                        column: x => x.CaveId,
                        principalTable: "Caves",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BiologyTag_TagTypes_TagTypeId",
                        column: x => x.TagTypeId,
                        principalTable: "TagTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BiologyTag_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_BiologyTag_Users_ModifiedByUserId",
                        column: x => x.ModifiedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "CaveAlternateNameTag",
                columns: table => new
                {
                    TagTypeId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CaveId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CreatedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ModifiedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
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

            migrationBuilder.CreateTable(
                name: "EntranceTypeTag",
                columns: table => new
                {
                    TagTypeId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    EntranceId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CreatedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ModifiedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EntranceTypeTag", x => new { x.TagTypeId, x.EntranceId });
                    table.ForeignKey(
                        name: "FK_EntranceTypeTag_Entrances_EntranceId",
                        column: x => x.EntranceId,
                        principalTable: "Entrances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EntranceTypeTag_TagTypes_TagTypeId",
                        column: x => x.TagTypeId,
                        principalTable: "TagTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EntranceTypeTag_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EntranceTypeTag_Users_ModifiedByUserId",
                        column: x => x.ModifiedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "GeologicAgeTag",
                columns: table => new
                {
                    TagTypeId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CaveId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CreatedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ModifiedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GeologicAgeTag", x => new { x.TagTypeId, x.CaveId });
                    table.ForeignKey(
                        name: "FK_GeologicAgeTag_Caves_CaveId",
                        column: x => x.CaveId,
                        principalTable: "Caves",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GeologicAgeTag_TagTypes_TagTypeId",
                        column: x => x.TagTypeId,
                        principalTable: "TagTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GeologicAgeTag_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_GeologicAgeTag_Users_ModifiedByUserId",
                        column: x => x.ModifiedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "MapStatusTag",
                columns: table => new
                {
                    TagTypeId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CaveId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CreatedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ModifiedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MapStatusTag", x => new { x.TagTypeId, x.CaveId });
                    table.ForeignKey(
                        name: "FK_MapStatusTag_Caves_CaveId",
                        column: x => x.CaveId,
                        principalTable: "Caves",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MapStatusTag_TagTypes_TagTypeId",
                        column: x => x.TagTypeId,
                        principalTable: "TagTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MapStatusTag_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MapStatusTag_Users_ModifiedByUserId",
                        column: x => x.ModifiedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "OtherTag",
                columns: table => new
                {
                    TagTypeId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CaveId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CreatedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ModifiedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OtherTag", x => new { x.TagTypeId, x.CaveId });
                    table.ForeignKey(
                        name: "FK_OtherTag_Caves_CaveId",
                        column: x => x.CaveId,
                        principalTable: "Caves",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OtherTag_TagTypes_TagTypeId",
                        column: x => x.TagTypeId,
                        principalTable: "TagTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OtherTag_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_OtherTag_Users_ModifiedByUserId",
                        column: x => x.ModifiedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PeopleTag",
                columns: table => new
                {
                    TagTypeId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CreatedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ModifiedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PeopleTag", x => x.TagTypeId);
                    table.ForeignKey(
                        name: "FK_PeopleTag_TagTypes_TagTypeId",
                        column: x => x.TagTypeId,
                        principalTable: "TagTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PeopleTag_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PeopleTag_Users_ModifiedByUserId",
                        column: x => x.ModifiedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PhysiographicProvinceTag",
                columns: table => new
                {
                    TagTypeId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CaveId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CreatedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ModifiedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhysiographicProvinceTag", x => new { x.TagTypeId, x.CaveId });
                    table.ForeignKey(
                        name: "FK_PhysiographicProvinceTag_Caves_CaveId",
                        column: x => x.CaveId,
                        principalTable: "Caves",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PhysiographicProvinceTag_TagTypes_TagTypeId",
                        column: x => x.TagTypeId,
                        principalTable: "TagTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PhysiographicProvinceTag_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PhysiographicProvinceTag_Users_ModifiedByUserId",
                        column: x => x.ModifiedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "CartographerNameTag",
                columns: table => new
                {
                    PeopleTagId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CaveId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CreatedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ModifiedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CartographerNameTag", x => new { x.PeopleTagId, x.CaveId });
                    table.ForeignKey(
                        name: "FK_CartographerNameTag_Caves_CaveId",
                        column: x => x.CaveId,
                        principalTable: "Caves",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CartographerNameTag_PeopleTag_PeopleTagId",
                        column: x => x.PeopleTagId,
                        principalTable: "PeopleTag",
                        principalColumn: "TagTypeId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CartographerNameTag_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CartographerNameTag_Users_ModifiedByUserId",
                        column: x => x.ModifiedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "CaveReportedByNameTag",
                columns: table => new
                {
                    PeopleTagId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CaveId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CreatedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ModifiedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaveReportedByNameTag", x => new { x.PeopleTagId, x.CaveId });
                    table.ForeignKey(
                        name: "FK_CaveReportedByNameTag_Caves_CaveId",
                        column: x => x.CaveId,
                        principalTable: "Caves",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CaveReportedByNameTag_PeopleTag_PeopleTagId",
                        column: x => x.PeopleTagId,
                        principalTable: "PeopleTag",
                        principalColumn: "TagTypeId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CaveReportedByNameTag_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CaveReportedByNameTag_Users_ModifiedByUserId",
                        column: x => x.ModifiedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "EntranceReportedByNameTag",
                columns: table => new
                {
                    PeopleTagId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CaveId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    EntranceId = table.Column<string>(type: "character varying(10)", nullable: true),
                    Id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CreatedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ModifiedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EntranceReportedByNameTag", x => new { x.PeopleTagId, x.CaveId });
                    table.ForeignKey(
                        name: "FK_EntranceReportedByNameTag_Caves_CaveId",
                        column: x => x.CaveId,
                        principalTable: "Caves",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EntranceReportedByNameTag_Entrances_EntranceId",
                        column: x => x.EntranceId,
                        principalTable: "Entrances",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EntranceReportedByNameTag_PeopleTag_PeopleTagId",
                        column: x => x.PeopleTagId,
                        principalTable: "PeopleTag",
                        principalColumn: "TagTypeId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EntranceReportedByNameTag_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EntranceReportedByNameTag_Users_ModifiedByUserId",
                        column: x => x.ModifiedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArcheologyTag_CaveId",
                table: "ArcheologyTag",
                column: "CaveId");

            migrationBuilder.CreateIndex(
                name: "IX_ArcheologyTag_CreatedByUserId",
                table: "ArcheologyTag",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ArcheologyTag_ModifiedByUserId",
                table: "ArcheologyTag",
                column: "ModifiedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_BiologyTag_CaveId",
                table: "BiologyTag",
                column: "CaveId");

            migrationBuilder.CreateIndex(
                name: "IX_BiologyTag_CreatedByUserId",
                table: "BiologyTag",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_BiologyTag_ModifiedByUserId",
                table: "BiologyTag",
                column: "ModifiedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CartographerNameTag_CaveId",
                table: "CartographerNameTag",
                column: "CaveId");

            migrationBuilder.CreateIndex(
                name: "IX_CartographerNameTag_CreatedByUserId",
                table: "CartographerNameTag",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CartographerNameTag_ModifiedByUserId",
                table: "CartographerNameTag",
                column: "ModifiedByUserId");

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

            migrationBuilder.CreateIndex(
                name: "IX_CaveReportedByNameTag_CaveId",
                table: "CaveReportedByNameTag",
                column: "CaveId");

            migrationBuilder.CreateIndex(
                name: "IX_CaveReportedByNameTag_CreatedByUserId",
                table: "CaveReportedByNameTag",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CaveReportedByNameTag_ModifiedByUserId",
                table: "CaveReportedByNameTag",
                column: "ModifiedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EntranceReportedByNameTag_CaveId",
                table: "EntranceReportedByNameTag",
                column: "CaveId");

            migrationBuilder.CreateIndex(
                name: "IX_EntranceReportedByNameTag_CreatedByUserId",
                table: "EntranceReportedByNameTag",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EntranceReportedByNameTag_EntranceId",
                table: "EntranceReportedByNameTag",
                column: "EntranceId");

            migrationBuilder.CreateIndex(
                name: "IX_EntranceReportedByNameTag_ModifiedByUserId",
                table: "EntranceReportedByNameTag",
                column: "ModifiedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EntranceTypeTag_CreatedByUserId",
                table: "EntranceTypeTag",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EntranceTypeTag_EntranceId",
                table: "EntranceTypeTag",
                column: "EntranceId");

            migrationBuilder.CreateIndex(
                name: "IX_EntranceTypeTag_ModifiedByUserId",
                table: "EntranceTypeTag",
                column: "ModifiedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_GeologicAgeTag_CaveId",
                table: "GeologicAgeTag",
                column: "CaveId");

            migrationBuilder.CreateIndex(
                name: "IX_GeologicAgeTag_CreatedByUserId",
                table: "GeologicAgeTag",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_GeologicAgeTag_ModifiedByUserId",
                table: "GeologicAgeTag",
                column: "ModifiedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MapStatusTag_CaveId",
                table: "MapStatusTag",
                column: "CaveId");

            migrationBuilder.CreateIndex(
                name: "IX_MapStatusTag_CreatedByUserId",
                table: "MapStatusTag",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MapStatusTag_ModifiedByUserId",
                table: "MapStatusTag",
                column: "ModifiedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OtherTag_CaveId",
                table: "OtherTag",
                column: "CaveId");

            migrationBuilder.CreateIndex(
                name: "IX_OtherTag_CreatedByUserId",
                table: "OtherTag",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OtherTag_ModifiedByUserId",
                table: "OtherTag",
                column: "ModifiedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PeopleTag_CreatedByUserId",
                table: "PeopleTag",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PeopleTag_ModifiedByUserId",
                table: "PeopleTag",
                column: "ModifiedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PhysiographicProvinceTag_CaveId",
                table: "PhysiographicProvinceTag",
                column: "CaveId");

            migrationBuilder.CreateIndex(
                name: "IX_PhysiographicProvinceTag_CreatedByUserId",
                table: "PhysiographicProvinceTag",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PhysiographicProvinceTag_ModifiedByUserId",
                table: "PhysiographicProvinceTag",
                column: "ModifiedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArcheologyTag");

            migrationBuilder.DropTable(
                name: "BiologyTag");

            migrationBuilder.DropTable(
                name: "CartographerNameTag");

            migrationBuilder.DropTable(
                name: "CaveAlternateNameTag");

            migrationBuilder.DropTable(
                name: "CaveReportedByNameTag");

            migrationBuilder.DropTable(
                name: "EntranceReportedByNameTag");

            migrationBuilder.DropTable(
                name: "EntranceTypeTag");

            migrationBuilder.DropTable(
                name: "GeologicAgeTag");

            migrationBuilder.DropTable(
                name: "MapStatusTag");

            migrationBuilder.DropTable(
                name: "OtherTag");

            migrationBuilder.DropTable(
                name: "PhysiographicProvinceTag");

            migrationBuilder.DropTable(
                name: "PeopleTag");

            migrationBuilder.RenameColumn(
                name: "PitDepthFeet",
                table: "Entrances",
                newName: "PitFeet");

            migrationBuilder.AddColumn<string>(
                name: "ReportedByName",
                table: "Entrances",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReportedByName",
                table: "Caves",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EntranceHydrologyFrequencyTags",
                columns: table => new
                {
                    TagTypeId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    EntranceId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CreatedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ModifiedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ModifiedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EntranceHydrologyFrequencyTags", x => new { x.TagTypeId, x.EntranceId });
                    table.ForeignKey(
                        name: "FK_EntranceHydrologyFrequencyTags_Entrances_EntranceId",
                        column: x => x.EntranceId,
                        principalTable: "Entrances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EntranceHydrologyFrequencyTags_TagTypes_TagTypeId",
                        column: x => x.TagTypeId,
                        principalTable: "TagTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EntranceHydrologyFrequencyTags_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EntranceHydrologyFrequencyTags_Users_ModifiedByUserId",
                        column: x => x.ModifiedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_EntranceHydrologyFrequencyTags_CreatedByUserId",
                table: "EntranceHydrologyFrequencyTags",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EntranceHydrologyFrequencyTags_EntranceId",
                table: "EntranceHydrologyFrequencyTags",
                column: "EntranceId");

            migrationBuilder.CreateIndex(
                name: "IX_EntranceHydrologyFrequencyTags_ModifiedByUserId",
                table: "EntranceHydrologyFrequencyTags",
                column: "ModifiedByUserId");
        }
    }
}
