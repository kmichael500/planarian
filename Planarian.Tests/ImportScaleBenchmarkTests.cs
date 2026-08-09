using System.Collections.Concurrent;
using System.Data.Common;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Planarian.Model.Database;
using Planarian.Model.Shared;
using Planarian.Modules.Caves.Revisions;
using Planarian.Modules.Import.Models;
using Planarian.Modules.Import.Planning;
using Xunit;
using Xunit.Abstractions;

namespace Planarian.Tests;

internal sealed record TimedSql(string Sql, TimeSpan Duration);
internal sealed class SqlTimingInterceptor : DbCommandInterceptor
{
    private readonly ConcurrentQueue<TimedSql> _items=new(); public IReadOnlyList<TimedSql> Items=>_items.ToArray();
    public void Reset(){while(_items.TryDequeue(out _)){} } private void Add(DbCommand c,CommandExecutedEventData e)=>_items.Enqueue(new(c.CommandText,e.Duration));
    public override DbDataReader ReaderExecuted(DbCommand c,CommandExecutedEventData e,DbDataReader r){Add(c,e);return r;}
    public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand c,CommandExecutedEventData e,DbDataReader r,CancellationToken t=default){Add(c,e);return ValueTask.FromResult(r);}
    public override int NonQueryExecuted(DbCommand c,CommandExecutedEventData e,int r){Add(c,e);return r;}
    public override ValueTask<int> NonQueryExecutedAsync(DbCommand c,CommandExecutedEventData e,int r,CancellationToken t=default){Add(c,e);return ValueTask.FromResult(r);}
}
internal sealed class SaveMetricsInterceptor:Microsoft.EntityFrameworkCore.Diagnostics.SaveChangesInterceptor
{
    public int Saves{get;private set;} public int TrackedHighWater{get;private set;} public void Reset(){Saves=0;TrackedHighWater=0;}
    private void Add(DbContext? c){Saves++;if(c!=null)TrackedHighWater=Math.Max(TrackedHighWater,c.ChangeTracker.Entries().Count());}
    public override InterceptionResult<int> SavingChanges(DbContextEventData e,InterceptionResult<int> r){Add(e.Context);return r;}
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData e,InterceptionResult<int> r,CancellationToken t=default){Add(e.Context);return ValueTask.FromResult(r);}
}

