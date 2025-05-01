using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planarian.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class v29 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsApproved",
                table: "CaveChangeRequests");

            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "CaveChangeRequests",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "Submission");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Type",
                table: "CaveChangeRequests");

            migrationBuilder.AddColumn<bool>(
                name: "IsApproved",
                table: "CaveChangeRequests",
                type: "boolean",
                nullable: true);
        }
    }
}
