using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;

#nullable disable

namespace Planarian.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class v33_Search : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,")
                .Annotation("Npgsql:PostgresExtension:postgis", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:postgis", ",,");

            migrationBuilder.CreateTable(
                name: "CaveSearchDocuments",
                columns: table => new
                {
                    CaveId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    DisplayId = table.Column<string>(type: "text", nullable: false),
                    NameSearchText = table.Column<string>(type: "text", nullable: false),
                    SnippetText = table.Column<string>(type: "text", nullable: false),
                    PhraseVector = table.Column<NpgsqlTsVector>(type: "tsvector", nullable: false),
                    SearchVector = table.Column<NpgsqlTsVector>(type: "tsvector", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaveSearchDocuments", x => x.CaveId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CaveSearchDocuments_DisplayId",
                table: "CaveSearchDocuments",
                column: "DisplayId")
                .Annotation("Npgsql:IndexMethod", "GIN")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_CaveSearchDocuments_NameSearchText",
                table: "CaveSearchDocuments",
                column: "NameSearchText")
                .Annotation("Npgsql:IndexMethod", "GIN")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_CaveSearchDocuments_SearchVector",
                table: "CaveSearchDocuments",
                column: "SearchVector")
                .Annotation("Npgsql:IndexMethod", "GIN");
            // CaveSearchDocument
            migrationBuilder.Sql("""
            DROP VIEW IF EXISTS "CaveSearchDocument";

            CREATE OR REPLACE FUNCTION "RefreshCaveSearchDocument"(p_cave_id text DEFAULT NULL)
            RETURNS void
            LANGUAGE plpgsql
            AS $$
            BEGIN
                IF p_cave_id IS NULL THEN
                    DELETE FROM "CaveSearchDocuments";
                ELSE
                    DELETE FROM "CaveSearchDocuments"
                    WHERE "CaveId" = p_cave_id;
                END IF;

                INSERT INTO "CaveSearchDocuments"
                (
                    "CaveId",
                    "Name",
                    "DisplayId",
                    "NameSearchText",
                    "SnippetText",
                    "PhraseVector",
                    "SearchVector"
                )
                WITH base_caves AS (
                    SELECT c."Id" AS "CaveId",
                           c."Name",
                           c."AlternateNames",
                           c."Narrative",
                           county."Name" AS "CountyName",
                           county."DisplayId" AS "CountyCode",
                           state."Name" AS "StateName",
                           state."Abbreviation" AS "StateAbbreviation",
                           concat(county."DisplayId", COALESCE(account."CountyIdDelimiter", ''), c."CountyNumber") AS "DisplayId"
                    FROM "Caves" c
                    JOIN "Counties" county ON county."Id" = c."CountyId"
                    JOIN "States" state ON state."Id" = c."StateId"
                    JOIN "Accounts" account ON account."Id" = c."AccountId"
                    WHERE p_cave_id IS NULL OR c."Id" = p_cave_id
                ),
                search_parts AS (
                    SELECT b."CaveId", 'A'::text AS "Weight", 'ID'::text AS "Label", b."DisplayId"::text AS "Value"
                    FROM base_caves b
                    UNION ALL
                    SELECT b."CaveId", 'A', 'Name', b."Name"
                    FROM base_caves b
                    UNION ALL
                    SELECT b."CaveId", 'A', 'Alternate Name', alternate_name.value
                    FROM base_caves b
                    CROSS JOIN LATERAL jsonb_array_elements_text(
                        COALESCE(NULLIF(b."AlternateNames", ''), '[]')::jsonb
                    ) AS alternate_name(value)

                    UNION ALL
                    SELECT b."CaveId", 'B', 'Geology', tt."Name"
                    FROM base_caves b
                    JOIN "GeologyTags" t ON t."CaveId" = b."CaveId"
                    JOIN "TagTypes" tt ON tt."Id" = t."TagTypeId"
                    UNION ALL
                    SELECT b."CaveId", 'B', 'Geologic Age', tt."Name"
                    FROM base_caves b
                    JOIN "GeologicAgeTags" t ON t."CaveId" = b."CaveId"
                    JOIN "TagTypes" tt ON tt."Id" = t."TagTypeId"
                    UNION ALL
                    SELECT b."CaveId", 'B', 'Physiographic Province', tt."Name"
                    FROM base_caves b
                    JOIN "PhysiographicProvinceTags" t ON t."CaveId" = b."CaveId"
                    JOIN "TagTypes" tt ON tt."Id" = t."TagTypeId"
                    UNION ALL
                    SELECT b."CaveId", 'B', 'Biology', tt."Name"
                    FROM base_caves b
                    JOIN "BiologyTags" t ON t."CaveId" = b."CaveId"
                    JOIN "TagTypes" tt ON tt."Id" = t."TagTypeId"
                    UNION ALL
                    SELECT b."CaveId", 'B', 'Archaeology', tt."Name"
                    FROM base_caves b
                    JOIN "ArcheologyTags" t ON t."CaveId" = b."CaveId"
                    JOIN "TagTypes" tt ON tt."Id" = t."TagTypeId"
                    UNION ALL
                    SELECT b."CaveId", 'B', 'Map Status', tt."Name"
                    FROM base_caves b
                    JOIN "MapStatusTags" t ON t."CaveId" = b."CaveId"
                    JOIN "TagTypes" tt ON tt."Id" = t."TagTypeId"
                    UNION ALL
                    SELECT b."CaveId", 'B', 'Cartographer', tt."Name"
                    FROM base_caves b
                    JOIN "CartographerNameTags" t ON t."CaveId" = b."CaveId"
                    JOIN "TagTypes" tt ON tt."Id" = t."TagTypeId"
                    UNION ALL
                    SELECT b."CaveId", 'B', 'Reported By', tt."Name"
                    FROM base_caves b
                    JOIN "CaveReportedByNameTags" t ON t."CaveId" = b."CaveId"
                    JOIN "TagTypes" tt ON tt."Id" = t."TagTypeId"
                    UNION ALL
                    SELECT b."CaveId", 'B', 'Other Tag', tt."Name"
                    FROM base_caves b
                    JOIN "CaveOtherTags" t ON t."CaveId" = b."CaveId"
                    JOIN "TagTypes" tt ON tt."Id" = t."TagTypeId"

                    UNION ALL
                    SELECT b."CaveId", 'B', 'Entrance Name', e."Name"
                    FROM base_caves b
                    JOIN "Entrances" e ON e."CaveId" = b."CaveId"
                    UNION ALL
                    SELECT b."CaveId", 'B', 'Location Quality', tt."Name"
                    FROM base_caves b
                    JOIN "Entrances" e ON e."CaveId" = b."CaveId"
                    JOIN "TagTypes" tt ON tt."Id" = e."LocationQualityTagId"
                    UNION ALL
                    SELECT b."CaveId", 'B', 'Entrance Status', tt."Name"
                    FROM base_caves b
                    JOIN "Entrances" e ON e."CaveId" = b."CaveId"
                    JOIN "EntranceStatusTags" t ON t."EntranceId" = e."Id"
                    JOIN "TagTypes" tt ON tt."Id" = t."TagTypeId"
                    UNION ALL
                    SELECT b."CaveId", 'B', 'Field Indication', tt."Name"
                    FROM base_caves b
                    JOIN "Entrances" e ON e."CaveId" = b."CaveId"
                    JOIN "FieldIndicationTags" t ON t."EntranceId" = e."Id"
                    JOIN "TagTypes" tt ON tt."Id" = t."TagTypeId"
                    UNION ALL
                    SELECT b."CaveId", 'B', 'Entrance Hydrology', tt."Name"
                    FROM base_caves b
                    JOIN "Entrances" e ON e."CaveId" = b."CaveId"
                    JOIN "EntranceHydrologyTags" t ON t."EntranceId" = e."Id"
                    JOIN "TagTypes" tt ON tt."Id" = t."TagTypeId"
                    UNION ALL
                    SELECT b."CaveId", 'B', 'Entrance Reported By', tt."Name"
                    FROM base_caves b
                    JOIN "Entrances" e ON e."CaveId" = b."CaveId"
                    JOIN "EntranceReportedByNameTags" t ON t."EntranceId" = e."Id"
                    JOIN "TagTypes" tt ON tt."Id" = t."TagTypeId"
                    UNION ALL
                    SELECT b."CaveId", 'B', 'Entrance Other', tt."Name"
                    FROM base_caves b
                    JOIN "Entrances" e ON e."CaveId" = b."CaveId"
                    JOIN "EntranceOtherTag" t ON t."EntranceId" = e."Id"
                    JOIN "TagTypes" tt ON tt."Id" = t."TagTypeId"

                    UNION ALL
                    SELECT b."CaveId", 'C', 'County', b."CountyName"
                    FROM base_caves b
                    UNION ALL
                    SELECT b."CaveId", 'C', 'County Code', b."CountyCode"
                    FROM base_caves b
                    UNION ALL
                    SELECT b."CaveId", 'C', 'State', b."StateName"
                    FROM base_caves b
                    UNION ALL
                    SELECT b."CaveId", 'C', 'State Abbreviation', b."StateAbbreviation"
                    FROM base_caves b
                    UNION ALL
                    SELECT b."CaveId", 'C', 'File Display Name', f."DisplayName"
                    FROM base_caves b
                    JOIN "Files" f ON f."CaveId" = b."CaveId"
                    UNION ALL
                    SELECT b."CaveId", 'C', 'File Name', f."FileName"
                    FROM base_caves b
                    JOIN "Files" f ON f."CaveId" = b."CaveId"
                    UNION ALL
                    SELECT b."CaveId", 'C', 'File Type', tt."Name"
                    FROM base_caves b
                    JOIN "Files" f ON f."CaveId" = b."CaveId"
                    JOIN "TagTypes" tt ON tt."Id" = f."FileTypeTagId"

                    UNION ALL
                    SELECT b."CaveId", 'D', 'Narrative', b."Narrative"
                    FROM base_caves b
                    UNION ALL
                    SELECT b."CaveId", 'D', 'Entrance Description', e."Description"
                    FROM base_caves b
                    JOIN "Entrances" e ON e."CaveId" = b."CaveId"
                ),
                aggregated AS (
                    SELECT "CaveId",
                           string_agg(DISTINCT "Value", E'\n' ORDER BY "Value")
                               FILTER (WHERE "Weight" = 'A') AS "AText",
                           string_agg(DISTINCT "Value", E'\n' ORDER BY "Value")
                               FILTER (WHERE "Weight" = 'B') AS "BText",
                           string_agg(DISTINCT "Value", E'\n' ORDER BY "Value")
                               FILTER (WHERE "Weight" = 'C') AS "CText",
                           string_agg(DISTINCT "Value", E'\n' ORDER BY "Value")
                               FILTER (WHERE "Weight" = 'D') AS "DText",
                           string_agg(DISTINCT "Value", E'\n' ORDER BY "Value")
                               FILTER (WHERE "Label" IN ('Name', 'Alternate Name')) AS "NameSearchText",
                           string_agg(
                               DISTINCT "Value",
                               ' planariansearchboundary ' ORDER BY "Value"
                           ) AS "PhraseText",
                           string_agg(
                               DISTINCT "Label" || ': ' || "Value",
                               E'\n' ORDER BY "Label" || ': ' || "Value"
                           ) AS "SnippetText"
                    FROM search_parts
                    WHERE NULLIF(btrim("Value"), '') IS NOT NULL
                    GROUP BY "CaveId"
                )
                SELECT b."CaveId",
                       b."Name",
                       b."DisplayId",
                       COALESCE(a."NameSearchText", b."Name"),
                       COALESCE(a."SnippetText", ''),
                       to_tsvector('simple', COALESCE(a."PhraseText", '')),
                       setweight(to_tsvector('simple', COALESCE(a."AText", '')), 'A') ||
                       setweight(to_tsvector('simple', COALESCE(a."BText", '')), 'B') ||
                       setweight(to_tsvector('simple', COALESCE(a."CText", '')), 'C') ||
                       setweight(to_tsvector('simple', COALESCE(a."DText", '')), 'D')
                FROM base_caves b
                LEFT JOIN aggregated a ON a."CaveId" = b."CaveId";
            END;
            $$;

            CREATE OR REPLACE FUNCTION "RefreshCaveSearchDocumentDirectTrigger"()
            RETURNS trigger
            LANGUAGE plpgsql
            AS $$
            DECLARE
                old_cave_id text;
                new_cave_id text;
            BEGIN
                IF TG_OP <> 'INSERT' THEN
                    old_cave_id := to_jsonb(OLD) ->> TG_ARGV[0];
                END IF;

                IF TG_OP <> 'DELETE' THEN
                    new_cave_id := to_jsonb(NEW) ->> TG_ARGV[0];
                END IF;

                IF old_cave_id IS NOT NULL THEN
                    PERFORM "RefreshCaveSearchDocument"(old_cave_id);
                END IF;

                IF new_cave_id IS NOT NULL AND new_cave_id IS DISTINCT FROM old_cave_id THEN
                    PERFORM "RefreshCaveSearchDocument"(new_cave_id);
                END IF;

                IF TG_OP = 'DELETE' THEN
                    RETURN OLD;
                END IF;

                RETURN NEW;
            END;
            $$;

            CREATE OR REPLACE FUNCTION "RefreshCaveSearchDocumentEntranceTagTrigger"()
            RETURNS trigger
            LANGUAGE plpgsql
            AS $$
            DECLARE
                old_entrance_id text;
                new_entrance_id text;
                old_cave_id text;
                new_cave_id text;
            BEGIN
                IF TG_OP <> 'INSERT' THEN
                    old_entrance_id := OLD."EntranceId";
                    SELECT "CaveId" INTO old_cave_id
                    FROM "Entrances"
                    WHERE "Id" = old_entrance_id;
                END IF;

                IF TG_OP <> 'DELETE' THEN
                    new_entrance_id := NEW."EntranceId";
                    SELECT "CaveId" INTO new_cave_id
                    FROM "Entrances"
                    WHERE "Id" = new_entrance_id;
                END IF;

                IF old_cave_id IS NOT NULL THEN
                    PERFORM "RefreshCaveSearchDocument"(old_cave_id);
                END IF;

                IF new_cave_id IS NOT NULL AND new_cave_id IS DISTINCT FROM old_cave_id THEN
                    PERFORM "RefreshCaveSearchDocument"(new_cave_id);
                END IF;

                IF TG_OP = 'DELETE' THEN
                    RETURN OLD;
                END IF;

                RETURN NEW;
            END;
            $$;

            CREATE OR REPLACE FUNCTION "RefreshCaveSearchDocumentsForOwnerTrigger"()
            RETURNS trigger
            LANGUAGE plpgsql
            AS $$
            DECLARE
                cave_id text;
            BEGIN
                FOR cave_id IN EXECUTE format(
                    'SELECT "Id" FROM "Caves" WHERE %I = $1',
                    TG_ARGV[0]
                ) USING NEW."Id"
                LOOP
                    PERFORM "RefreshCaveSearchDocument"(cave_id);
                END LOOP;

                RETURN NEW;
            END;
            $$;

            CREATE OR REPLACE FUNCTION "RefreshCaveSearchDocumentsForTagTypeTrigger"()
            RETURNS trigger
            LANGUAGE plpgsql
            AS $$
            DECLARE
                cave_id text;
            BEGIN
                IF NEW."Name" IS NOT DISTINCT FROM OLD."Name" THEN
                    RETURN NEW;
                END IF;

                FOR cave_id IN
                    SELECT t."CaveId" FROM "GeologyTags" t WHERE t."TagTypeId" = NEW."Id"
                    UNION
                    SELECT t."CaveId" FROM "GeologicAgeTags" t WHERE t."TagTypeId" = NEW."Id"
                    UNION
                    SELECT t."CaveId" FROM "PhysiographicProvinceTags" t WHERE t."TagTypeId" = NEW."Id"
                    UNION
                    SELECT t."CaveId" FROM "BiologyTags" t WHERE t."TagTypeId" = NEW."Id"
                    UNION
                    SELECT t."CaveId" FROM "ArcheologyTags" t WHERE t."TagTypeId" = NEW."Id"
                    UNION
                    SELECT t."CaveId" FROM "MapStatusTags" t WHERE t."TagTypeId" = NEW."Id"
                    UNION
                    SELECT t."CaveId" FROM "CartographerNameTags" t WHERE t."TagTypeId" = NEW."Id"
                    UNION
                    SELECT t."CaveId" FROM "CaveReportedByNameTags" t WHERE t."TagTypeId" = NEW."Id"
                    UNION
                    SELECT t."CaveId" FROM "CaveOtherTags" t WHERE t."TagTypeId" = NEW."Id"
                    UNION
                    SELECT e."CaveId"
                    FROM "Entrances" e
                    WHERE e."LocationQualityTagId" = NEW."Id"
                    UNION
                    SELECT e."CaveId"
                    FROM "EntranceStatusTags" t
                    JOIN "Entrances" e ON e."Id" = t."EntranceId"
                    WHERE t."TagTypeId" = NEW."Id"
                    UNION
                    SELECT e."CaveId"
                    FROM "FieldIndicationTags" t
                    JOIN "Entrances" e ON e."Id" = t."EntranceId"
                    WHERE t."TagTypeId" = NEW."Id"
                    UNION
                    SELECT e."CaveId"
                    FROM "EntranceHydrologyTags" t
                    JOIN "Entrances" e ON e."Id" = t."EntranceId"
                    WHERE t."TagTypeId" = NEW."Id"
                    UNION
                    SELECT e."CaveId"
                    FROM "EntranceReportedByNameTags" t
                    JOIN "Entrances" e ON e."Id" = t."EntranceId"
                    WHERE t."TagTypeId" = NEW."Id"
                    UNION
                    SELECT e."CaveId"
                    FROM "EntranceOtherTag" t
                    JOIN "Entrances" e ON e."Id" = t."EntranceId"
                    WHERE t."TagTypeId" = NEW."Id"
                    UNION
                    SELECT f."CaveId"
                    FROM "Files" f
                    WHERE f."FileTypeTagId" = NEW."Id" AND f."CaveId" IS NOT NULL
                LOOP
                    PERFORM "RefreshCaveSearchDocument"(cave_id);
                END LOOP;

                RETURN NEW;
            END;
            $$;

            DROP TRIGGER IF EXISTS "CaveSearch_Caves" ON "Caves";
            CREATE TRIGGER "CaveSearch_Caves"
            AFTER INSERT OR DELETE OR UPDATE OF
                "Name", "AlternateNames", "Narrative", "CountyNumber", "CountyId", "StateId", "AccountId"
            ON "Caves"
            FOR EACH ROW
            EXECUTE FUNCTION "RefreshCaveSearchDocumentDirectTrigger"('Id');

            DROP TRIGGER IF EXISTS "CaveSearch_GeologyTags" ON "GeologyTags";
            CREATE TRIGGER "CaveSearch_GeologyTags"
            AFTER INSERT OR DELETE OR UPDATE OF "CaveId", "TagTypeId"
            ON "GeologyTags"
            FOR EACH ROW
            EXECUTE FUNCTION "RefreshCaveSearchDocumentDirectTrigger"('CaveId');

            DROP TRIGGER IF EXISTS "CaveSearch_GeologicAgeTags" ON "GeologicAgeTags";
            CREATE TRIGGER "CaveSearch_GeologicAgeTags"
            AFTER INSERT OR DELETE OR UPDATE OF "CaveId", "TagTypeId"
            ON "GeologicAgeTags"
            FOR EACH ROW
            EXECUTE FUNCTION "RefreshCaveSearchDocumentDirectTrigger"('CaveId');

            DROP TRIGGER IF EXISTS "CaveSearch_PhysiographicProvinceTags" ON "PhysiographicProvinceTags";
            CREATE TRIGGER "CaveSearch_PhysiographicProvinceTags"
            AFTER INSERT OR DELETE OR UPDATE OF "CaveId", "TagTypeId"
            ON "PhysiographicProvinceTags"
            FOR EACH ROW
            EXECUTE FUNCTION "RefreshCaveSearchDocumentDirectTrigger"('CaveId');

            DROP TRIGGER IF EXISTS "CaveSearch_BiologyTags" ON "BiologyTags";
            CREATE TRIGGER "CaveSearch_BiologyTags"
            AFTER INSERT OR DELETE OR UPDATE OF "CaveId", "TagTypeId"
            ON "BiologyTags"
            FOR EACH ROW
            EXECUTE FUNCTION "RefreshCaveSearchDocumentDirectTrigger"('CaveId');

            DROP TRIGGER IF EXISTS "CaveSearch_ArcheologyTags" ON "ArcheologyTags";
            CREATE TRIGGER "CaveSearch_ArcheologyTags"
            AFTER INSERT OR DELETE OR UPDATE OF "CaveId", "TagTypeId"
            ON "ArcheologyTags"
            FOR EACH ROW
            EXECUTE FUNCTION "RefreshCaveSearchDocumentDirectTrigger"('CaveId');

            DROP TRIGGER IF EXISTS "CaveSearch_MapStatusTags" ON "MapStatusTags";
            CREATE TRIGGER "CaveSearch_MapStatusTags"
            AFTER INSERT OR DELETE OR UPDATE OF "CaveId", "TagTypeId"
            ON "MapStatusTags"
            FOR EACH ROW
            EXECUTE FUNCTION "RefreshCaveSearchDocumentDirectTrigger"('CaveId');

            DROP TRIGGER IF EXISTS "CaveSearch_CartographerNameTags" ON "CartographerNameTags";
            CREATE TRIGGER "CaveSearch_CartographerNameTags"
            AFTER INSERT OR DELETE OR UPDATE OF "CaveId", "TagTypeId"
            ON "CartographerNameTags"
            FOR EACH ROW
            EXECUTE FUNCTION "RefreshCaveSearchDocumentDirectTrigger"('CaveId');

            DROP TRIGGER IF EXISTS "CaveSearch_CaveReportedByNameTags" ON "CaveReportedByNameTags";
            CREATE TRIGGER "CaveSearch_CaveReportedByNameTags"
            AFTER INSERT OR DELETE OR UPDATE OF "CaveId", "TagTypeId"
            ON "CaveReportedByNameTags"
            FOR EACH ROW
            EXECUTE FUNCTION "RefreshCaveSearchDocumentDirectTrigger"('CaveId');

            DROP TRIGGER IF EXISTS "CaveSearch_CaveOtherTags" ON "CaveOtherTags";
            CREATE TRIGGER "CaveSearch_CaveOtherTags"
            AFTER INSERT OR DELETE OR UPDATE OF "CaveId", "TagTypeId"
            ON "CaveOtherTags"
            FOR EACH ROW
            EXECUTE FUNCTION "RefreshCaveSearchDocumentDirectTrigger"('CaveId');

            DROP TRIGGER IF EXISTS "CaveSearch_Entrances" ON "Entrances";
            CREATE TRIGGER "CaveSearch_Entrances"
            AFTER INSERT OR DELETE OR UPDATE OF "CaveId", "Name", "Description", "LocationQualityTagId"
            ON "Entrances"
            FOR EACH ROW
            EXECUTE FUNCTION "RefreshCaveSearchDocumentDirectTrigger"('CaveId');

            DROP TRIGGER IF EXISTS "CaveSearch_EntranceStatusTags" ON "EntranceStatusTags";
            CREATE TRIGGER "CaveSearch_EntranceStatusTags"
            AFTER INSERT OR DELETE OR UPDATE OF "EntranceId", "TagTypeId"
            ON "EntranceStatusTags"
            FOR EACH ROW
            EXECUTE FUNCTION "RefreshCaveSearchDocumentEntranceTagTrigger"();

            DROP TRIGGER IF EXISTS "CaveSearch_FieldIndicationTags" ON "FieldIndicationTags";
            CREATE TRIGGER "CaveSearch_FieldIndicationTags"
            AFTER INSERT OR DELETE OR UPDATE OF "EntranceId", "TagTypeId"
            ON "FieldIndicationTags"
            FOR EACH ROW
            EXECUTE FUNCTION "RefreshCaveSearchDocumentEntranceTagTrigger"();

            DROP TRIGGER IF EXISTS "CaveSearch_EntranceHydrologyTags" ON "EntranceHydrologyTags";
            CREATE TRIGGER "CaveSearch_EntranceHydrologyTags"
            AFTER INSERT OR DELETE OR UPDATE OF "EntranceId", "TagTypeId"
            ON "EntranceHydrologyTags"
            FOR EACH ROW
            EXECUTE FUNCTION "RefreshCaveSearchDocumentEntranceTagTrigger"();

            DROP TRIGGER IF EXISTS "CaveSearch_EntranceReportedByNameTags" ON "EntranceReportedByNameTags";
            CREATE TRIGGER "CaveSearch_EntranceReportedByNameTags"
            AFTER INSERT OR DELETE OR UPDATE OF "EntranceId", "TagTypeId"
            ON "EntranceReportedByNameTags"
            FOR EACH ROW
            EXECUTE FUNCTION "RefreshCaveSearchDocumentEntranceTagTrigger"();

            DROP TRIGGER IF EXISTS "CaveSearch_EntranceOtherTag" ON "EntranceOtherTag";
            CREATE TRIGGER "CaveSearch_EntranceOtherTag"
            AFTER INSERT OR DELETE OR UPDATE OF "EntranceId", "TagTypeId"
            ON "EntranceOtherTag"
            FOR EACH ROW
            EXECUTE FUNCTION "RefreshCaveSearchDocumentEntranceTagTrigger"();

            DROP TRIGGER IF EXISTS "CaveSearch_Files" ON "Files";
            CREATE TRIGGER "CaveSearch_Files"
            AFTER INSERT OR DELETE OR UPDATE OF "CaveId", "DisplayName", "FileName", "FileTypeTagId"
            ON "Files"
            FOR EACH ROW
            EXECUTE FUNCTION "RefreshCaveSearchDocumentDirectTrigger"('CaveId');

            DROP TRIGGER IF EXISTS "CaveSearch_TagTypes" ON "TagTypes";
            CREATE TRIGGER "CaveSearch_TagTypes"
            AFTER UPDATE OF "Name"
            ON "TagTypes"
            FOR EACH ROW
            EXECUTE FUNCTION "RefreshCaveSearchDocumentsForTagTypeTrigger"();

            DROP TRIGGER IF EXISTS "CaveSearch_Counties" ON "Counties";
            CREATE TRIGGER "CaveSearch_Counties"
            AFTER UPDATE OF "Name", "DisplayId"
            ON "Counties"
            FOR EACH ROW
            EXECUTE FUNCTION "RefreshCaveSearchDocumentsForOwnerTrigger"('CountyId');

            DROP TRIGGER IF EXISTS "CaveSearch_States" ON "States";
            CREATE TRIGGER "CaveSearch_States"
            AFTER UPDATE OF "Name", "Abbreviation"
            ON "States"
            FOR EACH ROW
            EXECUTE FUNCTION "RefreshCaveSearchDocumentsForOwnerTrigger"('StateId');

            DROP TRIGGER IF EXISTS "CaveSearch_Accounts" ON "Accounts";
            CREATE TRIGGER "CaveSearch_Accounts"
            AFTER UPDATE OF "CountyIdDelimiter"
            ON "Accounts"
            FOR EACH ROW
            EXECUTE FUNCTION "RefreshCaveSearchDocumentsForOwnerTrigger"('AccountId');

            SELECT "RefreshCaveSearchDocument"(NULL);
            """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // CaveSearchDocument
            migrationBuilder.Sql("""
            DROP TRIGGER IF EXISTS "CaveSearch_Accounts" ON "Accounts";
            DROP TRIGGER IF EXISTS "CaveSearch_States" ON "States";
            DROP TRIGGER IF EXISTS "CaveSearch_Counties" ON "Counties";
            DROP TRIGGER IF EXISTS "CaveSearch_TagTypes" ON "TagTypes";
            DROP TRIGGER IF EXISTS "CaveSearch_Files" ON "Files";
            DROP TRIGGER IF EXISTS "CaveSearch_EntranceOtherTag" ON "EntranceOtherTag";
            DROP TRIGGER IF EXISTS "CaveSearch_EntranceReportedByNameTags" ON "EntranceReportedByNameTags";
            DROP TRIGGER IF EXISTS "CaveSearch_EntranceHydrologyTags" ON "EntranceHydrologyTags";
            DROP TRIGGER IF EXISTS "CaveSearch_FieldIndicationTags" ON "FieldIndicationTags";
            DROP TRIGGER IF EXISTS "CaveSearch_EntranceStatusTags" ON "EntranceStatusTags";
            DROP TRIGGER IF EXISTS "CaveSearch_Entrances" ON "Entrances";
            DROP TRIGGER IF EXISTS "CaveSearch_CaveOtherTags" ON "CaveOtherTags";
            DROP TRIGGER IF EXISTS "CaveSearch_CaveReportedByNameTags" ON "CaveReportedByNameTags";
            DROP TRIGGER IF EXISTS "CaveSearch_CartographerNameTags" ON "CartographerNameTags";
            DROP TRIGGER IF EXISTS "CaveSearch_MapStatusTags" ON "MapStatusTags";
            DROP TRIGGER IF EXISTS "CaveSearch_ArcheologyTags" ON "ArcheologyTags";
            DROP TRIGGER IF EXISTS "CaveSearch_BiologyTags" ON "BiologyTags";
            DROP TRIGGER IF EXISTS "CaveSearch_PhysiographicProvinceTags" ON "PhysiographicProvinceTags";
            DROP TRIGGER IF EXISTS "CaveSearch_GeologicAgeTags" ON "GeologicAgeTags";
            DROP TRIGGER IF EXISTS "CaveSearch_GeologyTags" ON "GeologyTags";
            DROP TRIGGER IF EXISTS "CaveSearch_Caves" ON "Caves";

            DROP FUNCTION IF EXISTS "RefreshCaveSearchDocumentsForTagTypeTrigger"();
            DROP FUNCTION IF EXISTS "RefreshCaveSearchDocumentsForOwnerTrigger"();
            DROP FUNCTION IF EXISTS "RefreshCaveSearchDocumentEntranceTagTrigger"();
            DROP FUNCTION IF EXISTS "RefreshCaveSearchDocumentDirectTrigger"();
            DROP FUNCTION IF EXISTS "RefreshCaveSearchDocument"(text);
            """);

            migrationBuilder.DropTable(
                name: "CaveSearchDocuments");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:postgis", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:pg_trgm", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:postgis", ",,");
        }
    }
}
