using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using Planarian.Model.Database.Entities;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.Revisions;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Helpers;
using File = Planarian.Model.Database.Entities.RidgeWalker.File;

namespace Planarian.Tests.Integration.Infrastructure.Data;
internal static class CaveTestDataFactory
{
    public static async Task<CaveTestData> AddCaveAsync(
        PostgresTestDatabase database,
        AccountCountyTestData tenant,
        string? caveId = null,
        string? name = null,
        int countyNumber = 1)
    {
        caveId ??= $"cave{tenant.AccountId[^1]}00000";
        name ??= $"Cave {tenant.StateAbbreviation[0]}";
        var data = new CaveTestData(
            tenant.AccountId, tenant.StateId, tenant.StateName, tenant.StateAbbreviation,
            tenant.CountyId, tenant.CountyName, tenant.CountyDisplayId,
            caveId, name, countyNumber);

        await using var db = database.CreateDbContext("cave-seed", tenant.AccountId);
        db.Caves.Add(new Cave
        {
            Id = data.CaveId,
            AccountId = data.AccountId,
            StateId = data.StateId,
            CountyId = data.CountyId,
            Name = data.CaveName,
            CountyNumber = data.CountyNumber,
            IsArchived = false
        });
        await db.SaveChangesAsync();
        return data;
    }

    public static Task<CaveTestData> AddCaveAsync(
        PostgresTestDatabase database,
        PublishedCaveTestData tenant,
        string? caveId = null,
        string? name = null,
        int countyNumber = 1) =>
        AddCaveAsync(database, new AccountCountyTestData(
            tenant.AccountId, tenant.StateId, tenant.StateName, tenant.StateAbbreviation,
            tenant.CountyId, tenant.CountyName, tenant.CountyDisplayId), caveId, name, countyNumber);

    public static async Task<PublishedCaveTestData> PublishBaselineRevisionAsync(
        PostgresTestDatabase database,
        CaveTestData cave,
        string? revisionId = null)
    {
        revisionId ??= $"revision0{cave.AccountId[^1]}";
        var snapshot = new CavePublishedSnapshotV1
        {
            CaveId = cave.CaveId,
            AccountId = cave.AccountId,
            Name = cave.CaveName,
            State = new SnapshotReference(cave.StateId, cave.StateName, null, cave.StateAbbreviation),
            County = new SnapshotReference(cave.CountyId, cave.CountyName, cave.CountyDisplayId),
            CountyNumber = cave.CountyNumber
        };

        await using var db = database.CreateDbContext("revision-seed", cave.AccountId);
        db.CaveRevisions.Add(new CaveRevision
        {
            Id = revisionId,
            AccountId = cave.AccountId,
            CaveId = cave.CaveId,
            Source = CaveRevisionSource.SystemBaseline,
            Operation = CaveRevisionOperation.Create,
            SnapshotSchemaVersion = 1,
            SnapshotJson = CaveSnapshotJson.Serialize(snapshot)
        });
        await db.SaveChangesAsync();

        var persisted = await db.Caves.IgnoreQueryFilters().SingleAsync(candidate => candidate.Id == cave.CaveId);
        persisted.CurrentRevisionId = revisionId;
        await db.SaveChangesAsync();

        return new PublishedCaveTestData(
            cave.AccountId, cave.StateId, cave.StateName, cave.StateAbbreviation,
            cave.CountyId, cave.CountyName, cave.CountyDisplayId,
            cave.CaveId, cave.CaveName, cave.CountyNumber, revisionId);
    }

    public static async Task<PublishedCaveTestData> CreatePublishedCaveAsync(
        PostgresTestDatabase database,
        char suffix)
    {
        var tenant = await AccountTestDataFactory.CreateAccountWithCountyAsync(database, suffix);
        var cave = await AddCaveAsync(database, tenant, $"cave00000{suffix}");
        return await PublishBaselineRevisionAsync(database, cave);
    }

}
