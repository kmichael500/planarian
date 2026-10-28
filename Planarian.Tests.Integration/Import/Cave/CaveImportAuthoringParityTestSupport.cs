using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database.Entities;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.Revisions;
using Planarian.Model.Shared;
using Planarian.Modules.Caves.Models;
using Planarian.Modules.Caves.Repositories;
using Planarian.Modules.Caves.Revisions;
using Planarian.Modules.Import.Planning;
using Planarian.Tests.Integration.Infrastructure.Actors;
using Xunit;
using static Planarian.Tests.Integration.Caves.Revisions.CaveChangeRequestTestSupport;

namespace Planarian.Tests;

public sealed partial class CaveImportAuthoringParityIntegrationTests
{
    private static async Task<string> NamePrimaryEntranceAsync(
        PostgresTestDatabase database, PublishedCaveTestData tenant, string managerId)
    {
        await using var manager = await CaveTestActor.CreateAsync(database, tenant.AccountId, managerId);
        var context = await manager.Services.Caves.GetEditAuthoringContextAsync(tenant.CaveId, default);
        var values = ValuesFromCave(context.Cave);
        var entrance = Assert.Single(values.Entrances);
        values.Narrative = "Identity narrative";
        entrance.Name = "Primary";
        entrance.Description = "Identity entrance";
        await manager.Services.Caves.AddCave(values, default);
        manager.Db.ChangeTracker.Clear();
        return (await CurrentRevisionAsync(manager.Db, tenant.CaveId)).Id;
    }

    private static async Task<string> PopulateExistingCaveAsync(
        PostgresTestDatabase database,
        PublishedCaveTestData tenant,
        ParityReferences references,
        string managerId)
    {
        await using var manager = await CaveTestActor.CreateAsync(database, tenant.AccountId, managerId);
        var context = await manager.Services.Caves.GetEditAuthoringContextAsync(tenant.CaveId, default);
        var values = ValuesFromCave(context.Cave);
        ApplyFullTarget(values, references);
        await manager.Services.Caves.AddCave(values, default);
        manager.Db.ChangeTracker.Clear();
        return (await CurrentRevisionAsync(manager.Db, tenant.CaveId)).Id;
    }

    private static async Task<string> PopulateExistingCaveByImportAsync(
        PostgresTestDatabase database,
        PublishedCaveTestData tenant)
    {
        await using var db = database.CreateDbContext("populate-import", tenant.AccountId);
        var import = new CaveImportTestHarness(db, db.RequestUser);
        var plan = await import.PlanCsvAsync(
            CaveCsv(FullRow(tenant, tenant.CountyNumber, archived: false)), syncExisting: true);
        await import.ExecuteAsync(plan, "populate-before-delete.csv");
        db.ChangeTracker.Clear();
        return (await CurrentRevisionAsync(db, tenant.CaveId)).Id;
    }

    private static async Task AssertCurrentSnapshotsEqualAsync(
        PostgresTestDatabase directDatabase,
        PublishedCaveTestData directTenant,
        PostgresTestDatabase importDatabase,
        PublishedCaveTestData importTenant)
    {
        await using var directDb = directDatabase.CreateDbContext("parity-verify", directTenant.AccountId);
        await using var importDb = importDatabase.CreateDbContext("parity-verify", importTenant.AccountId);
        var direct = await new CavePublishedSnapshotRepository(directDb, directDb.RequestUser)
            .BuildAsync(directTenant.CaveId);
        var imported = await new CavePublishedSnapshotRepository(importDb, importDb.RequestUser)
            .BuildAsync(importTenant.CaveId);
        ImportParityAssertions.AssertPublishedSnapshotsEqual(direct, imported);
    }

    private static CavePublishedSnapshotV1 ReadSnapshot(CaveRevision revision) =>
        CaveSnapshotJson.Deserialize(revision.SnapshotJson, revision.SnapshotSchemaVersion);

    private static async Task<CaveRevision> CurrentRevisionAsync(
        Planarian.Model.Database.PlanarianDbContext db, string caveId)
    {
        var currentId = await db.Caves.IgnoreQueryFilters()
            .Where(row => row.Id == caveId)
            .Select(row => row.CurrentRevisionId)
            .SingleAsync();
        return await db.CaveRevisions.SingleAsync(row => row.Id == currentId);
    }

    private static void ApplyFullTarget(AddCaveVm values, ParityReferences references)
    {
        values.Name = "Parity Cave";
        values.AlternateNames = ["Alpha Alias", "Zulu Alias"];
        values.LengthFeet = 123.5;
        values.DepthFeet = 45.5;
        values.MaxPitDepthFeet = 20.5;
        values.NumberOfPits = 4;
        values.Narrative = "Full parity narrative";
        values.ReportedOn = ReportedOn;
        values.GeologyTagIds = [references.Geology];
        values.GeologicAgeTagIds = [references.GeologicAge];
        values.MapStatusTagIds = [references.MapStatus];
        values.PhysiographicProvinceTagIds = [references.PhysiographicProvince];
        values.ArcheologyTagIds = [references.Archeology];
        values.BiologyTagIds = [references.Biology];
        values.OtherTagIds = [references.Other];
        values.CartographerNameTagIds = [references.Cartographer];
        values.ReportedByNameTagIds = [references.Reporter];
    }

    private static void ApplyClearedTarget(AddCaveVm values)
    {
        values.Name = "Cleared Parity Cave";
        values.AlternateNames = [];
        values.LengthFeet = null;
        values.DepthFeet = null;
        values.MaxPitDepthFeet = null;
        values.NumberOfPits = null;
        values.Narrative = null;
        values.ReportedOn = null;
        values.GeologyTagIds = [];
        values.GeologicAgeTagIds = [];
        values.MapStatusTagIds = [];
        values.PhysiographicProvinceTagIds = [];
        values.ArcheologyTagIds = [];
        values.BiologyTagIds = [];
        values.OtherTagIds = [];
        values.CartographerNameTagIds = [];
        values.ReportedByNameTagIds = [];
    }

