using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planarian.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class v16 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserPermissions_UserId_PermissionId",
                table: "UserPermissions");

            migrationBuilder.CreateIndex(
                name: "IX_UserPermissions_UserId_PermissionId_AccountId",
                table: "UserPermissions",
                columns: new[] { "UserId", "PermissionId", "AccountId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserPermissions_UserId_PermissionId_AccountId",
                table: "UserPermissions");

            migrationBuilder.CreateIndex(
                name: "IX_UserPermissions_UserId_PermissionId",
                table: "UserPermissions",
                columns: new[] { "UserId", "PermissionId" },
                unique: true);
        }
    }
}
