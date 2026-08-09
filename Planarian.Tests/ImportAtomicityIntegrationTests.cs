using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NetTopologySuite.Geometries;
using Planarian.Model.Database.Entities;
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
    public async Task CaveSecondRecordLateFailureRollsBackCompleteState()
    {
        await using var d=await fixture.CreateDatabaseAsync(nameof(CaveSecondRecordLateFailureRollsBackCompleteState));var t=await IntegrationTestData.SeedTenantAsync(d,'a');CaveImportPlan plan;
        await using(var pctx=d.CreateDbContext("a",t.AccountId)){var p=new CaveImportPlanner(pctx,pctx.RequestUser);await using var csv=ImportDryRunIntegrationTests.CsvStream(ImportDryRunIntegrationTests.CaveHeader+"\nRecord A,New County,NEW,50,AA,,,,100,20,5,1,Rollback Geology,,,,,,,false,,A\nRecord B,New County,NEW,51,AA,,,,110,21,6,2,Rollback Geology,,,,,,,false,,B\n");plan=await p.PlanAsync(csv,false);Assert.Equal(2,plan.Caves.Count);Assert.Contains(plan.TagCreations,x=>x.Name=="Rollback Geology");Assert.Single(plan.CountyCreations);}
        var before=await NormalizedDatabaseState.CaptureAllAsync(d);var fault=new FailAtRevisionPublicationInterceptor();
        await using(var db=d.CreateDbContext("a",t.AccountId,fault)){var r=new CavePublishedSnapshotReader(db,db.RequestUser);var error=await Assert.ThrowsAsync<DbUpdateException>(()=>new CaveImportExecutor(db,db.RequestUser,r,new ImportRevisionPublisher(db,db.RequestUser)).ExecuteAsync(plan,"rollback-cave.csv"));Assert.IsType<InvalidOperationException>(error.InnerException);Assert.Contains("deterministic late failure",error.InnerException!.Message);}
        Assert.True(fault.SawCaveWriteBeforeFailure);Assert.Equal(before,await NormalizedDatabaseState.CaptureAllAsync(d));
    }

    [Fact]
    public async Task EntranceSecondRecordLateFailureRollsBackCompleteState()
    {
        await using var d=await fixture.CreateDatabaseAsync(nameof(EntranceSecondRecordLateFailureRollsBackCompleteState));var t=await IntegrationTestData.SeedTenantAsync(d,'a');
        await using(var seed=d.CreateDbContext("a",t.AccountId)){var second=new Cave{Id="secondcav0",AccountId=t.AccountId,StateId=t.StateId,CountyId=t.CountyId,CountyNumber=2,Name="Second",IsArchived=false};seed.Caves.Add(second);seed.TagTypes.Add(new TagType("Survey Grade",TagTypeKeyConstant.LocationQuality){Id=IdGenerator.Generate(),AccountId=t.AccountId,IsDefault=false});await seed.SaveChangesAsync();}
        EntranceImportPlan plan;await using(var pctx=d.CreateDbContext("a",t.AccountId)){var p=new EntranceImportPlanner(pctx,pctx.RequestUser);await using var csv=ImportDryRunIntegrationTests.CsvStream(ImportDryRunIntegrationTests.EntranceHeader+"\nRecord A,A01,1,true,35,-86,500,Survey Grade,1,Rollback Status,,,,,,A\nRecord B,A01,2,true,35.1,-86.1,510,Survey Grade,2,Rollback Status,,,,,,B\n");plan=await p.PlanAsync(csv,false);Assert.Equal(2,plan.Entrances.Count);Assert.Contains(plan.TagCreations,x=>x.Name=="Rollback Status");}
        var before=await NormalizedDatabaseState.CaptureAllAsync(d);var fault=new FailAtRevisionPublicationInterceptor();
        await using(var db=d.CreateDbContext("a",t.AccountId,fault)){var r=new CavePublishedSnapshotReader(db,db.RequestUser);var error=await Assert.ThrowsAsync<DbUpdateException>(()=>new EntranceImportExecutor(db,db.RequestUser,r,new ImportRevisionPublisher(db,db.RequestUser)).ExecuteAsync(plan,"rollback-ent.csv"));Assert.IsType<InvalidOperationException>(error.InnerException);Assert.Contains("deterministic late failure",error.InnerException!.Message);}
        Assert.True(fault.SawEntranceWriteBeforeFailure);Assert.Equal(before,await NormalizedDatabaseState.CaptureAllAsync(d));
    }
}

/// <summary>Fails after imported relational rows have been written, immediately before revision publication.</summary>
internal sealed class FailAtRevisionPublicationInterceptor:DbCommandInterceptor
{
    public bool SawCaveWriteBeforeFailure{get;private set;}
    public bool SawEntranceWriteBeforeFailure{get;private set;}

    private void Inspect(DbCommand command)
    {
        var sql=command.CommandText;
        if(sql.Contains("INSERT INTO \"Caves\"",StringComparison.Ordinal))SawCaveWriteBeforeFailure=true;
        if(sql.Contains("INSERT INTO \"Entrances\"",StringComparison.Ordinal))SawEntranceWriteBeforeFailure=true;
        if(sql.Contains("INSERT INTO \"CaveRevisions\"",StringComparison.Ordinal))throw new InvalidOperationException("Injected deterministic late failure at revision publication.");
    }

    public override InterceptionResult<int> NonQueryExecuting(DbCommand command,CommandEventData eventData,InterceptionResult<int> result){Inspect(command);return result;}
    public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command,CommandEventData eventData,InterceptionResult<DbDataReader> result){Inspect(command);return result;}
    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,CommandEventData eventData,InterceptionResult<int> result,CancellationToken cancellationToken=default){Inspect(command);return ValueTask.FromResult(result);}
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,CommandEventData eventData,InterceptionResult<DbDataReader> result,CancellationToken cancellationToken=default){Inspect(command);return ValueTask.FromResult(result);}
}
