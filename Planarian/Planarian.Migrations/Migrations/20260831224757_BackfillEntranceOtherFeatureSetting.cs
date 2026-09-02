using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planarian.Migrations.Migrations
{
    public partial class BackfillEntranceOtherFeatureSetting : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO "FeatureSettings" ("Id", "AccountId", "Key", "IsEnabled", "IsDefault", "CreatedOn")
                SELECT substr(md5(account."Id" || ':EnabledFieldEntranceOtherTags'), 1, 10),
                       account."Id", 'EnabledFieldEntranceOtherTags', true, false, NOW()
                FROM "Accounts" AS account
                WHERE NOT EXISTS (
                    SELECT 1 FROM "FeatureSettings" AS setting
                    WHERE setting."AccountId" = account."Id"
                      AND setting."Key" = 'EnabledFieldEntranceOtherTags');
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM "FeatureSettings" AS setting
                USING "Accounts" AS account
                WHERE setting."AccountId" = account."Id"
                  AND setting."Key" = 'EnabledFieldEntranceOtherTags'
                  AND setting."Id" = substr(md5(account."Id" || ':EnabledFieldEntranceOtherTags'), 1, 10);
                """);
        }
    }
}
