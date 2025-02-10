using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planarian.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class v12 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE ""Users"" 
                SET ""PasswordResetCode"" = NULL 
                WHERE LENGTH(""PasswordResetCode"") > 10;
                
                UPDATE ""Users"" 
                SET ""EmailConfirmationCode"" = NULL 
                WHERE LENGTH(""EmailConfirmationCode"") > 10;

                UPDATE ""AccountUsers"" 
                SET ""InvitationCode"" = NULL 
                WHERE LENGTH(""InvitationCode"") > 10;
            ");
            migrationBuilder.AlterColumn<string>(
                name: "PasswordResetCode",
                table: "Users",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "EmailConfirmationCode",
                table: "Users",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "InvitationCode",
                table: "AccountUsers",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "CavePermission",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    UserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    AccountId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CountyId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CaveId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CreatedByUserId =
                        table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ModifiedByUserId =
                        table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CavePermission", x => x.Id);
                    table.CheckConstraint("CK_CavePermission_CountyOrCave",
                        "\"CountyId\" IS NULL OR \"CaveId\" IS NULL");
                    table.ForeignKey(
                        name: "FK_CavePermission_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CavePermission_Caves_CaveId",
                        column: x => x.CaveId,
                        principalTable: "Caves",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CavePermission_Counties_CountyId",
                        column: x => x.CountyId,
                        principalTable: "Counties",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CavePermission_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CavePermission_Users_ModifiedByUserId",
                        column: x => x.ModifiedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CavePermission_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Permissions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    IsHidden = table.Column<bool>(type: "boolean", nullable: false),
                    Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CavePermissionId = table.Column<string>(type: "character varying(10)", nullable: true),
                    CreatedByUserId =
                        table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ModifiedByUserId =
                        table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Permissions_CavePermission_CavePermissionId",
                        column: x => x.CavePermissionId,
                        principalTable: "CavePermission",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Permissions_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Permissions_Users_ModifiedByUserId",
                        column: x => x.ModifiedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "CavePermissionPermissions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CavePermissionId =
                        table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    PermissionId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CreatedByUserId =
                        table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ModifiedByUserId =
                        table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CavePermissionPermissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CavePermissionPermissions_CavePermission_CavePermissionId",
                        column: x => x.CavePermissionId,
                        principalTable: "CavePermission",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CavePermissionPermissions_Permissions_PermissionId",
                        column: x => x.PermissionId,
                        principalTable: "Permissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CavePermission_AccountId",
                table: "CavePermission",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_CavePermission_CaveId",
                table: "CavePermission",
                column: "CaveId");

            migrationBuilder.CreateIndex(
                name: "IX_CavePermission_CountyId",
                table: "CavePermission",
                column: "CountyId");

            migrationBuilder.CreateIndex(
                name: "IX_CavePermission_CreatedByUserId",
                table: "CavePermission",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CavePermission_ModifiedByUserId",
                table: "CavePermission",
                column: "ModifiedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CavePermission_UserId_AccountId",
                table: "CavePermission",
                columns: new[] { "UserId", "AccountId" },
                unique: true,
                filter: "\"CountyId\" IS NULL AND \"CaveId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CavePermission_UserId_AccountId_CaveId",
                table: "CavePermission",
                columns: new[] { "UserId", "AccountId", "CaveId" },
                unique: true,
                filter: "\"CaveId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CavePermission_UserId_AccountId_CountyId",
                table: "CavePermission",
                columns: new[] { "UserId", "AccountId", "CountyId" },
                unique: true,
                filter: "\"CountyId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CavePermissionPermissions_CavePermissionId",
                table: "CavePermissionPermissions",
                column: "CavePermissionId");

            migrationBuilder.CreateIndex(
                name: "IX_CavePermissionPermissions_PermissionId",
                table: "CavePermissionPermissions",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_CavePermissionId",
                table: "Permissions",
                column: "CavePermissionId");

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_CreatedByUserId",
                table: "Permissions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_ModifiedByUserId",
                table: "Permissions",
                column: "ModifiedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CavePermissionPermissions");

            migrationBuilder.DropTable(
                name: "Permissions");

            migrationBuilder.DropTable(
                name: "CavePermission");

            migrationBuilder.AlterColumn<string>(
                name: "PasswordResetCode",
                table: "Users",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(10)",
                oldMaxLength: 10,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "EmailConfirmationCode",
                table: "Users",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(10)",
                oldMaxLength: 10,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "InvitationCode",
                table: "AccountUsers",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(10)",
                oldMaxLength: 10,
                oldNullable: true);
        }
    }
}