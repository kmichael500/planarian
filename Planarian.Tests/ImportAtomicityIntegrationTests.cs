using Microsoft.EntityFrameworkCore;
using Npgsql;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Helpers;
using Planarian.Modules.Caves.Revisions;
using Planarian.Modules.Import.Planning;
using Xunit;

namespace Planarian.Tests;

public sealed class ImportAtomicityIntegrationTests(PostgresIntegrationFixture fixture):IClassFixture<PostgresIntegrationFixture>
{
    [Fact]
    public async Task CaveLateFailureRollsBackBatchAndReferenceCreations()
    {
        await using var d=await fixture.CreateDatabaseAsync(nameof(CaveLateFailureRollsBackBatchAndReferenceCreations));var t=await IntegrationTestData.SeedTenantAsync(d,'a');CaveImportPlan plan;
        await using(var pctx=d.CreateDbContext("a",t.AccountId)){var p=new CaveImportPlanner(pctx,pctx.RequestUser);await using var csv=ImportDryRunIntegrationTests.CsvStream(ImportDryRunIntegrationTests.CaveHeader+"\nLate,County A,A01,50,AA,,,,100,20,5,1,Rollback Geology,,,,,,,false,,\n");plan=await p.PlanAsync(csv,false);Assert.Contains(plan.TagCreations,x=>x.Name=="Rollback Geology");}
        var row=Assert.Single(plan.Caves);await using(var cn=new NpgsqlConnection(d.ConnectionString)){await cn.OpenAsync();await using var c=cn.CreateCommand();c.CommandText="insert into \"Caves\"(\"Id\",\"AccountId\",\"StateId\",\"CountyId\",\"Name\",\"AlternateNames\",\"CountyNumber\",\"IsArchived\",\"CreatedOn\") values(@id,@a,@s,@co,'Duplicate','[]',999,false,now())";c.Parameters.AddWithValue("id",row.Id);c.Parameters.AddWithValue("a",t.AccountId);c.Parameters.AddWithValue("s",t.StateId);c.Parameters.AddWithValue("co",t.CountyId);await c.ExecuteNonQueryAsync();}
        await using(var db=d.CreateDbContext("a",t.AccountId)){var r=new CavePublishedSnapshotReader(db,db.RequestUser);await Assert.ThrowsAnyAsync<Exception>(()=>new CaveImportExecutor(db,db.RequestUser,r,new ImportRevisionPublisher(db,db.RequestUser)).ExecuteAsync(plan,"rollback-cave.csv"));}
        await using var verify=d.CreateDbContext("a",t.AccountId);Assert.False(await verify.CaveImportBatches.AnyAsync(b=>b.SourceFileName=="rollback-cave.csv"));Assert.False(await verify.TagTypes.AnyAsync(x=>x.AccountId==t.AccountId&&x.Name=="Rollback Geology"));
    }

    [Fact]
    public async Task EntranceLateFailureRollsBackBatchAndTagCreation()
    {
        await using var d=await fixture.CreateDatabaseAsync(nameof(EntranceLateFailureRollsBackBatchAndTagCreation));var t=await IntegrationTestData.SeedTenantAsync(d,'a');EntranceImportPlan plan;
        await using(var pctx=d.CreateDbContext("a",t.AccountId))
        {
            pctx.TagTypes.Add(new TagType("Survey Grade",TagTypeKeyConstant.LocationQuality){Id=IdGenerator.Generate(),AccountId=t.AccountId,IsDefault=false});
            await pctx.SaveChangesAsync();
            var p=new EntranceImportPlanner(pctx,pctx.RequestUser);await using var csv=ImportDryRunIntegrationTests.CsvStream(ImportDryRunIntegrationTests.EntranceHeader+"\nLate,A01,1,true,35,-86,500,Survey Grade,0,Rollback Status,,,,,,\n");plan=await p.PlanAsync(csv,false);Assert.Contains(plan.TagCreations,x=>x.Name=="Rollback Status");
        }
        var row=Assert.Single(plan.Entrances);await using(var cn=new NpgsqlConnection(d.ConnectionString)){await cn.OpenAsync();await using var c=cn.CreateCommand();c.CommandText="insert into \"Entrances\"(\"Id\",\"CaveId\",\"LocationQualityTagId\",\"Name\",\"IsPrimary\",\"Location\",\"CreatedOn\") values(@id,@c,@q,'Duplicate',true,ST_SetSRID(ST_MakePoint(-86,35,500),4326),now())";c.Parameters.AddWithValue("id",row.Id);c.Parameters.AddWithValue("c",t.CaveId);c.Parameters.AddWithValue("q",row.LocationQualityTagId);await c.ExecuteNonQueryAsync();}
        await using(var db=d.CreateDbContext("a",t.AccountId)){var r=new CavePublishedSnapshotReader(db,db.RequestUser);await Assert.ThrowsAnyAsync<Exception>(()=>new EntranceImportExecutor(db,db.RequestUser,r,new ImportRevisionPublisher(db,db.RequestUser)).ExecuteAsync(plan,"rollback-ent.csv"));}
        await using var verify=d.CreateDbContext("a",t.AccountId);Assert.False(await verify.CaveImportBatches.AnyAsync(b=>b.SourceFileName=="rollback-ent.csv"));Assert.False(await verify.TagTypes.AnyAsync(x=>x.AccountId==t.AccountId&&x.Name=="Rollback Status"));
    }
}
