using Npgsql;

namespace Planarian.Tests;

/// <summary>
/// Purpose-specific SQL for the exact historical v29 schema. Current EF models
/// must not arrange data that predates their own columns and constraints.
/// </summary>
internal static class V29DatabaseSeeder
{
    public static async Task SeedRepresentativeTenantAsync(PostgresTestDatabase database)
    {
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            insert into "States"("Id","Name","Abbreviation","CreatedOn") values('mainstate1','Tennessee','TN',now());
            insert into "Accounts"("Id","Name","CountyIdDelimiter","DefaultViewAccessAllCaves","ExportEnabled","CreatedOn") values('mainacct01','Existing Main Account','-',false,true,now());
            insert into "AccountStates"("Id","AccountId","StateId","CreatedOn") values('mainastate','mainacct01','mainstate1',now());
            insert into "Counties"("Id","AccountId","StateId","DisplayId","Name","CreatedOn") values('maincnty01','mainacct01','mainstate1','MAIN','Existing County',now());
            insert into "Caves"("Id","AccountId","StateId","CountyId","Name","AlternateNames","CountyNumber","IsArchived","CreatedOn") values('maincave01','mainacct01','mainstate1','maincnty01','Existing Main Cave','[]',42,false,now());
            insert into "TagTypes"("Id","AccountId","Key","Name","IsDefault","CreatedOn") values
              ('mainfile01','mainacct01','File','Existing file',false,now()),
              ('maingeo001','mainacct01','Geology','Limestone',false,now()),
              ('mainqual01','mainacct01','LocationQuality','Survey Grade',false,now()),
              ('mainstat01','mainacct01','EntranceStatus','Open',false,now());
            insert into "GeologyTags"("Id","TagTypeId","CaveId","CreatedOn") values('maingeot01','maingeo001','maincave01',now());
            insert into "Entrances"("Id","CaveId","LocationQualityTagId","Name","IsPrimary","Description","Location","ReportedOn","PitDepthFeet","CreatedOn") values
              ('mainentr01','maincave01','mainqual01','Historic Entrance',true,'Preserved entrance',ST_SetSRID(ST_MakePoint(-86.25,35.15,612),4326),'2025-04-03',18,now());
            insert into "EntranceStatusTags"("Id","TagTypeId","EntranceId","CreatedOn") values('mainenst01','mainstat01','mainentr01',now());
            insert into "Files"("Id","AccountId","CaveId","FileTypeTagId","FileName","BlobKey","BlobContainer","CreatedOn") values
              ('mainfiler1','mainacct01','maincave01','mainfile01','existing-cave.pdf','caves/maincave01/files/mainfiler1.pdf','main',now()),
              ('mainfilet1','mainacct01',null,'mainfile01','temporary.csv','temp/import/caves/mainfilet1.csv','main',now());
            """;
        await command.ExecuteNonQueryAsync();
    }

    public static async Task SeedFileOwnershipScenarioAsync(PostgresTestDatabase database, string fileId,
        string? accountId, string? caveId, string fileName, string blobKey)
    {
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            insert into "States"("Id","Name","Abbreviation","CreatedOn") values('legstate','Legacy State','LS',now()) on conflict do nothing;
            insert into "Accounts"("Id","Name","CountyIdDelimiter","DefaultViewAccessAllCaves","ExportEnabled","CreatedOn") values('legacyacct','Legacy Account','-',false,true,now()) on conflict do nothing;
            insert into "Counties"("Id","AccountId","StateId","DisplayId","Name","CreatedOn") values('legcounty','legacyacct','legstate','LEG','Legacy County',now()) on conflict do nothing;
            insert into "Caves"("Id","AccountId","StateId","CountyId","Name","AlternateNames","CountyNumber","IsArchived","CreatedOn") values('legcave01','legacyacct','legstate','legcounty','Legacy Cave','[]',1,false,now()) on conflict do nothing;
            insert into "TagTypes"("Id","AccountId","Key","Name","IsDefault","CreatedOn") values('legacyfile','legacyacct','file','Legacy file',false,now()) on conflict do nothing;
            insert into "Files"("Id","AccountId","CaveId","FileTypeTagId","FileName","BlobKey","BlobContainer","CreatedOn") values(@id,@account,@cave,'legacyfile',@name,@blob,'legacy',now())
            """;
        command.Parameters.AddWithValue("id", fileId);
        command.Parameters.AddWithValue("account", (object?)accountId ?? DBNull.Value);
        command.Parameters.AddWithValue("cave", (object?)caveId ?? DBNull.Value);
        command.Parameters.AddWithValue("name", fileName);
        command.Parameters.AddWithValue("blob", blobKey);
        await command.ExecuteNonQueryAsync();
    }
}
