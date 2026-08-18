using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planarian.Migrations.Migrations
{
    public partial class FileNameAndExtension : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "DisplayName",
                table: "Files",
                newName: "Name");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Files",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Extension",
                table: "Files",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "Files"
                SET "Extension" = CASE
                        WHEN position('.' in reverse("FileName")) > 1
                             AND length("FileName") - position('.' in reverse("FileName")) + 1 > 1
                        THEN substring("FileName" from length("FileName") - position('.' in reverse("FileName")) + 1)
                        ELSE ''
                    END,
                    "Name" = CASE
                        WHEN NULLIF(btrim("Name"), '') IS NOT NULL THEN "Name"
                        WHEN position('.' in reverse("FileName")) > 1
                             AND length("FileName") - position('.' in reverse("FileName")) + 1 > 1
                        THEN substring("FileName" from 1 for length("FileName") - position('.' in reverse("FileName")))
                        ELSE "FileName"
                    END;
                """);

            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM "Files"
                        WHERE length("Name") + length("Extension") > 1000
                    ) THEN
                        RAISE EXCEPTION 'Cannot migrate Files: Name + Extension would exceed 1000 characters.';
                    END IF;
                    IF EXISTS (
                        SELECT 1 FROM "Files"
                        WHERE btrim("Name", ' ' || chr(9) || chr(10) || chr(11) || chr(12) || chr(13) ||
                            chr(133) || chr(160) || chr(5760) || chr(8192) || chr(8193) || chr(8194) ||
                            chr(8195) || chr(8196) || chr(8197) || chr(8198) || chr(8199) || chr(8200) ||
                            chr(8201) || chr(8202) || chr(8232) || chr(8233) || chr(8239) || chr(8287) ||
                            chr(12288)) = ''
                           OR "Name" IN ('.', '..')
                           OR "Name" LIKE '%/%'
                           OR "Name" LIKE '%\\%'
                           OR "Name" ~ '[[:cntrl:]]'
                    ) THEN
                        RAISE EXCEPTION 'Cannot migrate Files: legacy DisplayName contains a file name that is invalid under the new Name policy.';
                    END IF;
                    IF EXISTS (
                        SELECT 1 FROM "Files"
                        WHERE "Extension" <> '' AND (
                            length("Extension") = 1
                            OR left("Extension", 1) <> '.'
                            OR "Extension" LIKE '%/%'
                            OR "Extension" LIKE '%\\%'
                            OR "Extension" ~ '[[:cntrl:]]'
                        )
                    ) THEN
                        RAISE EXCEPTION 'Cannot migrate Files: legacy FileName contains an invalid file extension under the new Extension policy.';
                    END IF;
                END $$;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Files_ValidNameAndExtension",
                table: "Files",
                sql: """
                     length("Name") + length("Extension") <= 1000
                     AND btrim("Name", ' ' || chr(9) || chr(10) || chr(11) || chr(12) || chr(13) ||
                         chr(133) || chr(160) || chr(5760) || chr(8192) || chr(8193) || chr(8194) ||
                         chr(8195) || chr(8196) || chr(8197) || chr(8198) || chr(8199) || chr(8200) ||
                         chr(8201) || chr(8202) || chr(8232) || chr(8233) || chr(8239) || chr(8287) ||
                         chr(12288)) <> ''
                     AND "Name" NOT IN ('.', '..')
                     AND "Name" NOT LIKE '%/%'
                     AND "Name" NOT LIKE '%\\%'
                     AND "Name" !~ '[[:cntrl:]]'
                     AND (
                         "Extension" = '' OR (
                             length("Extension") > 1
                             AND left("Extension", 1) = '.'
                             AND "Extension" NOT LIKE '%/%'
                             AND "Extension" NOT LIKE '%\\%'
                             AND "Extension" !~ '[[:cntrl:]]'
                         )
                     )
                     """);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Files",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Extension",
                table: "Files",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "FileName",
                table: "Files");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "Files" WHERE length("Name") > 100) THEN
                        RAISE EXCEPTION 'Cannot roll back FileNameAndExtension: Name cannot fit the legacy DisplayName column.';
                    END IF;
                END $$;
                """);

            migrationBuilder.DropCheckConstraint(
                name: "CK_Files_ValidNameAndExtension",
                table: "Files");

            migrationBuilder.AddColumn<string>(
                name: "FileName",
                table: "Files",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("""UPDATE "Files" SET "FileName" = "Name" || "Extension";""");

            migrationBuilder.DropColumn(
                name: "Extension",
                table: "Files");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Files",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000);

            migrationBuilder.RenameColumn(
                name: "Name",
                table: "Files",
                newName: "DisplayName");
        }
    }
}
