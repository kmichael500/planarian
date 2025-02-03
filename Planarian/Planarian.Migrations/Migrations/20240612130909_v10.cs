using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planarian.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class v10 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ArcheologyTags_Caves_CaveId",
                table: "ArcheologyTags");

            migrationBuilder.DropForeignKey(
                name: "FK_ArcheologyTags_TagTypes_TagTypeId",
                table: "ArcheologyTags");

            migrationBuilder.DropForeignKey(
                name: "FK_BiologyTags_Caves_CaveId",
                table: "BiologyTags");

            migrationBuilder.DropForeignKey(
                name: "FK_BiologyTags_TagTypes_TagTypeId",
                table: "BiologyTags");

            migrationBuilder.DropForeignKey(
                name: "FK_CartographerNameTags_Caves_CaveId",
                table: "CartographerNameTags");

            migrationBuilder.DropForeignKey(
                name: "FK_CartographerNameTags_TagTypes_TagTypeId",
                table: "CartographerNameTags");

            migrationBuilder.DropForeignKey(
                name: "FK_CaveOtherTags_Caves_CaveId",
                table: "CaveOtherTags");

            migrationBuilder.DropForeignKey(
                name: "FK_CaveOtherTags_TagTypes_TagTypeId",
                table: "CaveOtherTags");

            migrationBuilder.DropForeignKey(
                name: "FK_CaveReportedByNameTags_Caves_CaveId",
                table: "CaveReportedByNameTags");

            migrationBuilder.DropForeignKey(
                name: "FK_CaveReportedByNameTags_TagTypes_TagTypeId",
                table: "CaveReportedByNameTags");

            migrationBuilder.DropForeignKey(
                name: "FK_Caves_Accounts_AccountId",
                table: "Caves");

            migrationBuilder.DropForeignKey(
                name: "FK_EntranceHydrologyTags_Entrances_EntranceId",
                table: "EntranceHydrologyTags");

            migrationBuilder.DropForeignKey(
                name: "FK_EntranceHydrologyTags_TagTypes_TagTypeId",
                table: "EntranceHydrologyTags");

            migrationBuilder.DropForeignKey(
                name: "FK_EntranceOtherTag_Entrances_EntranceId",
                table: "EntranceOtherTag");

            migrationBuilder.DropForeignKey(
                name: "FK_EntranceOtherTag_TagTypes_TagTypeId",
                table: "EntranceOtherTag");

            migrationBuilder.DropForeignKey(
                name: "FK_EntranceReportedByNameTags_Entrances_EntranceId",
                table: "EntranceReportedByNameTags");

            migrationBuilder.DropForeignKey(
                name: "FK_EntranceReportedByNameTags_TagTypes_TagTypeId",
                table: "EntranceReportedByNameTags");

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

            migrationBuilder.DropForeignKey(
                name: "FK_Files_Caves_CaveId",
                table: "Files");

            migrationBuilder.DropForeignKey(
                name: "FK_Files_TagTypes_FileTypeTagId",
                table: "Files");

            migrationBuilder.DropForeignKey(
                name: "FK_GeologicAgeTags_Caves_CaveId",
                table: "GeologicAgeTags");

            migrationBuilder.DropForeignKey(
                name: "FK_GeologicAgeTags_TagTypes_TagTypeId",
                table: "GeologicAgeTags");

            migrationBuilder.DropForeignKey(
                name: "FK_GeologyTags_Caves_CaveId",
                table: "GeologyTags");

            migrationBuilder.DropForeignKey(
                name: "FK_GeologyTags_TagTypes_TagTypeId",
                table: "GeologyTags");

            migrationBuilder.DropForeignKey(
                name: "FK_Leads_Trips_TripId",
                table: "Leads");

            migrationBuilder.DropForeignKey(
                name: "FK_LeadTags_Leads_LeadId",
                table: "LeadTags");

            migrationBuilder.DropForeignKey(
                name: "FK_LeadTags_TagTypes_TagTypeId",
                table: "LeadTags");

            migrationBuilder.DropForeignKey(
                name: "FK_MapStatusTags_Caves_CaveId",
                table: "MapStatusTags");

            migrationBuilder.DropForeignKey(
                name: "FK_MapStatusTags_TagTypes_TagTypeId",
                table: "MapStatusTags");

            migrationBuilder.DropForeignKey(
                name: "FK_Members_Users_UserId",
                table: "Members");

            migrationBuilder.DropForeignKey(
                name: "FK_Photos_Trips_TripId",
                table: "Photos");

            migrationBuilder.DropForeignKey(
                name: "FK_PhysiographicProvinceTags_Caves_CaveId",
                table: "PhysiographicProvinceTags");

            migrationBuilder.DropForeignKey(
                name: "FK_PhysiographicProvinceTags_TagTypes_TagTypeId",
                table: "PhysiographicProvinceTags");

            migrationBuilder.DropForeignKey(
                name: "FK_Trips_Projects_ProjectId",
                table: "Trips");

            migrationBuilder.AddForeignKey(
                name: "FK_ArcheologyTags_Caves_CaveId",
                table: "ArcheologyTags",
                column: "CaveId",
                principalTable: "Caves",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ArcheologyTags_TagTypes_TagTypeId",
                table: "ArcheologyTags",
                column: "TagTypeId",
                principalTable: "TagTypes",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_BiologyTags_Caves_CaveId",
                table: "BiologyTags",
                column: "CaveId",
                principalTable: "Caves",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_BiologyTags_TagTypes_TagTypeId",
                table: "BiologyTags",
                column: "TagTypeId",
                principalTable: "TagTypes",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_CartographerNameTags_Caves_CaveId",
                table: "CartographerNameTags",
                column: "CaveId",
                principalTable: "Caves",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_CartographerNameTags_TagTypes_TagTypeId",
                table: "CartographerNameTags",
                column: "TagTypeId",
                principalTable: "TagTypes",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_CaveOtherTags_Caves_CaveId",
                table: "CaveOtherTags",
                column: "CaveId",
                principalTable: "Caves",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_CaveOtherTags_TagTypes_TagTypeId",
                table: "CaveOtherTags",
                column: "TagTypeId",
                principalTable: "TagTypes",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_CaveReportedByNameTags_Caves_CaveId",
                table: "CaveReportedByNameTags",
                column: "CaveId",
                principalTable: "Caves",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_CaveReportedByNameTags_TagTypes_TagTypeId",
                table: "CaveReportedByNameTags",
                column: "TagTypeId",
                principalTable: "TagTypes",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Caves_Accounts_AccountId",
                table: "Caves",
                column: "AccountId",
                principalTable: "Accounts",
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
                name: "FK_EntranceOtherTag_Entrances_EntranceId",
                table: "EntranceOtherTag",
                column: "EntranceId",
                principalTable: "Entrances",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_EntranceOtherTag_TagTypes_TagTypeId",
                table: "EntranceOtherTag",
                column: "TagTypeId",
                principalTable: "TagTypes",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_EntranceReportedByNameTags_Entrances_EntranceId",
                table: "EntranceReportedByNameTags",
                column: "EntranceId",
                principalTable: "Entrances",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_EntranceReportedByNameTags_TagTypes_TagTypeId",
                table: "EntranceReportedByNameTags",
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

            migrationBuilder.AddForeignKey(
                name: "FK_Files_Caves_CaveId",
                table: "Files",
                column: "CaveId",
                principalTable: "Caves",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Files_TagTypes_FileTypeTagId",
                table: "Files",
                column: "FileTypeTagId",
                principalTable: "TagTypes",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_GeologicAgeTags_Caves_CaveId",
                table: "GeologicAgeTags",
                column: "CaveId",
                principalTable: "Caves",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_GeologicAgeTags_TagTypes_TagTypeId",
                table: "GeologicAgeTags",
                column: "TagTypeId",
                principalTable: "TagTypes",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_GeologyTags_Caves_CaveId",
                table: "GeologyTags",
                column: "CaveId",
                principalTable: "Caves",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_GeologyTags_TagTypes_TagTypeId",
                table: "GeologyTags",
                column: "TagTypeId",
                principalTable: "TagTypes",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Leads_Trips_TripId",
                table: "Leads",
                column: "TripId",
                principalTable: "Trips",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_LeadTags_Leads_LeadId",
                table: "LeadTags",
                column: "LeadId",
                principalTable: "Leads",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_LeadTags_TagTypes_TagTypeId",
                table: "LeadTags",
                column: "TagTypeId",
                principalTable: "TagTypes",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_MapStatusTags_Caves_CaveId",
                table: "MapStatusTags",
                column: "CaveId",
                principalTable: "Caves",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_MapStatusTags_TagTypes_TagTypeId",
                table: "MapStatusTags",
                column: "TagTypeId",
                principalTable: "TagTypes",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Members_Users_UserId",
                table: "Members",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Photos_Trips_TripId",
                table: "Photos",
                column: "TripId",
                principalTable: "Trips",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PhysiographicProvinceTags_Caves_CaveId",
                table: "PhysiographicProvinceTags",
                column: "CaveId",
                principalTable: "Caves",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PhysiographicProvinceTags_TagTypes_TagTypeId",
                table: "PhysiographicProvinceTags",
                column: "TagTypeId",
                principalTable: "TagTypes",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Trips_Projects_ProjectId",
                table: "Trips",
                column: "ProjectId",
                principalTable: "Projects",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ArcheologyTags_Caves_CaveId",
                table: "ArcheologyTags");

            migrationBuilder.DropForeignKey(
                name: "FK_ArcheologyTags_TagTypes_TagTypeId",
                table: "ArcheologyTags");

            migrationBuilder.DropForeignKey(
                name: "FK_BiologyTags_Caves_CaveId",
                table: "BiologyTags");

            migrationBuilder.DropForeignKey(
                name: "FK_BiologyTags_TagTypes_TagTypeId",
                table: "BiologyTags");

            migrationBuilder.DropForeignKey(
                name: "FK_CartographerNameTags_Caves_CaveId",
                table: "CartographerNameTags");

            migrationBuilder.DropForeignKey(
                name: "FK_CartographerNameTags_TagTypes_TagTypeId",
                table: "CartographerNameTags");

            migrationBuilder.DropForeignKey(
                name: "FK_CaveOtherTags_Caves_CaveId",
                table: "CaveOtherTags");

            migrationBuilder.DropForeignKey(
                name: "FK_CaveOtherTags_TagTypes_TagTypeId",
                table: "CaveOtherTags");

            migrationBuilder.DropForeignKey(
                name: "FK_CaveReportedByNameTags_Caves_CaveId",
                table: "CaveReportedByNameTags");

            migrationBuilder.DropForeignKey(
                name: "FK_CaveReportedByNameTags_TagTypes_TagTypeId",
                table: "CaveReportedByNameTags");

            migrationBuilder.DropForeignKey(
                name: "FK_Caves_Accounts_AccountId",
                table: "Caves");

            migrationBuilder.DropForeignKey(
                name: "FK_EntranceHydrologyTags_Entrances_EntranceId",
                table: "EntranceHydrologyTags");

            migrationBuilder.DropForeignKey(
                name: "FK_EntranceHydrologyTags_TagTypes_TagTypeId",
                table: "EntranceHydrologyTags");

            migrationBuilder.DropForeignKey(
                name: "FK_EntranceOtherTag_Entrances_EntranceId",
                table: "EntranceOtherTag");

            migrationBuilder.DropForeignKey(
                name: "FK_EntranceOtherTag_TagTypes_TagTypeId",
                table: "EntranceOtherTag");

            migrationBuilder.DropForeignKey(
                name: "FK_EntranceReportedByNameTags_Entrances_EntranceId",
                table: "EntranceReportedByNameTags");

            migrationBuilder.DropForeignKey(
                name: "FK_EntranceReportedByNameTags_TagTypes_TagTypeId",
                table: "EntranceReportedByNameTags");

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

            migrationBuilder.DropForeignKey(
                name: "FK_Files_Caves_CaveId",
                table: "Files");

            migrationBuilder.DropForeignKey(
                name: "FK_Files_TagTypes_FileTypeTagId",
                table: "Files");

            migrationBuilder.DropForeignKey(
                name: "FK_GeologicAgeTags_Caves_CaveId",
                table: "GeologicAgeTags");

            migrationBuilder.DropForeignKey(
                name: "FK_GeologicAgeTags_TagTypes_TagTypeId",
                table: "GeologicAgeTags");

            migrationBuilder.DropForeignKey(
                name: "FK_GeologyTags_Caves_CaveId",
                table: "GeologyTags");

            migrationBuilder.DropForeignKey(
                name: "FK_GeologyTags_TagTypes_TagTypeId",
                table: "GeologyTags");

            migrationBuilder.DropForeignKey(
                name: "FK_Leads_Trips_TripId",
                table: "Leads");

            migrationBuilder.DropForeignKey(
                name: "FK_LeadTags_Leads_LeadId",
                table: "LeadTags");

            migrationBuilder.DropForeignKey(
                name: "FK_LeadTags_TagTypes_TagTypeId",
                table: "LeadTags");

            migrationBuilder.DropForeignKey(
                name: "FK_MapStatusTags_Caves_CaveId",
                table: "MapStatusTags");

            migrationBuilder.DropForeignKey(
                name: "FK_MapStatusTags_TagTypes_TagTypeId",
                table: "MapStatusTags");

            migrationBuilder.DropForeignKey(
                name: "FK_Members_Users_UserId",
                table: "Members");

            migrationBuilder.DropForeignKey(
                name: "FK_Photos_Trips_TripId",
                table: "Photos");

            migrationBuilder.DropForeignKey(
                name: "FK_PhysiographicProvinceTags_Caves_CaveId",
                table: "PhysiographicProvinceTags");

            migrationBuilder.DropForeignKey(
                name: "FK_PhysiographicProvinceTags_TagTypes_TagTypeId",
                table: "PhysiographicProvinceTags");

            migrationBuilder.DropForeignKey(
                name: "FK_Trips_Projects_ProjectId",
                table: "Trips");

            migrationBuilder.AddForeignKey(
                name: "FK_ArcheologyTags_Caves_CaveId",
                table: "ArcheologyTags",
                column: "CaveId",
                principalTable: "Caves",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ArcheologyTags_TagTypes_TagTypeId",
                table: "ArcheologyTags",
                column: "TagTypeId",
                principalTable: "TagTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_BiologyTags_Caves_CaveId",
                table: "BiologyTags",
                column: "CaveId",
                principalTable: "Caves",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_BiologyTags_TagTypes_TagTypeId",
                table: "BiologyTags",
                column: "TagTypeId",
                principalTable: "TagTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CartographerNameTags_Caves_CaveId",
                table: "CartographerNameTags",
                column: "CaveId",
                principalTable: "Caves",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CartographerNameTags_TagTypes_TagTypeId",
                table: "CartographerNameTags",
                column: "TagTypeId",
                principalTable: "TagTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CaveOtherTags_Caves_CaveId",
                table: "CaveOtherTags",
                column: "CaveId",
                principalTable: "Caves",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CaveOtherTags_TagTypes_TagTypeId",
                table: "CaveOtherTags",
                column: "TagTypeId",
                principalTable: "TagTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CaveReportedByNameTags_Caves_CaveId",
                table: "CaveReportedByNameTags",
                column: "CaveId",
                principalTable: "Caves",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CaveReportedByNameTags_TagTypes_TagTypeId",
                table: "CaveReportedByNameTags",
                column: "TagTypeId",
                principalTable: "TagTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Caves_Accounts_AccountId",
                table: "Caves",
                column: "AccountId",
                principalTable: "Accounts",
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
                name: "FK_EntranceOtherTag_Entrances_EntranceId",
                table: "EntranceOtherTag",
                column: "EntranceId",
                principalTable: "Entrances",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_EntranceOtherTag_TagTypes_TagTypeId",
                table: "EntranceOtherTag",
                column: "TagTypeId",
                principalTable: "TagTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_EntranceReportedByNameTags_Entrances_EntranceId",
                table: "EntranceReportedByNameTags",
                column: "EntranceId",
                principalTable: "Entrances",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_EntranceReportedByNameTags_TagTypes_TagTypeId",
                table: "EntranceReportedByNameTags",
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

            migrationBuilder.AddForeignKey(
                name: "FK_Files_Caves_CaveId",
                table: "Files",
                column: "CaveId",
                principalTable: "Caves",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Files_TagTypes_FileTypeTagId",
                table: "Files",
                column: "FileTypeTagId",
                principalTable: "TagTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_GeologicAgeTags_Caves_CaveId",
                table: "GeologicAgeTags",
                column: "CaveId",
                principalTable: "Caves",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_GeologicAgeTags_TagTypes_TagTypeId",
                table: "GeologicAgeTags",
                column: "TagTypeId",
                principalTable: "TagTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_GeologyTags_Caves_CaveId",
                table: "GeologyTags",
                column: "CaveId",
                principalTable: "Caves",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_GeologyTags_TagTypes_TagTypeId",
                table: "GeologyTags",
                column: "TagTypeId",
                principalTable: "TagTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Leads_Trips_TripId",
                table: "Leads",
                column: "TripId",
                principalTable: "Trips",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_LeadTags_Leads_LeadId",
                table: "LeadTags",
                column: "LeadId",
                principalTable: "Leads",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_LeadTags_TagTypes_TagTypeId",
                table: "LeadTags",
                column: "TagTypeId",
                principalTable: "TagTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MapStatusTags_Caves_CaveId",
                table: "MapStatusTags",
                column: "CaveId",
                principalTable: "Caves",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MapStatusTags_TagTypes_TagTypeId",
                table: "MapStatusTags",
                column: "TagTypeId",
                principalTable: "TagTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Members_Users_UserId",
                table: "Members",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Photos_Trips_TripId",
                table: "Photos",
                column: "TripId",
                principalTable: "Trips",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PhysiographicProvinceTags_Caves_CaveId",
                table: "PhysiographicProvinceTags",
                column: "CaveId",
                principalTable: "Caves",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PhysiographicProvinceTags_TagTypes_TagTypeId",
                table: "PhysiographicProvinceTags",
                column: "TagTypeId",
                principalTable: "TagTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Trips_Projects_ProjectId",
                table: "Trips",
                column: "ProjectId",
                principalTable: "Projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
