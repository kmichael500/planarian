using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planarian.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class v5 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ArcheologyTag_Caves_CaveId",
                table: "ArcheologyTag");

            migrationBuilder.DropForeignKey(
                name: "FK_ArcheologyTag_TagTypes_TagTypeId",
                table: "ArcheologyTag");

            migrationBuilder.DropForeignKey(
                name: "FK_ArcheologyTag_Users_CreatedByUserId",
                table: "ArcheologyTag");

            migrationBuilder.DropForeignKey(
                name: "FK_ArcheologyTag_Users_ModifiedByUserId",
                table: "ArcheologyTag");

            migrationBuilder.DropForeignKey(
                name: "FK_BiologyTag_Caves_CaveId",
                table: "BiologyTag");

            migrationBuilder.DropForeignKey(
                name: "FK_BiologyTag_TagTypes_TagTypeId",
                table: "BiologyTag");

            migrationBuilder.DropForeignKey(
                name: "FK_BiologyTag_Users_CreatedByUserId",
                table: "BiologyTag");

            migrationBuilder.DropForeignKey(
                name: "FK_BiologyTag_Users_ModifiedByUserId",
                table: "BiologyTag");

            migrationBuilder.DropForeignKey(
                name: "FK_CartographerNameTag_Caves_CaveId",
                table: "CartographerNameTag");

            migrationBuilder.DropForeignKey(
                name: "FK_CartographerNameTag_TagTypes_TagTypeId",
                table: "CartographerNameTag");

            migrationBuilder.DropForeignKey(
                name: "FK_CartographerNameTag_Users_CreatedByUserId",
                table: "CartographerNameTag");

            migrationBuilder.DropForeignKey(
                name: "FK_CartographerNameTag_Users_ModifiedByUserId",
                table: "CartographerNameTag");

            migrationBuilder.DropForeignKey(
                name: "FK_CaveOtherTag_Caves_CaveId",
                table: "CaveOtherTag");

            migrationBuilder.DropForeignKey(
                name: "FK_CaveOtherTag_TagTypes_TagTypeId",
                table: "CaveOtherTag");

            migrationBuilder.DropForeignKey(
                name: "FK_CaveOtherTag_Users_CreatedByUserId",
                table: "CaveOtherTag");

            migrationBuilder.DropForeignKey(
                name: "FK_CaveOtherTag_Users_ModifiedByUserId",
                table: "CaveOtherTag");

            migrationBuilder.DropForeignKey(
                name: "FK_GeologicAgeTag_Caves_CaveId",
                table: "GeologicAgeTag");

            migrationBuilder.DropForeignKey(
                name: "FK_GeologicAgeTag_TagTypes_TagTypeId",
                table: "GeologicAgeTag");

            migrationBuilder.DropForeignKey(
                name: "FK_GeologicAgeTag_Users_CreatedByUserId",
                table: "GeologicAgeTag");

            migrationBuilder.DropForeignKey(
                name: "FK_GeologicAgeTag_Users_ModifiedByUserId",
                table: "GeologicAgeTag");

            migrationBuilder.DropForeignKey(
                name: "FK_MapStatusTag_Caves_CaveId",
                table: "MapStatusTag");

            migrationBuilder.DropForeignKey(
                name: "FK_MapStatusTag_TagTypes_TagTypeId",
                table: "MapStatusTag");

            migrationBuilder.DropForeignKey(
                name: "FK_MapStatusTag_Users_CreatedByUserId",
                table: "MapStatusTag");

            migrationBuilder.DropForeignKey(
                name: "FK_MapStatusTag_Users_ModifiedByUserId",
                table: "MapStatusTag");

            migrationBuilder.DropForeignKey(
                name: "FK_PhysiographicProvinceTag_Caves_CaveId",
                table: "PhysiographicProvinceTag");

            migrationBuilder.DropForeignKey(
                name: "FK_PhysiographicProvinceTag_TagTypes_TagTypeId",
                table: "PhysiographicProvinceTag");

            migrationBuilder.DropForeignKey(
                name: "FK_PhysiographicProvinceTag_Users_CreatedByUserId",
                table: "PhysiographicProvinceTag");

            migrationBuilder.DropForeignKey(
                name: "FK_PhysiographicProvinceTag_Users_ModifiedByUserId",
                table: "PhysiographicProvinceTag");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PhysiographicProvinceTag",
                table: "PhysiographicProvinceTag");

            migrationBuilder.DropPrimaryKey(
                name: "PK_MapStatusTag",
                table: "MapStatusTag");

            migrationBuilder.DropPrimaryKey(
                name: "PK_GeologicAgeTag",
                table: "GeologicAgeTag");

            migrationBuilder.DropPrimaryKey(
                name: "PK_CaveOtherTag",
                table: "CaveOtherTag");

            migrationBuilder.DropPrimaryKey(
                name: "PK_CartographerNameTag",
                table: "CartographerNameTag");

            migrationBuilder.DropPrimaryKey(
                name: "PK_BiologyTag",
                table: "BiologyTag");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ArcheologyTag",
                table: "ArcheologyTag");

            migrationBuilder.RenameTable(
                name: "PhysiographicProvinceTag",
                newName: "PhysiographicProvinceTags");

            migrationBuilder.RenameTable(
                name: "MapStatusTag",
                newName: "MapStatusTags");

            migrationBuilder.RenameTable(
                name: "GeologicAgeTag",
                newName: "GeologicAgeTags");

            migrationBuilder.RenameTable(
                name: "CaveOtherTag",
                newName: "CaveOtherTags");

            migrationBuilder.RenameTable(
                name: "CartographerNameTag",
                newName: "CartographerNameTags");

            migrationBuilder.RenameTable(
                name: "BiologyTag",
                newName: "BiologyTags");

            migrationBuilder.RenameTable(
                name: "ArcheologyTag",
                newName: "ArcheologyTags");

            migrationBuilder.RenameIndex(
                name: "IX_PhysiographicProvinceTag_ModifiedByUserId",
                table: "PhysiographicProvinceTags",
                newName: "IX_PhysiographicProvinceTags_ModifiedByUserId");

            migrationBuilder.RenameIndex(
                name: "IX_PhysiographicProvinceTag_CreatedByUserId",
                table: "PhysiographicProvinceTags",
                newName: "IX_PhysiographicProvinceTags_CreatedByUserId");

            migrationBuilder.RenameIndex(
                name: "IX_PhysiographicProvinceTag_CaveId",
                table: "PhysiographicProvinceTags",
                newName: "IX_PhysiographicProvinceTags_CaveId");

            migrationBuilder.RenameIndex(
                name: "IX_MapStatusTag_ModifiedByUserId",
                table: "MapStatusTags",
                newName: "IX_MapStatusTags_ModifiedByUserId");

            migrationBuilder.RenameIndex(
                name: "IX_MapStatusTag_CreatedByUserId",
                table: "MapStatusTags",
                newName: "IX_MapStatusTags_CreatedByUserId");

            migrationBuilder.RenameIndex(
                name: "IX_MapStatusTag_CaveId",
                table: "MapStatusTags",
                newName: "IX_MapStatusTags_CaveId");

            migrationBuilder.RenameIndex(
                name: "IX_GeologicAgeTag_ModifiedByUserId",
                table: "GeologicAgeTags",
                newName: "IX_GeologicAgeTags_ModifiedByUserId");

            migrationBuilder.RenameIndex(
                name: "IX_GeologicAgeTag_CreatedByUserId",
                table: "GeologicAgeTags",
                newName: "IX_GeologicAgeTags_CreatedByUserId");

            migrationBuilder.RenameIndex(
                name: "IX_GeologicAgeTag_CaveId",
                table: "GeologicAgeTags",
                newName: "IX_GeologicAgeTags_CaveId");

            migrationBuilder.RenameIndex(
                name: "IX_CaveOtherTag_ModifiedByUserId",
                table: "CaveOtherTags",
                newName: "IX_CaveOtherTags_ModifiedByUserId");

            migrationBuilder.RenameIndex(
                name: "IX_CaveOtherTag_CreatedByUserId",
                table: "CaveOtherTags",
                newName: "IX_CaveOtherTags_CreatedByUserId");

            migrationBuilder.RenameIndex(
                name: "IX_CaveOtherTag_CaveId",
                table: "CaveOtherTags",
                newName: "IX_CaveOtherTags_CaveId");

            migrationBuilder.RenameIndex(
                name: "IX_CartographerNameTag_ModifiedByUserId",
                table: "CartographerNameTags",
                newName: "IX_CartographerNameTags_ModifiedByUserId");

            migrationBuilder.RenameIndex(
                name: "IX_CartographerNameTag_CreatedByUserId",
                table: "CartographerNameTags",
                newName: "IX_CartographerNameTags_CreatedByUserId");

            migrationBuilder.RenameIndex(
                name: "IX_CartographerNameTag_CaveId",
                table: "CartographerNameTags",
                newName: "IX_CartographerNameTags_CaveId");

            migrationBuilder.RenameIndex(
                name: "IX_BiologyTag_ModifiedByUserId",
                table: "BiologyTags",
                newName: "IX_BiologyTags_ModifiedByUserId");

            migrationBuilder.RenameIndex(
                name: "IX_BiologyTag_CreatedByUserId",
                table: "BiologyTags",
                newName: "IX_BiologyTags_CreatedByUserId");

            migrationBuilder.RenameIndex(
                name: "IX_BiologyTag_CaveId",
                table: "BiologyTags",
                newName: "IX_BiologyTags_CaveId");

            migrationBuilder.RenameIndex(
                name: "IX_ArcheologyTag_ModifiedByUserId",
                table: "ArcheologyTags",
                newName: "IX_ArcheologyTags_ModifiedByUserId");

            migrationBuilder.RenameIndex(
                name: "IX_ArcheologyTag_CreatedByUserId",
                table: "ArcheologyTags",
                newName: "IX_ArcheologyTags_CreatedByUserId");

            migrationBuilder.RenameIndex(
                name: "IX_ArcheologyTag_CaveId",
                table: "ArcheologyTags",
                newName: "IX_ArcheologyTags_CaveId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_PhysiographicProvinceTags",
                table: "PhysiographicProvinceTags",
                columns: new[] { "TagTypeId", "CaveId" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_MapStatusTags",
                table: "MapStatusTags",
                columns: new[] { "TagTypeId", "CaveId" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_GeologicAgeTags",
                table: "GeologicAgeTags",
                columns: new[] { "TagTypeId", "CaveId" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_CaveOtherTags",
                table: "CaveOtherTags",
                columns: new[] { "TagTypeId", "CaveId" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_CartographerNameTags",
                table: "CartographerNameTags",
                columns: new[] { "TagTypeId", "CaveId" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_BiologyTags",
                table: "BiologyTags",
                columns: new[] { "TagTypeId", "CaveId" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_ArcheologyTags",
                table: "ArcheologyTags",
                columns: new[] { "TagTypeId", "CaveId" });

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
                name: "FK_ArcheologyTags_Users_CreatedByUserId",
                table: "ArcheologyTags",
                column: "CreatedByUserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ArcheologyTags_Users_ModifiedByUserId",
                table: "ArcheologyTags",
                column: "ModifiedByUserId",
                principalTable: "Users",
                principalColumn: "Id");

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
                name: "FK_BiologyTags_Users_CreatedByUserId",
                table: "BiologyTags",
                column: "CreatedByUserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_BiologyTags_Users_ModifiedByUserId",
                table: "BiologyTags",
                column: "ModifiedByUserId",
                principalTable: "Users",
                principalColumn: "Id");

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
                name: "FK_CartographerNameTags_Users_CreatedByUserId",
                table: "CartographerNameTags",
                column: "CreatedByUserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_CartographerNameTags_Users_ModifiedByUserId",
                table: "CartographerNameTags",
                column: "ModifiedByUserId",
                principalTable: "Users",
                principalColumn: "Id");

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
                name: "FK_CaveOtherTags_Users_CreatedByUserId",
                table: "CaveOtherTags",
                column: "CreatedByUserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_CaveOtherTags_Users_ModifiedByUserId",
                table: "CaveOtherTags",
                column: "ModifiedByUserId",
                principalTable: "Users",
                principalColumn: "Id");

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
                name: "FK_GeologicAgeTags_Users_CreatedByUserId",
                table: "GeologicAgeTags",
                column: "CreatedByUserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_GeologicAgeTags_Users_ModifiedByUserId",
                table: "GeologicAgeTags",
                column: "ModifiedByUserId",
                principalTable: "Users",
                principalColumn: "Id");

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
                name: "FK_MapStatusTags_Users_CreatedByUserId",
                table: "MapStatusTags",
                column: "CreatedByUserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_MapStatusTags_Users_ModifiedByUserId",
                table: "MapStatusTags",
                column: "ModifiedByUserId",
                principalTable: "Users",
                principalColumn: "Id");

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
                name: "FK_PhysiographicProvinceTags_Users_CreatedByUserId",
                table: "PhysiographicProvinceTags",
                column: "CreatedByUserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PhysiographicProvinceTags_Users_ModifiedByUserId",
                table: "PhysiographicProvinceTags",
                column: "ModifiedByUserId",
                principalTable: "Users",
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
                name: "FK_ArcheologyTags_Users_CreatedByUserId",
                table: "ArcheologyTags");

            migrationBuilder.DropForeignKey(
                name: "FK_ArcheologyTags_Users_ModifiedByUserId",
                table: "ArcheologyTags");

            migrationBuilder.DropForeignKey(
                name: "FK_BiologyTags_Caves_CaveId",
                table: "BiologyTags");

            migrationBuilder.DropForeignKey(
                name: "FK_BiologyTags_TagTypes_TagTypeId",
                table: "BiologyTags");

            migrationBuilder.DropForeignKey(
                name: "FK_BiologyTags_Users_CreatedByUserId",
                table: "BiologyTags");

            migrationBuilder.DropForeignKey(
                name: "FK_BiologyTags_Users_ModifiedByUserId",
                table: "BiologyTags");

            migrationBuilder.DropForeignKey(
                name: "FK_CartographerNameTags_Caves_CaveId",
                table: "CartographerNameTags");

            migrationBuilder.DropForeignKey(
                name: "FK_CartographerNameTags_TagTypes_TagTypeId",
                table: "CartographerNameTags");

            migrationBuilder.DropForeignKey(
                name: "FK_CartographerNameTags_Users_CreatedByUserId",
                table: "CartographerNameTags");

            migrationBuilder.DropForeignKey(
                name: "FK_CartographerNameTags_Users_ModifiedByUserId",
                table: "CartographerNameTags");

            migrationBuilder.DropForeignKey(
                name: "FK_CaveOtherTags_Caves_CaveId",
                table: "CaveOtherTags");

            migrationBuilder.DropForeignKey(
                name: "FK_CaveOtherTags_TagTypes_TagTypeId",
                table: "CaveOtherTags");

            migrationBuilder.DropForeignKey(
                name: "FK_CaveOtherTags_Users_CreatedByUserId",
                table: "CaveOtherTags");

            migrationBuilder.DropForeignKey(
                name: "FK_CaveOtherTags_Users_ModifiedByUserId",
                table: "CaveOtherTags");

            migrationBuilder.DropForeignKey(
                name: "FK_GeologicAgeTags_Caves_CaveId",
                table: "GeologicAgeTags");

            migrationBuilder.DropForeignKey(
                name: "FK_GeologicAgeTags_TagTypes_TagTypeId",
                table: "GeologicAgeTags");

            migrationBuilder.DropForeignKey(
                name: "FK_GeologicAgeTags_Users_CreatedByUserId",
                table: "GeologicAgeTags");

            migrationBuilder.DropForeignKey(
                name: "FK_GeologicAgeTags_Users_ModifiedByUserId",
                table: "GeologicAgeTags");

            migrationBuilder.DropForeignKey(
                name: "FK_MapStatusTags_Caves_CaveId",
                table: "MapStatusTags");

            migrationBuilder.DropForeignKey(
                name: "FK_MapStatusTags_TagTypes_TagTypeId",
                table: "MapStatusTags");

            migrationBuilder.DropForeignKey(
                name: "FK_MapStatusTags_Users_CreatedByUserId",
                table: "MapStatusTags");

            migrationBuilder.DropForeignKey(
                name: "FK_MapStatusTags_Users_ModifiedByUserId",
                table: "MapStatusTags");

            migrationBuilder.DropForeignKey(
                name: "FK_PhysiographicProvinceTags_Caves_CaveId",
                table: "PhysiographicProvinceTags");

            migrationBuilder.DropForeignKey(
                name: "FK_PhysiographicProvinceTags_TagTypes_TagTypeId",
                table: "PhysiographicProvinceTags");

            migrationBuilder.DropForeignKey(
                name: "FK_PhysiographicProvinceTags_Users_CreatedByUserId",
                table: "PhysiographicProvinceTags");

            migrationBuilder.DropForeignKey(
                name: "FK_PhysiographicProvinceTags_Users_ModifiedByUserId",
                table: "PhysiographicProvinceTags");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PhysiographicProvinceTags",
                table: "PhysiographicProvinceTags");

            migrationBuilder.DropPrimaryKey(
                name: "PK_MapStatusTags",
                table: "MapStatusTags");

            migrationBuilder.DropPrimaryKey(
                name: "PK_GeologicAgeTags",
                table: "GeologicAgeTags");

            migrationBuilder.DropPrimaryKey(
                name: "PK_CaveOtherTags",
                table: "CaveOtherTags");

            migrationBuilder.DropPrimaryKey(
                name: "PK_CartographerNameTags",
                table: "CartographerNameTags");

            migrationBuilder.DropPrimaryKey(
                name: "PK_BiologyTags",
                table: "BiologyTags");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ArcheologyTags",
                table: "ArcheologyTags");

            migrationBuilder.RenameTable(
                name: "PhysiographicProvinceTags",
                newName: "PhysiographicProvinceTag");

            migrationBuilder.RenameTable(
                name: "MapStatusTags",
                newName: "MapStatusTag");

            migrationBuilder.RenameTable(
                name: "GeologicAgeTags",
                newName: "GeologicAgeTag");

            migrationBuilder.RenameTable(
                name: "CaveOtherTags",
                newName: "CaveOtherTag");

            migrationBuilder.RenameTable(
                name: "CartographerNameTags",
                newName: "CartographerNameTag");

            migrationBuilder.RenameTable(
                name: "BiologyTags",
                newName: "BiologyTag");

            migrationBuilder.RenameTable(
                name: "ArcheologyTags",
                newName: "ArcheologyTag");

            migrationBuilder.RenameIndex(
                name: "IX_PhysiographicProvinceTags_ModifiedByUserId",
                table: "PhysiographicProvinceTag",
                newName: "IX_PhysiographicProvinceTag_ModifiedByUserId");

            migrationBuilder.RenameIndex(
                name: "IX_PhysiographicProvinceTags_CreatedByUserId",
                table: "PhysiographicProvinceTag",
                newName: "IX_PhysiographicProvinceTag_CreatedByUserId");

            migrationBuilder.RenameIndex(
                name: "IX_PhysiographicProvinceTags_CaveId",
                table: "PhysiographicProvinceTag",
                newName: "IX_PhysiographicProvinceTag_CaveId");

            migrationBuilder.RenameIndex(
                name: "IX_MapStatusTags_ModifiedByUserId",
                table: "MapStatusTag",
                newName: "IX_MapStatusTag_ModifiedByUserId");

            migrationBuilder.RenameIndex(
                name: "IX_MapStatusTags_CreatedByUserId",
                table: "MapStatusTag",
                newName: "IX_MapStatusTag_CreatedByUserId");

            migrationBuilder.RenameIndex(
                name: "IX_MapStatusTags_CaveId",
                table: "MapStatusTag",
                newName: "IX_MapStatusTag_CaveId");

            migrationBuilder.RenameIndex(
                name: "IX_GeologicAgeTags_ModifiedByUserId",
                table: "GeologicAgeTag",
                newName: "IX_GeologicAgeTag_ModifiedByUserId");

            migrationBuilder.RenameIndex(
                name: "IX_GeologicAgeTags_CreatedByUserId",
                table: "GeologicAgeTag",
                newName: "IX_GeologicAgeTag_CreatedByUserId");

            migrationBuilder.RenameIndex(
                name: "IX_GeologicAgeTags_CaveId",
                table: "GeologicAgeTag",
                newName: "IX_GeologicAgeTag_CaveId");

            migrationBuilder.RenameIndex(
                name: "IX_CaveOtherTags_ModifiedByUserId",
                table: "CaveOtherTag",
                newName: "IX_CaveOtherTag_ModifiedByUserId");

            migrationBuilder.RenameIndex(
                name: "IX_CaveOtherTags_CreatedByUserId",
                table: "CaveOtherTag",
                newName: "IX_CaveOtherTag_CreatedByUserId");

            migrationBuilder.RenameIndex(
                name: "IX_CaveOtherTags_CaveId",
                table: "CaveOtherTag",
                newName: "IX_CaveOtherTag_CaveId");

            migrationBuilder.RenameIndex(
                name: "IX_CartographerNameTags_ModifiedByUserId",
                table: "CartographerNameTag",
                newName: "IX_CartographerNameTag_ModifiedByUserId");

            migrationBuilder.RenameIndex(
                name: "IX_CartographerNameTags_CreatedByUserId",
                table: "CartographerNameTag",
                newName: "IX_CartographerNameTag_CreatedByUserId");

            migrationBuilder.RenameIndex(
                name: "IX_CartographerNameTags_CaveId",
                table: "CartographerNameTag",
                newName: "IX_CartographerNameTag_CaveId");

            migrationBuilder.RenameIndex(
                name: "IX_BiologyTags_ModifiedByUserId",
                table: "BiologyTag",
                newName: "IX_BiologyTag_ModifiedByUserId");

            migrationBuilder.RenameIndex(
                name: "IX_BiologyTags_CreatedByUserId",
                table: "BiologyTag",
                newName: "IX_BiologyTag_CreatedByUserId");

            migrationBuilder.RenameIndex(
                name: "IX_BiologyTags_CaveId",
                table: "BiologyTag",
                newName: "IX_BiologyTag_CaveId");

            migrationBuilder.RenameIndex(
                name: "IX_ArcheologyTags_ModifiedByUserId",
                table: "ArcheologyTag",
                newName: "IX_ArcheologyTag_ModifiedByUserId");

            migrationBuilder.RenameIndex(
                name: "IX_ArcheologyTags_CreatedByUserId",
                table: "ArcheologyTag",
                newName: "IX_ArcheologyTag_CreatedByUserId");

            migrationBuilder.RenameIndex(
                name: "IX_ArcheologyTags_CaveId",
                table: "ArcheologyTag",
                newName: "IX_ArcheologyTag_CaveId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_PhysiographicProvinceTag",
                table: "PhysiographicProvinceTag",
                columns: new[] { "TagTypeId", "CaveId" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_MapStatusTag",
                table: "MapStatusTag",
                columns: new[] { "TagTypeId", "CaveId" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_GeologicAgeTag",
                table: "GeologicAgeTag",
                columns: new[] { "TagTypeId", "CaveId" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_CaveOtherTag",
                table: "CaveOtherTag",
                columns: new[] { "TagTypeId", "CaveId" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_CartographerNameTag",
                table: "CartographerNameTag",
                columns: new[] { "TagTypeId", "CaveId" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_BiologyTag",
                table: "BiologyTag",
                columns: new[] { "TagTypeId", "CaveId" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_ArcheologyTag",
                table: "ArcheologyTag",
                columns: new[] { "TagTypeId", "CaveId" });

            migrationBuilder.AddForeignKey(
                name: "FK_ArcheologyTag_Caves_CaveId",
                table: "ArcheologyTag",
                column: "CaveId",
                principalTable: "Caves",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ArcheologyTag_TagTypes_TagTypeId",
                table: "ArcheologyTag",
                column: "TagTypeId",
                principalTable: "TagTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ArcheologyTag_Users_CreatedByUserId",
                table: "ArcheologyTag",
                column: "CreatedByUserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ArcheologyTag_Users_ModifiedByUserId",
                table: "ArcheologyTag",
                column: "ModifiedByUserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_BiologyTag_Caves_CaveId",
                table: "BiologyTag",
                column: "CaveId",
                principalTable: "Caves",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_BiologyTag_TagTypes_TagTypeId",
                table: "BiologyTag",
                column: "TagTypeId",
                principalTable: "TagTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_BiologyTag_Users_CreatedByUserId",
                table: "BiologyTag",
                column: "CreatedByUserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_BiologyTag_Users_ModifiedByUserId",
                table: "BiologyTag",
                column: "ModifiedByUserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_CartographerNameTag_Caves_CaveId",
                table: "CartographerNameTag",
                column: "CaveId",
                principalTable: "Caves",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CartographerNameTag_TagTypes_TagTypeId",
                table: "CartographerNameTag",
                column: "TagTypeId",
                principalTable: "TagTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CartographerNameTag_Users_CreatedByUserId",
                table: "CartographerNameTag",
                column: "CreatedByUserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_CartographerNameTag_Users_ModifiedByUserId",
                table: "CartographerNameTag",
                column: "ModifiedByUserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_CaveOtherTag_Caves_CaveId",
                table: "CaveOtherTag",
                column: "CaveId",
                principalTable: "Caves",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CaveOtherTag_TagTypes_TagTypeId",
                table: "CaveOtherTag",
                column: "TagTypeId",
                principalTable: "TagTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CaveOtherTag_Users_CreatedByUserId",
                table: "CaveOtherTag",
                column: "CreatedByUserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_CaveOtherTag_Users_ModifiedByUserId",
                table: "CaveOtherTag",
                column: "ModifiedByUserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_GeologicAgeTag_Caves_CaveId",
                table: "GeologicAgeTag",
                column: "CaveId",
                principalTable: "Caves",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_GeologicAgeTag_TagTypes_TagTypeId",
                table: "GeologicAgeTag",
                column: "TagTypeId",
                principalTable: "TagTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_GeologicAgeTag_Users_CreatedByUserId",
                table: "GeologicAgeTag",
                column: "CreatedByUserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_GeologicAgeTag_Users_ModifiedByUserId",
                table: "GeologicAgeTag",
                column: "ModifiedByUserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_MapStatusTag_Caves_CaveId",
                table: "MapStatusTag",
                column: "CaveId",
                principalTable: "Caves",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MapStatusTag_TagTypes_TagTypeId",
                table: "MapStatusTag",
                column: "TagTypeId",
                principalTable: "TagTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MapStatusTag_Users_CreatedByUserId",
                table: "MapStatusTag",
                column: "CreatedByUserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_MapStatusTag_Users_ModifiedByUserId",
                table: "MapStatusTag",
                column: "ModifiedByUserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PhysiographicProvinceTag_Caves_CaveId",
                table: "PhysiographicProvinceTag",
                column: "CaveId",
                principalTable: "Caves",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PhysiographicProvinceTag_TagTypes_TagTypeId",
                table: "PhysiographicProvinceTag",
                column: "TagTypeId",
                principalTable: "TagTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PhysiographicProvinceTag_Users_CreatedByUserId",
                table: "PhysiographicProvinceTag",
                column: "CreatedByUserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PhysiographicProvinceTag_Users_ModifiedByUserId",
                table: "PhysiographicProvinceTag",
                column: "ModifiedByUserId",
                principalTable: "Users",
                principalColumn: "Id");
        }
    }
}