public sealed class ImportScaleBenchmarkTests(PostgresIntegrationFixture fixture,ITestOutputHelper output):IClassFixture<PostgresIntegrationFixture>
{
    private const int Count=10_000;
    [Fact]
    public async Task TenThousandCaveAndEntranceImportSyncWorkloadIsBoundedAndSemanticallyCorrect()
    {
        await using var d=await fixture.CreateDatabaseAsync(nameof(TenThousandCaveAndEntranceImportSyncWorkloadIsBoundedAndSemanticallyCorrect));const string account="benchacct1";await SeedAccount(d,account);
        var total=Stopwatch.StartNew();var memoryBefore=GC.GetTotalMemory(true);
        var initial=await RunCaves(d,account,"initial",BuildCaves(Enumerable.Range(1,Count),n=>false),false);Assert.Equal(Count,initial.Inserts);
        var entrances=await RunEntrances(d,account,"entrances",BuildEntrances(Enumerable.Range(1,Count),false),false);Assert.Equal(13_200,entrances.Rows);
        AssertCaveStructure(initial, maxCommands: 600, maxSelects: 400, maxWrites: 550, maxRevisionWrites: 50, maxSaves: 40, maxTracked: 40_000);
        AssertEntranceStructure(entrances, maxCommands: 250, maxSelects: 200, maxWrites: 200, maxRevisionWrites: 50, maxSaves: 40, maxTracked: 50_000);

        Dictionary<int,(uint Version,string? Revision)> before;int revisionsBefore;
        await using(var verify=d.CreateDbContext("bench",account))
        {
            Assert.Equal(Count,await verify.Caves.IgnoreQueryFilters().CountAsync(c=>c.AccountId==account));
            before=await verify.Caves.IgnoreQueryFilters().Where(c=>c.AccountId==account).AsNoTracking().ToDictionaryAsync(c=>c.CountyNumber,c=>(c.Version,c.CurrentRevisionId));
            revisionsBefore=await verify.CaveRevisions.CountAsync();
        }
        var mostly=await RunCaves(d,account,"mostly",BuildCaves(Enumerable.Range(1,Count),n=>n%20==0),true);Assert.Equal(500,mostly.Updates);Assert.Equal(9500,mostly.NoChange);
        AssertCaveStructure(mostly, maxCommands: 250, maxSelects: 150, maxWrites: 180, maxRevisionWrites: 20, maxSaves: 40, maxTracked: 40_000);
        await using(var verify=d.CreateDbContext("bench",account))
        {
            var after=await verify.Caves.IgnoreQueryFilters().Where(c=>c.AccountId==account).AsNoTracking().ToDictionaryAsync(c=>c.CountyNumber,c=>(c.Version,c.CurrentRevisionId));
            foreach(var pair in before.Where(p=>p.Key%20!=0)){Assert.Equal(pair.Value.Version,after[pair.Key].Version);Assert.Equal(pair.Value.Revision,after[pair.Key].CurrentRevisionId);}
            var changed=before.Where(p=>p.Key%20==0).ToList();Assert.Equal(500,changed.Count);foreach(var pair in changed){Assert.NotEqual(pair.Value.Version,after[pair.Key].Version);Assert.NotEqual(pair.Value.Revision,after[pair.Key].CurrentRevisionId);Assert.NotNull(after[pair.Key].CurrentRevisionId);}Assert.Equal(500,await verify.CaveRevisions.CountAsync()-revisionsBefore);
        }

        var churnNumbers=Enumerable.Range(101,9900).Concat(Enumerable.Range(10001,100));
        var churn=await RunCaves(d,account,"churn",BuildCaves(churnNumbers,n=>n%10==0,true),true);Assert.Equal(100,churn.Deletes);Assert.Equal(100,churn.Inserts);
        var replace=Enumerable.Range(101,900).Concat(Enumerable.Range(10001,100));
        var entranceChurn=await RunEntrances(d,account,"entrance-churn",BuildEntrances(replace,true),true);Assert.Equal(1000,entranceChurn.Targets);
        AssertCaveStructure(churn, maxCommands: 300, maxSelects: 200, maxWrites: 220, maxRevisionWrites: 25, maxSaves: 50, maxTracked: 40_000);
        AssertEntranceStructure(entranceChurn, maxCommands: 250, maxSelects: 150, maxWrites: 180, maxRevisionWrites: 25, maxSaves: 40, maxTracked: 30_000);
        await using(var verify=d.CreateDbContext("bench",account)){Assert.Equal(Count,await verify.Caves.IgnoreQueryFilters().CountAsync(c=>c.AccountId==account));var selected=await verify.Caves.IgnoreQueryFilters().Where(c=>c.AccountId==account&&replace.Contains(c.CountyNumber)).Select(c=>c.Id).ToListAsync();Assert.Equal(1000,await verify.Entrances.IgnoreQueryFilters().CountAsync(e=>selected.Contains(e.CaveId)));Assert.Equal(1000,await verify.Entrances.IgnoreQueryFilters().CountAsync(e=>selected.Contains(e.CaveId)&&e.IsPrimary));}
        total.Stop();var process=Process.GetCurrentProcess();process.Refresh();
        output.WriteLine("10K_IMPORT_BENCHMARK "+System.Text.Json.JsonSerializer.Serialize(new{initial,entrances,mostly,churn,entranceChurn,totalMs=total.Elapsed.TotalMilliseconds,managedMemoryDeltaBytes=GC.GetTotalMemory(false)-memoryBefore,peakWorkingSetBytes=process.PeakWorkingSet64 > 0 ? process.PeakWorkingSet64 : (long?)null}));
    }

