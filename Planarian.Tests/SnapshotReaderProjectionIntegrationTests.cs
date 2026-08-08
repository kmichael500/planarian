using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using Planarian.Model.Database;
using Planarian.Model.Database.Entities;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.Revisions;
using Planarian.Model.Shared.Helpers;
using Planarian.Modules.Caves.Revisions;
using Xunit;

namespace Planarian.Tests;

public sealed class SnapshotReaderProjectionIntegrationTests(PostgresIntegrationFixture fixture)
    : IClassFixture<PostgresIntegrationFixture>
{
    [Fact]
    public async Task ProjectionReaderMatchesLegacyIncludeReaderForRepresentativeAggregate()
    {
        await using var database = await fixture.CreateDatabaseAsync(nameof(ProjectionReaderMatchesLegacyIncludeReaderForRepresentativeAggregate));
        var tenant = await IntegrationTestData.SeedTenantAsync(database, 'a');
        await using (var db = database.CreateDbContext("manager", tenant.AccountId))
        {
            var geology = Tag(tenant.AccountId, "geology", "Limestone");
            var quality = Tag(tenant.AccountId, "location-quality", "Survey Grade");
            var status = Tag(tenant.AccountId, "entrance-status", "Open");
            db.TagTypes.AddRange(geology, quality, status);
            await db.SaveChangesAsync();
            var cave = await db.Caves.IgnoreQueryFilters().SingleAsync(c => c.Id == tenant.CaveId);
            cave.SetAlternateNamesList(["Zulu", "Alpha"]);
            cave.Narrative = "Representative narrative";
            db.GeologyTags.Add(new GeologyTag { Id = IdGenerator.Generate(), CaveId = cave.Id, TagTypeId = geology.Id });
            var entrance = new Entrance
            {
                Id = "entrance01", CaveId = cave.Id, LocationQualityTagId = quality.Id, Name = "Main", IsPrimary = true,
                Description = "Main entrance", Location = new Point(new CoordinateZ(-86.2, 35.4, 812)) { SRID = 4326 }, PitDepthFeet = 42
            };
            db.Entrances.Add(entrance);
            await db.SaveChangesAsync();
            db.EntranceStatusTags.Add(new EntranceStatusTag { Id = IdGenerator.Generate(), EntranceId = entrance.Id, TagTypeId = status.Id });
            var file = await db.Files.SingleAsync(f => f.Id == tenant.FileId);
            file.CaveId = cave.Id;
            file.DisplayName = "Survey map";
            await db.SaveChangesAsync();
        }

        await using var verify = database.CreateDbContext("manager", tenant.AccountId);
        var actual = await new CavePublishedSnapshotReader(verify, verify.RequestUser).BuildAsync(tenant.CaveId);
        var expected = await BuildLegacyAsync(verify, tenant.AccountId, tenant.CaveId);
        Assert.Equal(CaveSnapshotJson.Serialize(expected), CaveSnapshotJson.Serialize(actual));
    }

    private static TagType Tag(string accountId, string key, string name) => new(name, key)
    { Id = IdGenerator.Generate(), AccountId = accountId, IsDefault = false };

    private static async Task<CavePublishedSnapshotV1> BuildLegacyAsync(PlanarianDbContext db, string accountId, string caveId)
    {
        var cave = await db.Caves.IgnoreQueryFilters().Where(c => c.AccountId == accountId).AsNoTracking().AsSplitQuery()
            .Include(c => c.State).Include(c => c.County)
            .Include(c => c.Files).ThenInclude(f => f.FileTypeTag)
            .Include(c => c.Entrances).ThenInclude(e => e.LocationQualityTag)
            .Include(c => c.Entrances).ThenInclude(e => e.EntranceStatusTags).ThenInclude(t => t.TagType)
            .Include(c => c.GeologyTags).ThenInclude(t => t.TagType)
            .SingleAsync(c => c.Id == caveId);
        return new CavePublishedSnapshotV1
        {
            CaveId = cave.Id, AccountId = cave.AccountId, Name = cave.Name,
            AlternateNames = cave.AlternateNamesList.Order(StringComparer.Ordinal).ToList(),
            State = new SnapshotReference(cave.StateId, cave.State.Name, null, cave.State.Abbreviation),
            County = new SnapshotReference(cave.CountyId, cave.County.Name, cave.County.DisplayId),
            CountyNumber = cave.CountyNumber, LengthFeet = cave.LengthFeet, DepthFeet = cave.DepthFeet,
            MaxPitDepthFeet = cave.MaxPitDepthFeet, NumberOfPits = cave.NumberOfPits, Narrative = cave.Narrative,
            ReportedByUserId = cave.ReportedByUserId, ReportedOn = cave.ReportedOn, IsArchived = cave.IsArchived,
            Tags = cave.GeologyTags.Select(t => new SnapshotTagReference(SnapshotTagRole.Geology, t.TagTypeId, t.TagType.Name))
                .OrderBy(t => t.Role).ThenBy(t => t.TagTypeId).ToList(),
            Entrances = cave.Entrances.OrderBy(e => e.Id).Select(e => new CaveEntranceSnapshotV1
            {
                Id = e.Id, Name = e.Name, IsPrimary = e.IsPrimary, Description = e.Description,
                ReportedByUserId = e.ReportedByUserId, Latitude = e.Location?.Y, Longitude = e.Location?.X,
                Elevation = e.Location?.Z, Srid = e.Location?.SRID ?? 4326,
                LocationQualityTagId = e.LocationQualityTagId, LocationQualityNameAtRevision = e.LocationQualityTag.Name,
                ReportedOn = e.ReportedOn, PitDepthFeet = e.PitDepthFeet,
                Tags = e.EntranceStatusTags.Select(t => new SnapshotTagReference(SnapshotTagRole.EntranceStatus, t.TagTypeId, t.TagType.Name))
                    .OrderBy(t => t.Role).ThenBy(t => t.TagTypeId).ToList()
            }).ToList(),
            Files = cave.Files.OrderBy(f => f.Id).Select(f => new CaveFileSnapshotV1
            {
                Id = f.Id, FileTypeTagId = f.FileTypeTagId, FileTypeNameAtRevision = f.FileTypeTag.Name,
                FileName = f.FileName, DisplayName = f.DisplayName
            }).ToList()
        };
    }
}
