using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Planarian.Migrations.Generators;
#nullable disable

namespace Planarian.Migrations.Migrations
{
    public partial class v3 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AccountId",
                table: "TagTypes",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Accounts",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    ModifiedByUserId = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedOn = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table => { table.PrimaryKey("PK_Accounts", x => x.Id); });

            migrationBuilder.CreateTable(
                name: "States",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Abbreviation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    ModifiedByUserId = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_States", x => x.Id);
                    table.ForeignKey(
                        name: "FK_States_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_States_Users_ModifiedByUserId",
                        column: x => x.ModifiedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "AccountUsers",
                columns: table => new
                {
                    AccountId = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Id = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    ModifiedByUserId = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedOn = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountUsers", x => new { x.AccountId, x.UserId });
                    table.ForeignKey(
                        name: "FK_AccountUsers_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AccountUsers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "AccountStates",
                columns: table => new
                {
                    AccountId = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    StateId = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Id = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    ModifiedByUserId = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedOn = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountStates", x => new { x.AccountId, x.StateId });
                    table.ForeignKey(
                        name: "FK_AccountStates_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AccountStates_States_StateId",
                        column: x => x.StateId,
                        principalTable: "States",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Counties",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    AccountId = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    StateId = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    DisplayId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    ModifiedByUserId = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Counties", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Counties_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Counties_States_StateId",
                        column: x => x.StateId,
                        principalTable: "States",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Caves",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    AccountId = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    ReportedByUserId = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    PrimaryEntranceId = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CountyId = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CaveNumber = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LengthFeet = table.Column<double>(type: "float", nullable: false),
                    DepthFeet = table.Column<double>(type: "float", nullable: false),
                    MaxPitDepthFeet = table.Column<double>(type: "float", nullable: true),
                    NumberOfPits = table.Column<int>(type: "int", nullable: false),
                    Narrative = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReportedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReportedByName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    ModifiedByUserId = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedOn = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Caves", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Caves_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Caves_Counties_CountyId",
                        column: x => x.CountyId,
                        principalTable: "Counties",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Caves_Users_ReportedByUserId",
                        column: x => x.ReportedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Entrances",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CaveId = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    ReportedByUserId = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    LocationQualityTagId = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Latitude = table.Column<double>(type: "float", nullable: false),
                    Longitude = table.Column<double>(type: "float", nullable: false),
                    ElevationFeet = table.Column<double>(type: "float", nullable: false),
                    ReportedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReportedByName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PitFeet = table.Column<double>(type: "float", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    ModifiedByUserId = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedOn = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Entrances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Entrances_Caves_CaveId",
                        column: x => x.CaveId,
                        principalTable: "Caves",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Entrances_TagTypes_LocationQualityTagId",
                        column: x => x.LocationQualityTagId,
                        principalTable: "TagTypes",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Entrances_Users_ReportedByUserId",
                        column: x => x.ReportedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "GeologyTags",
                columns: table => new
                {
                    TagTypeId = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CaveId = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Id = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    ModifiedByUserId = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedOn = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GeologyTags", x => new { x.TagTypeId, x.CaveId });
                    table.ForeignKey(
                        name: "FK_GeologyTags_Caves_CaveId",
                        column: x => x.CaveId,
                        principalTable: "Caves",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_GeologyTags_TagTypes_TagTypeId",
                        column: x => x.TagTypeId,
                        principalTable: "TagTypes",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_GeologyTags_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_GeologyTags_Users_ModifiedByUserId",
                        column: x => x.ModifiedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Maps",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CaveId = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    MapStatusTagId = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    ModifiedByUserId = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedOn = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Maps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Maps_Caves_CaveId",
                        column: x => x.CaveId,
                        principalTable: "Caves",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Maps_TagTypes_MapStatusTagId",
                        column: x => x.MapStatusTagId,
                        principalTable: "TagTypes",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "EntranceHydrologyFrequencyTags",
                columns: table => new
                {
                    TagTypeId = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    EntranceId = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Id = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    ModifiedByUserId = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedOn = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EntranceHydrologyFrequencyTags", x => new { x.TagTypeId, x.EntranceId });
                    table.ForeignKey(
                        name: "FK_EntranceHydrologyFrequencyTags_Entrances_EntranceId",
                        column: x => x.EntranceId,
                        principalTable: "Entrances",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EntranceHydrologyFrequencyTags_TagTypes_TagTypeId",
                        column: x => x.TagTypeId,
                        principalTable: "TagTypes",
                        principalColumn: "Id");
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

            migrationBuilder.CreateTable(
                name: "EntranceHydrologyTags",
                columns: table => new
                {
                    TagTypeId = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    EntranceId = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Id = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    ModifiedByUserId = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedOn = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EntranceHydrologyTags", x => new { x.TagTypeId, x.EntranceId });
                    table.ForeignKey(
                        name: "FK_EntranceHydrologyTags_Entrances_EntranceId",
                        column: x => x.EntranceId,
                        principalTable: "Entrances",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EntranceHydrologyTags_TagTypes_TagTypeId",
                        column: x => x.TagTypeId,
                        principalTable: "TagTypes",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EntranceHydrologyTags_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EntranceHydrologyTags_Users_ModifiedByUserId",
                        column: x => x.ModifiedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "EntranceStatusTags",
                columns: table => new
                {
                    TagTypeId = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    EntranceId = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Id = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    ModifiedByUserId = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedOn = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EntranceStatusTags", x => new { x.TagTypeId, x.EntranceId });
                    table.ForeignKey(
                        name: "FK_EntranceStatusTags_Entrances_EntranceId",
                        column: x => x.EntranceId,
                        principalTable: "Entrances",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EntranceStatusTags_TagTypes_TagTypeId",
                        column: x => x.TagTypeId,
                        principalTable: "TagTypes",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EntranceStatusTags_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EntranceStatusTags_Users_ModifiedByUserId",
                        column: x => x.ModifiedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "FieldIndicationTags",
                columns: table => new
                {
                    TagTypeId = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    EntranceId = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Id = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    ModifiedByUserId = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedOn = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FieldIndicationTags", x => new { x.TagTypeId, x.EntranceId });
                    table.ForeignKey(
                        name: "FK_FieldIndicationTags_Entrances_EntranceId",
                        column: x => x.EntranceId,
                        principalTable: "Entrances",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_FieldIndicationTags_TagTypes_TagTypeId",
                        column: x => x.TagTypeId,
                        principalTable: "TagTypes",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_FieldIndicationTags_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_FieldIndicationTags_Users_ModifiedByUserId",
                        column: x => x.ModifiedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountStates_StateId",
                table: "AccountStates",
                column: "StateId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountUsers_UserId",
                table: "AccountUsers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Caves_AccountId",
                table: "Caves",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Caves_CountyId",
                table: "Caves",
                column: "CountyId");

            migrationBuilder.CreateIndex(
                name: "IX_Caves_PrimaryEntranceId",
                table: "Caves",
                column: "PrimaryEntranceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Caves_ReportedByUserId",
                table: "Caves",
                column: "ReportedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Counties_AccountId",
                table: "Counties",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Counties_StateId",
                table: "Counties",
                column: "StateId");

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

            migrationBuilder.CreateIndex(
                name: "IX_EntranceHydrologyTags_CreatedByUserId",
                table: "EntranceHydrologyTags",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EntranceHydrologyTags_EntranceId",
                table: "EntranceHydrologyTags",
                column: "EntranceId");

            migrationBuilder.CreateIndex(
                name: "IX_EntranceHydrologyTags_ModifiedByUserId",
                table: "EntranceHydrologyTags",
                column: "ModifiedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Entrances_CaveId",
                table: "Entrances",
                column: "CaveId");

            migrationBuilder.CreateIndex(
                name: "IX_Entrances_LocationQualityTagId",
                table: "Entrances",
                column: "LocationQualityTagId");

            migrationBuilder.CreateIndex(
                name: "IX_Entrances_ReportedByUserId",
                table: "Entrances",
                column: "ReportedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EntranceStatusTags_CreatedByUserId",
                table: "EntranceStatusTags",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EntranceStatusTags_EntranceId",
                table: "EntranceStatusTags",
                column: "EntranceId");

            migrationBuilder.CreateIndex(
                name: "IX_EntranceStatusTags_ModifiedByUserId",
                table: "EntranceStatusTags",
                column: "ModifiedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FieldIndicationTags_CreatedByUserId",
                table: "FieldIndicationTags",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FieldIndicationTags_EntranceId",
                table: "FieldIndicationTags",
                column: "EntranceId");

            migrationBuilder.CreateIndex(
                name: "IX_FieldIndicationTags_ModifiedByUserId",
                table: "FieldIndicationTags",
                column: "ModifiedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_GeologyTags_CaveId",
                table: "GeologyTags",
                column: "CaveId");

            migrationBuilder.CreateIndex(
                name: "IX_GeologyTags_CreatedByUserId",
                table: "GeologyTags",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_GeologyTags_ModifiedByUserId",
                table: "GeologyTags",
                column: "ModifiedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Maps_CaveId",
                table: "Maps",
                column: "CaveId");

            migrationBuilder.CreateIndex(
                name: "IX_Maps_MapStatusTagId",
                table: "Maps",
                column: "MapStatusTagId");

            migrationBuilder.CreateIndex(
                name: "IX_States_CreatedByUserId",
                table: "States",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_States_ModifiedByUserId",
                table: "States",
                column: "ModifiedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Caves_Entrances_PrimaryEntranceId",
                table: "Caves",
                column: "PrimaryEntranceId",
                principalTable: "Entrances",
                principalColumn: "Id");

            var states = StateGenerator.GenerateStates();

            foreach (var state in states)
            {
                migrationBuilder.InsertData(
                    table: "States",
                    columns: new[] { "Id", "Name", "Abbreviation", "CreatedOn" },
                    values: new object[] { state.Id, state.Name, state.Abbreviation, state.CreatedOn });
            }

        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Caves_Accounts_AccountId",
                table: "Caves");

            migrationBuilder.DropForeignKey(
                name: "FK_Counties_Accounts_AccountId",
                table: "Counties");

            migrationBuilder.DropForeignKey(
                name: "FK_Counties_States_StateId",
                table: "Counties");

            migrationBuilder.DropForeignKey(
                name: "FK_Caves_Counties_CountyId",
                table: "Caves");

            migrationBuilder.DropForeignKey(
                name: "FK_Caves_Entrances_PrimaryEntranceId",
                table: "Caves");

            migrationBuilder.DropTable(
                name: "AccountStates");

            migrationBuilder.DropTable(
                name: "AccountUsers");

            migrationBuilder.DropTable(
                name: "EntranceHydrologyFrequencyTags");

            migrationBuilder.DropTable(
                name: "EntranceHydrologyTags");

            migrationBuilder.DropTable(
                name: "EntranceStatusTags");

            migrationBuilder.DropTable(
                name: "FieldIndicationTags");

            migrationBuilder.DropTable(
                name: "GeologyTags");

            migrationBuilder.DropTable(
                name: "Maps");

            migrationBuilder.DropTable(
                name: "Accounts");

            migrationBuilder.DropTable(
                name: "States");

            migrationBuilder.DropTable(
                name: "Counties");

            migrationBuilder.DropTable(
                name: "Entrances");

            migrationBuilder.DropTable(
                name: "Caves");

            migrationBuilder.DropColumn(
                name: "AccountId",
                table: "TagTypes");
        }
    }
}
