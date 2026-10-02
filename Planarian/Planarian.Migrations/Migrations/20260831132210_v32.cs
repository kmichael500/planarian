using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planarian.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class v32 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // On main this migration widened Files.DisplayName. Cave revisions
            // replaced that legacy column with Name + Extension earlier in the
            // migration sequence, already at the wider limit. Keep v32 safe for
            // both histories so fresh and already-upgraded databases converge.
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM information_schema.columns
                        WHERE table_schema = current_schema()
                          AND table_name = 'Files'
                          AND column_name = 'DisplayName'
                    ) THEN
                        ALTER TABLE "Files"
                            ALTER COLUMN "DisplayName" TYPE character varying(1000);
                    END IF;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM information_schema.columns
                        WHERE table_schema = current_schema()
                          AND table_name = 'Files'
                          AND column_name = 'DisplayName'
                    ) THEN
                        ALTER TABLE "Files"
                            ALTER COLUMN "DisplayName" TYPE character varying(100);
                    END IF;
                END $$;
                """);
        }
    }
}
