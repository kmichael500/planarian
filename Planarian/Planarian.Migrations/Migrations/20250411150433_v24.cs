using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Planarian.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class v24 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
            CREATE OR REPLACE FUNCTION public.ts_headline_simple(
                config_text text,
                doc         text,
                query_text  text,
                opts_text   text DEFAULT ''
            ) RETURNS text
                LANGUAGE sql STABLE PARALLEL SAFE AS
            $$
            SELECT ts_headline(
                           config_text::regconfig,
                           doc,
                           websearch_to_tsquery(config_text::regconfig, query_text),  -- 👈 new
                           opts_text
                   );
            $$;
            ");

            migrationBuilder.Sql(@"
             CREATE OR REPLACE FUNCTION public.fts_matches_websearch(
                config_text text,          -- 'english'
                doc         text,          -- narrative column
                query_text  text           -- user input
            ) RETURNS boolean
                LANGUAGE sql IMMUTABLE PARALLEL SAFE AS
            $$
            SELECT to_tsvector(config_text::regconfig, doc) @@
                   websearch_to_tsquery(config_text::regconfig, query_text);
            $$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                @"DROP FUNCTION IF EXISTS public.ts_headline_simple(text,text,text,text);");
            migrationBuilder.Sql(
                @"DROP FUNCTION IF EXISTS public.fts_matches_websearch(text,text,text);");

        }
    }
}