    private static AddCaveVm FullValues(
        AccountCountyTestData tenant, ParityReferences references, int countyNumber)
    {
        var values = new AddCaveVm
        {
            StateId = tenant.StateId,
            CountyId = tenant.CountyId,
            IsCountyNumberManuallySet = true,
            CountyNumber = countyNumber
        };
        ApplyFullTarget(values, references);
        values.Entrances =
        [
            new AddEntranceVm
            {
                IsPrimary = true,
                LocationQualityTagId = references.LocationQuality,
                Name = "Primary",
                Description = "Primary entrance",
                Latitude = 35,
                Longitude = -86,
                ElevationFeet = 500
            }
        ];
        return values;
    }

    private static string FullRow(
        AccountCountyTestData tenant, int countyNumber, bool archived) =>
        CsvRow("Parity Cave", tenant.CountyName, tenant.CountyDisplayId, countyNumber,
            tenant.StateAbbreviation, "Zulu Alias,Alpha Alias", "Mapped", "Parity Cartographer",
            123.5, 45.5, 20.5, 4, "Limestone", "Mississippian", "Highland Rim",
            "Historic", "Bat", "2026-08-01", "Parity Reporter", archived, "Other cave value",
            "Full parity narrative");

    private static string FullRow(
        PublishedCaveTestData tenant, int countyNumber, bool archived) =>
        CsvRow("Parity Cave", tenant.CountyName, tenant.CountyDisplayId, countyNumber,
            tenant.StateAbbreviation, "Zulu Alias,Alpha Alias", "Mapped", "Parity Cartographer",
            123.5, 45.5, 20.5, 4, "Limestone", "Mississippian", "Highland Rim",
            "Historic", "Bat", "2026-08-01", "Parity Reporter", archived, "Other cave value",
            "Full parity narrative");

    private static string ClearedRow(PublishedCaveTestData tenant, int countyNumber) =>
        CsvRow("Cleared Parity Cave", tenant.CountyName, tenant.CountyDisplayId, countyNumber,
            tenant.StateAbbreviation, "", "", "", null, null, null, null, "", "", "", "",
            "", "", "", false, "", "");

    private static string BaselineRow(PublishedCaveTestData tenant, bool archived) =>
        CsvRow(tenant.CaveName, tenant.CountyName, tenant.CountyDisplayId, tenant.CountyNumber,
            tenant.StateAbbreviation, "", "", "", null, null, null, null, "", "", "", "",
            "", "", "", archived, "", "");

    private static string CaveCsv(string row) =>
        ImportDryRunIntegrationTests.CaveHeader + "\n" + row + "\n";

    private static string CsvRow(params object?[] values) =>
        string.Join(',', values.Select(value =>
        {
            var text = value switch
            {
                null => "",
                bool boolean => boolean ? "true" : "false",
                IFormattable formattable => formattable.ToString(null, System.Globalization.CultureInfo.InvariantCulture),
                _ => value.ToString() ?? ""
            };
            return text.Contains(',') || text.Contains('"')
                ? "\"" + text.Replace("\"", "\"\"") + "\""
                : text;
        }));

    private static async Task<ParityReferences> SeedReferencesAsync(
        PostgresTestDatabase database, string accountId)
    {
        async Task<string> Add(string key, string name, string id) =>
            (await ReferenceTestData.AddTagAsync(database, accountId, key, name, id)).Id;

        return new ParityReferences(
            await Add(TagTypeKeyConstant.Geology, "Limestone", "impgeo0001"),
            await Add(TagTypeKeyConstant.GeologicAge, "Mississippian", "impage0001"),
            await Add(TagTypeKeyConstant.MapStatus, "Mapped", "impmap0001"),
            await Add(TagTypeKeyConstant.PhysiographicProvince, "Highland Rim", "impphy0001"),
            await Add(TagTypeKeyConstant.Archeology, "Historic", "imparc0001"),
            await Add(TagTypeKeyConstant.Biology, "Bat", "impbio0001"),
            await Add(TagTypeKeyConstant.CaveOther, "Other cave value", "impoth0001"),
            await Add(TagTypeKeyConstant.People, "Parity Cartographer", "imppep0001"),
            await Add(TagTypeKeyConstant.People, "Parity Reporter", "imppep0002"),
            await Add(TagTypeKeyConstant.LocationQuality, "Survey Grade", "imploc0001"));
    }

    private static async Task GrantCountyManagerAsync(
        PostgresTestDatabase database, AccountCountyTestData tenant, string userId)
    {
        const string permissionId = "mAnageR000";
        await GlobalSystemTestData.EnsurePermissionAsync(database, new Permission
        {
            Id = permissionId,
            Key = "Manager",
            Name = "Manager",
            Description = "Manage Caves",
            PermissionType = "Cave"
        });

        await using var db = database.CreateDbContext(userId, tenant.AccountId);
        await CavePermissions.EnsureAccountUserAsync(db, tenant.AccountId);
        db.CavePermissions.Add(new CavePermission
        {
            UserId = db.RequestUser.Id,
            AccountId = tenant.AccountId,
            CountyId = tenant.CountyId,
            PermissionId = permissionId
        });
        await db.SaveChangesAsync();
    }

    private sealed record ParityReferences(
        string Geology,
        string GeologicAge,
        string MapStatus,
        string PhysiographicProvince,
        string Archeology,
        string Biology,
        string Other,
        string Cartographer,
        string Reporter,
        string LocationQuality);
}
