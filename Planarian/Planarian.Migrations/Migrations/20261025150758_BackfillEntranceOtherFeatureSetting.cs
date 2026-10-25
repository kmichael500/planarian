using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planarian.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class BackfillEntranceOtherFeatureSetting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Track exact rows inserted by this migration. The deterministic FeatureSetting
            // ID alone is not provenance: a preexisting setting can legitimately already
            // have that ID, in which case Up must leave it alone and Down must not delete it.
            migrationBuilder.CreateTable(
                name: "__BackfillEntranceOtherFeatureSettingRows",
                columns: table => new
                {
                    AccountId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    FeatureSettingId = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK___BackfillEntranceOtherFeatureSettingRows", x => x.AccountId);
                });

            migrationBuilder.Sql("""
                WITH inserted AS (
                    INSERT INTO "FeatureSettings" ("Id", "AccountId", "Key", "IsEnabled", "IsDefault", "CreatedOn")
                    SELECT substr(md5(account."Id" || ':EnabledFieldEntranceOtherTags'), 1, 10),
                           account."Id", 'EnabledFieldEntranceOtherTags', true, false, NOW()
                    FROM "Accounts" AS account
                    WHERE NOT EXISTS (
                        SELECT 1 FROM "FeatureSettings" AS setting
                        WHERE setting."AccountId" = account."Id"
                          AND setting."Key" = 'EnabledFieldEntranceOtherTags')
                    RETURNING "Id", "AccountId"
                )
                INSERT INTO "__BackfillEntranceOtherFeatureSettingRows" ("AccountId", "FeatureSettingId")
                SELECT "AccountId", "Id"
                FROM inserted;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM "FeatureSettings" AS setting
                USING "__BackfillEntranceOtherFeatureSettingRows" AS inserted
                WHERE setting."AccountId" = inserted."AccountId"
                  AND setting."Id" = inserted."FeatureSettingId"
                  AND setting."Key" = 'EnabledFieldEntranceOtherTags';
                """);

            migrationBuilder.DropTable(
                name: "__BackfillEntranceOtherFeatureSettingRows");
        }
    }
}