    private async Task<CaveMetrics> RunCaves(PostgresTestDatabase d,string account,string phase,string text,bool sync)
    {
        var parse=Stopwatch.StartNew();var rows=ProbeCaves(text);parse.Stop();var sql=new SqlTimingInterceptor();var saves=new SaveMetricsInterceptor();await using var db=Context(d,account,sql,saves);var planner=new CaveImportPlanner(db,db.RequestUser);await using var csv=ImportDryRunIntegrationTests.CsvStream(text);var p=Stopwatch.StartNew();var plan=await planner.PlanAsync(csv,sync);p.Stop();sql.Reset();saves.Reset();var r=new CavePublishedSnapshotReader(db,db.RequestUser);var e=Stopwatch.StartNew();await new CaveImportExecutor(db,db.RequestUser,r,new ImportRevisionPublisher(db,db.RequestUser)).ExecuteAsync(plan,phase+".csv");e.Stop();var writes=sql.Items.Where(x=>IsWrite(x.Sql)).ToList();return new(phase,rows,parse.Elapsed.TotalMilliseconds,p.Elapsed.TotalMilliseconds,e.Elapsed.TotalMilliseconds,plan.Caves.Count(x=>x.Action==CaveImportAction.Insert),plan.Caves.Count(x=>x.Action==CaveImportAction.Update),plan.Caves.Count(x=>x.Action==CaveImportAction.NoChange),plan.Deletions.Count,sql.Items.Count,sql.Items.Count(x=>x.Sql.Contains("SELECT",StringComparison.OrdinalIgnoreCase)),writes.Count,writes.Count(x=>x.Sql.Contains("\"CaveRevisions\"",StringComparison.Ordinal)),writes.Sum(x=>x.Duration.TotalMilliseconds),writes.Where(x=>x.Sql.Contains("\"CaveRevisions\"",StringComparison.Ordinal)).Sum(x=>x.Duration.TotalMilliseconds),saves.Saves,saves.TrackedHighWater);
    }
    private async Task<EntranceMetrics> RunEntrances(PostgresTestDatabase d,string account,string phase,string text,bool sync)
    {
        var parse=Stopwatch.StartNew();var rows=ProbeEntrances(text);parse.Stop();var sql=new SqlTimingInterceptor();var saves=new SaveMetricsInterceptor();await using var db=Context(d,account,sql,saves);var planner=new EntranceImportPlanner(db,db.RequestUser);await using var csv=ImportDryRunIntegrationTests.CsvStream(text);var p=Stopwatch.StartNew();var plan=await planner.PlanAsync(csv,sync);p.Stop();sql.Reset();saves.Reset();var r=new CavePublishedSnapshotReader(db,db.RequestUser);var e=Stopwatch.StartNew();await new EntranceImportExecutor(db,db.RequestUser,r,new ImportRevisionPublisher(db,db.RequestUser)).ExecuteAsync(plan,phase+".csv");e.Stop();var writes=sql.Items.Where(x=>IsWrite(x.Sql)).ToList();return new(phase,rows,plan.Targets.Count,parse.Elapsed.TotalMilliseconds,p.Elapsed.TotalMilliseconds,e.Elapsed.TotalMilliseconds,sql.Items.Count,sql.Items.Count(x=>x.Sql.Contains("SELECT",StringComparison.OrdinalIgnoreCase)),writes.Count,writes.Count(x=>x.Sql.Contains("\"CaveRevisions\"",StringComparison.Ordinal)),writes.Sum(x=>x.Duration.TotalMilliseconds),writes.Where(x=>x.Sql.Contains("\"CaveRevisions\"",StringComparison.Ordinal)).Sum(x=>x.Duration.TotalMilliseconds),saves.Saves,saves.TrackedHighWater);
    }
    private static PlanarianDbContext Context(PostgresTestDatabase d,string account,SqlTimingInterceptor sql,SaveMetricsInterceptor saves){using(d.CreateDbContext("bench",account)){}var options=new DbContextOptionsBuilder<PlanarianDbContext>().UseNpgsql(d.ConnectionString,o=>{o.MigrationsAssembly("Planarian.Migrations");o.UseNetTopologySuite();o.MaxBatchSize(1000);}).AddInterceptors(sql,saves).Options;var db=new PlanarianDbContext(options);db.RequestUser=new RequestUser(db){Id="bench",AccountId=account,FirstName="Scale",LastName="Benchmark"};return db;}
    private static async Task SeedAccount(PostgresTestDatabase d,string account){await using var c=new Npgsql.NpgsqlConnection(d.ConnectionString);await c.OpenAsync();await using var q=c.CreateCommand();q.CommandText="insert into \"States\"(\"Id\",\"Name\",\"Abbreviation\",\"CreatedOn\") values('benchstate','Tennessee','TN',now());insert into \"Accounts\"(\"Id\",\"Name\",\"CountyIdDelimiter\",\"DefaultViewAccessAllCaves\",\"ExportEnabled\",\"CreatedOn\") values(@a,'10k Benchmark','-',false,true,now());";q.Parameters.AddWithValue("a",account);await q.ExecuteNonQueryAsync();}
    private static string BuildCaves(IEnumerable<int> numbers,Func<int,bool> changed,bool churn=false){var b=new StringBuilder(ImportDryRunIntegrationTests.CaveHeader).Append('\n');foreach(var n in numbers){var name=changed(n)?$"Benchmark Cave {n} updated":$"Benchmark Cave {n}";var geo=churn&&n%25==0?"Dolomite":"Limestone";b.Append(Csv(name,"Benchmark County","BEN",n,"TN",$"Alt {n}","Mapped","Mapper A,Mapper B",1000+n%500,100+n%200,20+n%100,2+n%5,geo,"Mississippian","Cumberland Plateau","Artifact","Bats","2026-08-01","Reporter A,Reporter B,Reporter C",false,"Interesting",$"Benchmark narrative {n} "+new string((char)('a'+n%26),180+n%160))).Append('\n');}return b.ToString();}
    private static string BuildEntrances(IEnumerable<int> numbers,bool replacement){var b=new StringBuilder(ImportDryRunIntegrationTests.EntranceHeader).Append('\n');foreach(var n in numbers){var count=replacement?1:EntranceCount(n);for(var i=0;i<count;i++)b.Append(Csv(replacement?$"Replacement {n}":$"Entrance {n}-{i+1}","BEN",n,i==0,35+n/100000d+i/1000000d,-86-n/100000d-i/1000000d,500+n%1000+i,replacement&&n%10==0?"Estimated":"Survey Grade",i*5,replacement&&n%10==0?"Restricted":"Open","Wet","Sink","2026-08-02","Reporter A,Reporter B",replacement?"Replacement entrance":"Benchmark entrance")).Append('\n');}return b.ToString();}
    private static int EntranceCount(int n)=>n<=8000?1:n<=9500?2:n<=9900?3:10;
    private static bool IsWrite(string sql)=>sql.Contains("INSERT",StringComparison.OrdinalIgnoreCase)||sql.Contains("UPDATE",StringComparison.OrdinalIgnoreCase)||sql.Contains("DELETE",StringComparison.OrdinalIgnoreCase);
    private static void AssertCaveStructure(CaveMetrics metrics,int maxCommands,int maxSelects,int maxWrites,int maxRevisionWrites,int maxSaves,int maxTracked)
    {
        Assert.InRange(metrics.Commands, 1, maxCommands);
        Assert.InRange(metrics.Selects, 1, maxSelects);
        Assert.InRange(metrics.WriteCommands, 1, maxWrites);
        Assert.InRange(metrics.RevisionWriteCommands, 1, maxRevisionWrites);
        Assert.InRange(metrics.SaveChanges, 1, maxSaves);
        Assert.InRange(metrics.TrackedHighWater, 1, maxTracked);
    }
    private static void AssertEntranceStructure(EntranceMetrics metrics,int maxCommands,int maxSelects,int maxWrites,int maxRevisionWrites,int maxSaves,int maxTracked)
    {
        Assert.InRange(metrics.Commands, 1, maxCommands);
        Assert.InRange(metrics.Selects, 1, maxSelects);
        Assert.InRange(metrics.WriteCommands, 1, maxWrites);
        Assert.InRange(metrics.RevisionWriteCommands, 1, maxRevisionWrites);
        Assert.InRange(metrics.SaveChanges, 1, maxSaves);
        Assert.InRange(metrics.TrackedHighWater, 1, maxTracked);
    }
    private static string Csv(params object?[] values)=>string.Join(',',values.Select(v=>{var s=v switch{null=>"",bool x=>x?"true":"false",IFormattable f=>f.ToString(null,CultureInfo.InvariantCulture),_=>v.ToString()??""};return s.Contains(',')||s.Contains('"')?'"'+s.Replace("\"","\"\"")+'"':s;}));
    private static int ProbeCaves(string text){using var s=ImportDryRunIntegrationTests.CsvStream(text);using var r=new StreamReader(s);using var c=new CsvReader(r,new CsvConfiguration(CultureInfo.InvariantCulture){MissingFieldFound=null});c.Context.RegisterClassMap<CaveCsvModelMap>();return c.GetRecords<CaveCsvModel>().Count();}
    private static int ProbeEntrances(string text){using var s=ImportDryRunIntegrationTests.CsvStream(text);using var r=new StreamReader(s);using var c=new CsvReader(r,new CsvConfiguration(CultureInfo.InvariantCulture){MissingFieldFound=null});c.Context.RegisterClassMap<EntranceCsvModelMap>();return c.GetRecords<EntranceCsvModel>().Count();}
    public sealed record CaveMetrics(string Phase,int Rows,double ParseProbeMs,double PlanningMs,double ExecutionMs,int Inserts,int Updates,int NoChange,int Deletes,int Commands,int Selects,int WriteCommands,int RevisionWriteCommands,double WriteSqlMs,double RevisionSqlMs,int SaveChanges,int TrackedHighWater);
    public sealed record EntranceMetrics(string Phase,int Rows,int Targets,double ParseProbeMs,double PlanningMs,double ExecutionMs,int Commands,int Selects,int WriteCommands,int RevisionWriteCommands,double WriteSqlMs,double RevisionSqlMs,int SaveChanges,int TrackedHighWater);
}
