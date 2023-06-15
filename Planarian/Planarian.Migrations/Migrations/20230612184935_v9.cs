using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planarian.Migrations.Migrations
{
    public partial class v9 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EntranceHydrologyFrequencyTags_Entrances_EntranceId",
                table: "EntranceHydrologyFrequencyTags");

            migrationBuilder.DropForeignKey(
                name: "FK_EntranceHydrologyFrequencyTags_TagTypes_TagTypeId",
                table: "EntranceHydrologyFrequencyTags");

            migrationBuilder.DropForeignKey(
                name: "FK_EntranceHydrologyTags_Entrances_EntranceId",
                table: "EntranceHydrologyTags");

            migrationBuilder.DropForeignKey(
                name: "FK_EntranceHydrologyTags_TagTypes_TagTypeId",
                table: "EntranceHydrologyTags");

            migrationBuilder.DropForeignKey(
                name: "FK_Entrances_Caves_CaveId",
                table: "Entrances");

            migrationBuilder.DropForeignKey(
                name: "FK_Entrances_Caves_PrimaryEntranceCaveId",
                table: "Entrances");

            migrationBuilder.DropForeignKey(
                name: "FK_Entrances_Users_ReportedByUserId",
                table: "Entrances");

            migrationBuilder.DropForeignKey(
                name: "FK_EntranceStatusTags_Entrances_EntranceId",
                table: "EntranceStatusTags");

            migrationBuilder.DropForeignKey(
                name: "FK_EntranceStatusTags_TagTypes_TagTypeId",
                table: "EntranceStatusTags");

            migrationBuilder.DropForeignKey(
                name: "FK_FieldIndicationTags_Entrances_EntranceId",
                table: "FieldIndicationTags");

            migrationBuilder.DropForeignKey(
                name: "FK_FieldIndicationTags_TagTypes_TagTypeId",
                table: "FieldIndicationTags");

            migrationBuilder.DropIndex(
                name: "IX_Entrances_CaveId",
                table: "Entrances");

            migrationBuilder.DropIndex(
                name: "IX_Entrances_PrimaryEntranceCaveId",
                table: "Entrances");

            migrationBuilder.DropColumn(
                name: "PrimaryEntranceCaveId",
                table: "Entrances");

            migrationBuilder.AddColumn<bool>(
                name: "IsPrimary",
                table: "Entrances",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Entrances_CaveId_IsPrimary",
                table: "Entrances",
                columns: new[] { "CaveId", "IsPrimary" },
                unique: true,
                filter: "[IsPrimary] = 1");

            migrationBuilder.AddForeignKey(
                name: "FK_EntranceHydrologyFrequencyTags_Entrances_EntranceId",
                table: "EntranceHydrologyFrequencyTags",
                column: "EntranceId",
                principalTable: "Entrances",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_EntranceHydrologyFrequencyTags_TagTypes_TagTypeId",
                table: "EntranceHydrologyFrequencyTags",
                column: "TagTypeId",
                principalTable: "TagTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_EntranceHydrologyTags_Entrances_EntranceId",
                table: "EntranceHydrologyTags",
                column: "EntranceId",
                principalTable: "Entrances",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_EntranceHydrologyTags_TagTypes_TagTypeId",
                table: "EntranceHydrologyTags",
                column: "TagTypeId",
                principalTable: "TagTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Entrances_Caves_CaveId",
                table: "Entrances",
                column: "CaveId",
                principalTable: "Caves",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Entrances_Users_ReportedByUserId",
                table: "Entrances",
                column: "ReportedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_EntranceStatusTags_Entrances_EntranceId",
                table: "EntranceStatusTags",
                column: "EntranceId",
                principalTable: "Entrances",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_EntranceStatusTags_TagTypes_TagTypeId",
                table: "EntranceStatusTags",
                column: "TagTypeId",
                principalTable: "TagTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_FieldIndicationTags_Entrances_EntranceId",
                table: "FieldIndicationTags",
                column: "EntranceId",
                principalTable: "Entrances",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_FieldIndicationTags_TagTypes_TagTypeId",
                table: "FieldIndicationTags",
                column: "TagTypeId",
                principalTable: "TagTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EntranceHydrologyFrequencyTags_Entrances_EntranceId",
                table: "EntranceHydrologyFrequencyTags");

            migrationBuilder.DropForeignKey(
                name: "FK_EntranceHydrologyFrequencyTags_TagTypes_TagTypeId",
                table: "EntranceHydrologyFrequencyTags");

            migrationBuilder.DropForeignKey(
                name: "FK_EntranceHydrologyTags_Entrances_EntranceId",
                table: "EntranceHydrologyTags");

            migrationBuilder.DropForeignKey(
                name: "FK_EntranceHydrologyTags_TagTypes_TagTypeId",
                table: "EntranceHydrologyTags");

            migrationBuilder.DropForeignKey(
                name: "FK_Entrances_Caves_CaveId",
                table: "Entrances");

            migrationBuilder.DropForeignKey(
                name: "FK_Entrances_Users_ReportedByUserId",
                table: "Entrances");

            migrationBuilder.DropForeignKey(
                name: "FK_EntranceStatusTags_Entrances_EntranceId",
                table: "EntranceStatusTags");

            migrationBuilder.DropForeignKey(
                name: "FK_EntranceStatusTags_TagTypes_TagTypeId",
                table: "EntranceStatusTags");

            migrationBuilder.DropForeignKey(
                name: "FK_FieldIndicationTags_Entrances_EntranceId",
                table: "FieldIndicationTags");

            migrationBuilder.DropForeignKey(
                name: "FK_FieldIndicationTags_TagTypes_TagTypeId",
                table: "FieldIndicationTags");

            migrationBuilder.DropIndex(
                name: "IX_Entrances_CaveId_IsPrimary",
                table: "Entrances");

            migrationBuilder.DropColumn(
                name: "IsPrimary",
                table: "Entrances");

            migrationBuilder.AddColumn<string>(
                name: "PrimaryEntranceCaveId",
                table: "Entrances",
                type: "nvarchar(10)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Entrances_CaveId",
                table: "Entrances",
                column: "CaveId");

            migrationBuilder.CreateIndex(
                name: "IX_Entrances_PrimaryEntranceCaveId",
                table: "Entrances",
                column: "PrimaryEntranceCaveId",
                unique: true,
                filter: "[PrimaryEntranceCaveId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_EntranceHydrologyFrequencyTags_Entrances_EntranceId",
                table: "EntranceHydrologyFrequencyTags",
                column: "EntranceId",
                principalTable: "Entrances",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_EntranceHydrologyFrequencyTags_TagTypes_TagTypeId",
                table: "EntranceHydrologyFrequencyTags",
                column: "TagTypeId",
                principalTable: "TagTypes",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_EntranceHydrologyTags_Entrances_EntranceId",
                table: "EntranceHydrologyTags",
                column: "EntranceId",
                principalTable: "Entrances",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_EntranceHydrologyTags_TagTypes_TagTypeId",
                table: "EntranceHydrologyTags",
                column: "TagTypeId",
                principalTable: "TagTypes",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Entrances_Caves_CaveId",
                table: "Entrances",
                column: "CaveId",
                principalTable: "Caves",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Entrances_Caves_PrimaryEntranceCaveId",
                table: "Entrances",
                column: "PrimaryEntranceCaveId",
                principalTable: "Caves",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Entrances_Users_ReportedByUserId",
                table: "Entrances",
                column: "ReportedByUserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_EntranceStatusTags_Entrances_EntranceId",
                table: "EntranceStatusTags",
                column: "EntranceId",
                principalTable: "Entrances",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_EntranceStatusTags_TagTypes_TagTypeId",
                table: "EntranceStatusTags",
                column: "TagTypeId",
                principalTable: "TagTypes",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_FieldIndicationTags_Entrances_EntranceId",
                table: "FieldIndicationTags",
                column: "EntranceId",
                principalTable: "Entrances",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_FieldIndicationTags_TagTypes_TagTypeId",
                table: "FieldIndicationTags",
                column: "TagTypeId",
                principalTable: "TagTypes",
                principalColumn: "Id");
        }
    }
}
