using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace Planarian.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class v1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:postgis", ",,");

            migrationBuilder.CreateTable(
                name: "Accounts",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ModifiedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Accounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    FirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EmailAddress = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    PhoneNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    HashedPassword = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PasswordResetCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    EmailConfirmationCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    EmailConfirmedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PasswordResetCodeExpiration = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ProfilePhotoBlobKey = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    CreatedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ModifiedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AccountUsers",
                columns: table => new
                {
                    AccountId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    UserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CreatedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ModifiedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
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
                name: "MessageLogs",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    MessageKey = table.Column<string>(type: "text", nullable: false),
                    MessageType = table.Column<string>(type: "text", nullable: false),
                    Subject = table.Column<string>(type: "text", nullable: false),
                    ToEmailAddress = table.Column<string>(type: "text", nullable: false),
                    ToName = table.Column<string>(type: "text", nullable: false),
                    FromName = table.Column<string>(type: "text", nullable: false),
                    FromEmailAddress = table.Column<string>(type: "text", nullable: false),
                    Substitutions = table.Column<string>(type: "text", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ModifiedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MessageLogs_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MessageLogs_Users_ModifiedByUserId",
                        column: x => x.ModifiedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "MessageTypes",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    Subject = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    FromName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FromEmail = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Mjml = table.Column<string>(type: "text", nullable: true),
                    Html = table.Column<string>(type: "text", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ModifiedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MessageTypes_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MessageTypes_Users_ModifiedByUserId",
                        column: x => x.ModifiedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Projects",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ModifiedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Projects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Projects_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Projects_Users_ModifiedByUserId",
                        column: x => x.ModifiedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "States",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Abbreviation = table.Column<string>(type: "text", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ModifiedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
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
                name: "TagTypes",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ProjectId = table.Column<string>(type: "character varying(10)", nullable: true),
                    AccountId = table.Column<string>(type: "character varying(10)", nullable: true),
                    Key = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ModifiedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TagTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TagTypes_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TagTypes_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "AccountStates",
                columns: table => new
                {
                    AccountId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    StateId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CreatedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ModifiedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
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
                    Id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    AccountId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    StateId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    DisplayId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ModifiedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
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
                name: "Trips",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ProjectId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    TripReport = table.Column<string>(type: "text", nullable: true),
                    TagTypeId = table.Column<string>(type: "character varying(10)", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ModifiedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
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
                        name: "FK_Trips_TagTypes_TagTypeId",
                        column: x => x.TagTypeId,
                        principalTable: "TagTypes",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Trips_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Trips_Users_ModifiedByUserId",
                        column: x => x.ModifiedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Caves",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    AccountId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    StateId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ReportedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CountyId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CountyNumber = table.Column<int>(type: "integer", nullable: false),
                    LengthFeet = table.Column<double>(type: "double precision", nullable: false),
                    DepthFeet = table.Column<double>(type: "double precision", nullable: false),
                    MaxPitDepthFeet = table.Column<double>(type: "double precision", nullable: false),
                    NumberOfPits = table.Column<int>(type: "integer", nullable: false),
                    Narrative = table.Column<string>(type: "text", nullable: true),
                    ReportedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReportedByName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsArchived = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ModifiedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Caves", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Caves_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Caves_Counties_CountyId",
                        column: x => x.CountyId,
                        principalTable: "Counties",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Caves_States_StateId",
                        column: x => x.StateId,
                        principalTable: "States",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Caves_Users_ReportedByUserId",
                        column: x => x.ReportedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Leads",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    TripId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Description = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ClosestStation = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Classification = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DateClosed = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ModifiedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Leads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Leads_Trips_TripId",
                        column: x => x.TripId,
                        principalTable: "Trips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Leads_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Leads_Users_ModifiedByUserId",
                        column: x => x.ModifiedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Members",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ProjectId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    TripId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    UserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CreatedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ModifiedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Members", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Members_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Members_Trips_TripId",
                        column: x => x.TripId,
                        principalTable: "Trips",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Members_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Photos",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    TripId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    FileType = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    BlobKey = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    CreatedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ModifiedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Photos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Photos_Trips_TripId",
                        column: x => x.TripId,
                        principalTable: "Trips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TripTags",
                columns: table => new
                {
                    TagTypeId = table.Column<string>(type: "character varying(10)", nullable: false),
                    TripId = table.Column<string>(type: "character varying(10)", nullable: false),
                    Id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CreatedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ModifiedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TripTags", x => new { x.TagTypeId, x.TripId });
                    table.ForeignKey(
                        name: "FK_TripTags_TagTypes_TagTypeId",
                        column: x => x.TagTypeId,
                        principalTable: "TagTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TripTags_Trips_TripId",
                        column: x => x.TripId,
                        principalTable: "Trips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Entrances",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ReportedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CaveId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    LocationQualityTagId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Location = table.Column<Point>(type: "geometry", nullable: false),
                    ReportedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReportedByName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PitFeet = table.Column<double>(type: "double precision", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ModifiedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Entrances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Entrances_Caves_CaveId",
                        column: x => x.CaveId,
                        principalTable: "Caves",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Entrances_TagTypes_LocationQualityTagId",
                        column: x => x.LocationQualityTagId,
                        principalTable: "TagTypes",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Entrances_Users_ReportedByUserId",
                        column: x => x.ReportedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Files",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    FileTypeTagId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CaveId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    AccountId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    BlobKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    BlobContainer = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    FileName = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ExpiresOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ModifiedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Files", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Files_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Files_Caves_CaveId",
                        column: x => x.CaveId,
                        principalTable: "Caves",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Files_TagTypes_FileTypeTagId",
                        column: x => x.FileTypeTagId,
                        principalTable: "TagTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GeologyTags",
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
                    table.PrimaryKey("PK_GeologyTags", x => new { x.TagTypeId, x.CaveId });
                    table.ForeignKey(
                        name: "FK_GeologyTags_Caves_CaveId",
                        column: x => x.CaveId,
                        principalTable: "Caves",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GeologyTags_TagTypes_TagTypeId",
                        column: x => x.TagTypeId,
                        principalTable: "TagTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
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
                name: "LeadTags",
                columns: table => new
                {
                    TagTypeId = table.Column<string>(type: "character varying(10)", nullable: false),
                    LeadId = table.Column<string>(type: "character varying(10)", nullable: false),
                    Id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CreatedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ModifiedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeadTags", x => new { x.TagTypeId, x.LeadId });
                    table.ForeignKey(
                        name: "FK_LeadTags_Leads_LeadId",
                        column: x => x.LeadId,
                        principalTable: "Leads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LeadTags_TagTypes_TagTypeId",
                        column: x => x.TagTypeId,
                        principalTable: "TagTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LeadTags_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_LeadTags_Users_ModifiedByUserId",
                        column: x => x.ModifiedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "EntranceHydrologyFrequencyTags",
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

            migrationBuilder.CreateTable(
                name: "EntranceHydrologyTags",
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
                    table.PrimaryKey("PK_EntranceHydrologyTags", x => new { x.TagTypeId, x.EntranceId });
                    table.ForeignKey(
                        name: "FK_EntranceHydrologyTags_Entrances_EntranceId",
                        column: x => x.EntranceId,
                        principalTable: "Entrances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EntranceHydrologyTags_TagTypes_TagTypeId",
                        column: x => x.TagTypeId,
                        principalTable: "TagTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
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
                    table.PrimaryKey("PK_EntranceStatusTags", x => new { x.TagTypeId, x.EntranceId });
                    table.ForeignKey(
                        name: "FK_EntranceStatusTags_Entrances_EntranceId",
                        column: x => x.EntranceId,
                        principalTable: "Entrances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EntranceStatusTags_TagTypes_TagTypeId",
                        column: x => x.TagTypeId,
                        principalTable: "TagTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
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
                    table.PrimaryKey("PK_FieldIndicationTags", x => new { x.TagTypeId, x.EntranceId });
                    table.ForeignKey(
                        name: "FK_FieldIndicationTags_Entrances_EntranceId",
                        column: x => x.EntranceId,
                        principalTable: "Entrances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FieldIndicationTags_TagTypes_TagTypeId",
                        column: x => x.TagTypeId,
                        principalTable: "TagTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
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
                name: "IX_Caves_CountyNumber",
                table: "Caves",
                column: "CountyNumber");

            migrationBuilder.CreateIndex(
                name: "IX_Caves_CountyNumber_CountyId",
                table: "Caves",
                columns: new[] { "CountyNumber", "CountyId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Caves_DepthFeet",
                table: "Caves",
                column: "DepthFeet");

            migrationBuilder.CreateIndex(
                name: "IX_Caves_LengthFeet",
                table: "Caves",
                column: "LengthFeet");

            migrationBuilder.CreateIndex(
                name: "IX_Caves_ReportedByUserId",
                table: "Caves",
                column: "ReportedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Caves_StateId",
                table: "Caves",
                column: "StateId");

            migrationBuilder.CreateIndex(
                name: "IX_Counties_AccountId_DisplayId",
                table: "Counties",
                columns: new[] { "AccountId", "DisplayId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Counties_DisplayId",
                table: "Counties",
                column: "DisplayId");

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
                name: "IX_Files_AccountId",
                table: "Files",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Files_CaveId",
                table: "Files",
                column: "CaveId");

            migrationBuilder.CreateIndex(
                name: "IX_Files_FileTypeTagId",
                table: "Files",
                column: "FileTypeTagId");

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
                name: "IX_Leads_CreatedByUserId",
                table: "Leads",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Leads_ModifiedByUserId",
                table: "Leads",
                column: "ModifiedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Leads_TripId",
                table: "Leads",
                column: "TripId");

            migrationBuilder.CreateIndex(
                name: "IX_LeadTags_CreatedByUserId",
                table: "LeadTags",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LeadTags_LeadId",
                table: "LeadTags",
                column: "LeadId");

            migrationBuilder.CreateIndex(
                name: "IX_LeadTags_ModifiedByUserId",
                table: "LeadTags",
                column: "ModifiedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Members_ProjectId",
                table: "Members",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Members_TripId",
                table: "Members",
                column: "TripId");

            migrationBuilder.CreateIndex(
                name: "IX_Members_UserId",
                table: "Members",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageLogs_CreatedByUserId",
                table: "MessageLogs",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageLogs_ModifiedByUserId",
                table: "MessageLogs",
                column: "ModifiedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageTypes_CreatedByUserId",
                table: "MessageTypes",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageTypes_ModifiedByUserId",
                table: "MessageTypes",
                column: "ModifiedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Photos_TripId",
                table: "Photos",
                column: "TripId");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_CreatedByUserId",
                table: "Projects",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_ModifiedByUserId",
                table: "Projects",
                column: "ModifiedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_States_CreatedByUserId",
                table: "States",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_States_ModifiedByUserId",
                table: "States",
                column: "ModifiedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TagTypes_AccountId",
                table: "TagTypes",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_TagTypes_Key",
                table: "TagTypes",
                column: "Key");

            migrationBuilder.CreateIndex(
                name: "IX_TagTypes_ProjectId",
                table: "TagTypes",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Trips_CreatedByUserId",
                table: "Trips",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Trips_ModifiedByUserId",
                table: "Trips",
                column: "ModifiedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Trips_ProjectId",
                table: "Trips",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Trips_TagTypeId",
                table: "Trips",
                column: "TagTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_TripTags_TripId",
                table: "TripTags",
                column: "TripId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_EmailAddress",
                table: "Users",
                column: "EmailAddress",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
                name: "Files");

            migrationBuilder.DropTable(
                name: "GeologyTags");

            migrationBuilder.DropTable(
                name: "LeadTags");

            migrationBuilder.DropTable(
                name: "Members");

            migrationBuilder.DropTable(
                name: "MessageLogs");

            migrationBuilder.DropTable(
                name: "MessageTypes");

            migrationBuilder.DropTable(
                name: "Photos");

            migrationBuilder.DropTable(
                name: "TripTags");

            migrationBuilder.DropTable(
                name: "Entrances");

            migrationBuilder.DropTable(
                name: "Leads");

            migrationBuilder.DropTable(
                name: "Caves");

            migrationBuilder.DropTable(
                name: "Trips");

            migrationBuilder.DropTable(
                name: "Counties");

            migrationBuilder.DropTable(
                name: "TagTypes");

            migrationBuilder.DropTable(
                name: "States");

            migrationBuilder.DropTable(
                name: "Accounts");

            migrationBuilder.DropTable(
                name: "Projects");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
