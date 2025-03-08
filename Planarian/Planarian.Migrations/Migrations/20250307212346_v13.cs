using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planarian.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class v13 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Permissions_CavePermission_CavePermissionId",
                table: "Permissions");

            migrationBuilder.DropTable(
                name: "CavePermissionPermissions");

            migrationBuilder.DropIndex(
                name: "IX_Permissions_CavePermissionId",
                table: "Permissions");

            migrationBuilder.DropIndex(
                name: "IX_CavePermission_UserId_AccountId",
                table: "CavePermission");

            migrationBuilder.DropIndex(
                name: "IX_CavePermission_UserId_AccountId_CaveId",
                table: "CavePermission");

            migrationBuilder.DropIndex(
                name: "IX_CavePermission_UserId_AccountId_CountyId",
                table: "CavePermission");

            migrationBuilder.DropColumn(
                name: "CavePermissionId",
                table: "Permissions");

            migrationBuilder.AddColumn<string>(
                name: "PermissionId",
                table: "CavePermission",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_CavePermission_PermissionId",
                table: "CavePermission",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "IX_CavePermission_UserId_AccountId_CaveId_PermissionId",
                table: "CavePermission",
                columns: new[] { "UserId", "AccountId", "CaveId", "PermissionId" },
                unique: true,
                filter: "\"CaveId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CavePermission_UserId_AccountId_CountyId_PermissionId",
                table: "CavePermission",
                columns: new[] { "UserId", "AccountId", "CountyId", "PermissionId" },
                unique: true,
                filter: "\"CountyId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CavePermission_UserId_AccountId_PermissionId",
                table: "CavePermission",
                columns: new[] { "UserId", "AccountId", "PermissionId" },
                unique: true,
                filter: "\"CountyId\" IS NULL AND \"CaveId\" IS NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_CavePermission_Permissions_PermissionId",
                table: "CavePermission",
                column: "PermissionId",
                principalTable: "Permissions",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CavePermission_Permissions_PermissionId",
                table: "CavePermission");

            migrationBuilder.DropIndex(
                name: "IX_CavePermission_PermissionId",
                table: "CavePermission");

            migrationBuilder.DropIndex(
                name: "IX_CavePermission_UserId_AccountId_CaveId_PermissionId",
                table: "CavePermission");

            migrationBuilder.DropIndex(
                name: "IX_CavePermission_UserId_AccountId_CountyId_PermissionId",
                table: "CavePermission");

            migrationBuilder.DropIndex(
                name: "IX_CavePermission_UserId_AccountId_PermissionId",
                table: "CavePermission");

            migrationBuilder.DropColumn(
                name: "PermissionId",
                table: "CavePermission");

            migrationBuilder.AddColumn<string>(
                name: "CavePermissionId",
                table: "Permissions",
                type: "character varying(10)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CavePermissionPermissions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CavePermissionId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    PermissionId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CreatedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedByUserId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
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
                name: "IX_Permissions_CavePermissionId",
                table: "Permissions",
                column: "CavePermissionId");

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

            migrationBuilder.AddForeignKey(
                name: "FK_Permissions_CavePermission_CavePermissionId",
                table: "Permissions",
                column: "CavePermissionId",
                principalTable: "CavePermission",
                principalColumn: "Id");
        }
    }
}
