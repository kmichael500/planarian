﻿using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planarian.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class v9 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FeatureSettings",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    AccountId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedByUserId =
                        table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ModifiedByUserId =
                        table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeatureSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FeatureSettings_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_FeatureSettings_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_FeatureSettings_Users_ModifiedByUserId",
                        column: x => x.ModifiedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_FeatureSettings_AccountId",
                table: "FeatureSettings",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_FeatureSettings_CreatedByUserId",
                table: "FeatureSettings",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FeatureSettings_ModifiedByUserId",
                table: "FeatureSettings",
                column: "ModifiedByUserId");

            migrationBuilder.Sql(@"
                INSERT INTO ""FeatureSettings"" (""Id"", ""AccountId"", ""Key"", ""IsEnabled"", ""IsDefault"", ""CreatedOn"")
                SELECT
                    substr(md5(random()::text || clock_timestamp()::text), 1, 10),
                    a.  ""Id"",
                    key.""Key"",
                    true,
                    false,
                    NOW()
                FROM
                    ""Accounts"" a,
                    (VALUES
                        ('EnabledFieldCaveAlternateNames'),
                        ('EnabledFieldCaveLengthFeet'),
                        ('EnabledFieldCaveDepthFeet'),
                        ('EnabledFieldCaveMaxPitDepthFeet'),
                        ('EnabledFieldCaveNumberOfPits'),
                        ('EnabledFieldCaveReportedOn'),
                        ('EnabledFieldCaveReportedByNameTags'),
                        ('EnabledFieldCaveGeologyTags'),
                        ('EnabledFieldCaveGeologicAgeTags'),
                        ('EnabledFieldCavePhysiographicProvinceTags'),
                        ('EnabledFieldCaveBiologyTags'),
                        ('EnabledFieldCaveArcheologyTags'),
                        ('EnabledFieldCaveMapStatusTags'),
                        ('EnabledFieldCaveCartographerNameTags'),
                        ('EnabledFieldCaveOtherTags'),
                        ('EnabledFieldCaveNarrative'),
                        ('EnabledFieldEntranceName'),
                        ('EnabledFieldEntranceReportedOn'),
                        ('EnabledFieldEntranceReportedByNameTags'),
                        ('EnabledFieldEntrancePitDepth'),
                        ('EnabledFieldEntranceStatusTags'),
                        ('EnabledFieldEntranceFieldIndicationTags'),
                        ('EnabledFieldEntranceHydrologyTags'),
                        ('EnabledFieldEntranceDescription')
                    ) AS key(""Key"");
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FeatureSettings");
        }
    }
}