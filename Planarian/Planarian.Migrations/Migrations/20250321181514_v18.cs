using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planarian.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class v18 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_UserPermissions_UserId_PermissionId",
                table: "UserPermissions",
                columns: new[] { "UserId", "PermissionId" });

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_Key",
                table: "Permissions",
                column: "Key");

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_PermissionType_Key",
                table: "Permissions",
                columns: new[] { "PermissionType", "Key" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserPermissions_UserId_PermissionId",
                table: "UserPermissions");

            migrationBuilder.DropIndex(
                name: "IX_Permissions_Key",
                table: "Permissions");

            migrationBuilder.DropIndex(
                name: "IX_Permissions_PermissionType_Key",
                table: "Permissions");
        }
    }
}
