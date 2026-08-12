using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.Revisions;
using Planarian.Model.Shared;
using Planarian.Modules.Account.Repositories;
using Planarian.Modules.Caves.Models;
using Planarian.Modules.Caves.Repositories;
using Planarian.Modules.Caves.Revisions;
using Planarian.Modules.Caves.Services;
using Planarian.Modules.Files.Controllers;
using Planarian.Modules.Files.Repositories;
using Planarian.Modules.Files.Services;
using Planarian.Modules.Tags.Repositories;
using Planarian.Tests;

namespace Planarian.Tests.Integration.Caves.Revisions;

internal static class CaveChangeRequestTestSupport
{
    internal sealed record TaggedPublishedCave(PublishedCaveTestData Cave, string BiologyTagId,
        string HydrologyTagId, string EntranceId);

    internal static async Task<TaggedPublishedCave> CreateTaggedPublishedCaveAsync(PostgresTestDatabase database)
    {
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var biology = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.Biology, "Cricket", "biology00a");
        var hydrology = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.EntranceHydrology, "Wet", "hydrologya");
        var location = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        const string entranceId = "entrance0a";
        await EntranceTestData.AddEntranceAsync(database, cave, entranceId,
            locationQualityTagId: location.Id);
        await using var seed = database.CreateDbContext("tagged-cave-seed", cave.AccountId);
        seed.BiologyTags.Add(new BiologyTag
            { Id = "biolink00a", CaveId = cave.CaveId, TagTypeId = biology.Id });
        seed.EntranceHydrologyTags.Add(new EntranceHydrologyTag
            { Id = "hydrolinkA", EntranceId = entranceId, TagTypeId = hydrology.Id });
        await seed.SaveChangesAsync();
        var revision = await new CaveMutationRepository(seed, seed.RequestUser,
                new CavePublishedSnapshotRepository(seed, seed.RequestUser))
            .PublishExistingAsync(cave.CaveId, cave.RevisionId, CaveRevisionSource.ManagerEdit,
                CaveRevisionOperation.Update, _ => { });
        return new TaggedPublishedCave(cave with { RevisionId = revision.RevisionId! }, biology.Id,
            hydrology.Id, entranceId);
    }

    internal static CaveProposalSnapshotV1 Proposal(PublishedCaveTestData cave, string name) => new()
    {
        AccountId = cave.AccountId,
        CaveId = cave.CaveId,
        Name = name,
        StateId = cave.StateId,
        CountyId = cave.CountyId,
        CountyNumberIntent = CountyNumberIntent.Manual,
        RequestedCountyNumber = cave.CountyNumber
    };

    internal static CaveProposalSnapshotV1 PublishableProposal(PublishedCaveTestData cave,
        string locationQualityTagId, string name, string? narrative = null) => new()
    {
        AccountId = cave.AccountId,
        CaveId = cave.CaveId,
        Name = name,
        StateId = cave.StateId,
        CountyId = cave.CountyId,
        CountyNumberIntent = CountyNumberIntent.Manual,
        RequestedCountyNumber = cave.CountyNumber,
        Narrative = narrative,
        Entrances = [new CaveProposalEntranceV1
        {
            EntranceId = string.Empty,
            Name = "Main Entrance",
            IsPrimary = true,
            Latitude = 35,
            Longitude = -86,
            Elevation = 500,
            LocationQualityTagId = locationQualityTagId
        }]
    };

    internal static AddCaveVm PublishableValues(PublishedCaveTestData cave, string locationQualityTagId,
        string name, string? narrative = null) => new()
    {
        Id = cave.CaveId,
        Name = name,
        AlternateNames = [],
        StateId = cave.StateId,
        CountyId = cave.CountyId,
        CountyNumber = cave.CountyNumber,
        IsCountyNumberManuallySet = true,
        Narrative = narrative,
        Entrances = [new AddEntranceVm
        {
            Name = "Main Entrance",
            IsPrimary = true,
            Latitude = 35,
            Longitude = -86,
            ElevationFeet = 500,
            LocationQualityTagId = locationQualityTagId
        }]
    };

    internal static AddCaveVm ValuesFromCave(CaveVm cave) => new()
    {
        Id = cave.Id,
        Name = cave.Name,
        AlternateNames = cave.AlternateNames,
        StateId = cave.StateId,
        CountyId = cave.CountyId,
        CountyNumber = cave.CountyNumber,
        IsCountyNumberManuallySet = true,
        LengthFeet = cave.LengthFeet,
        DepthFeet = cave.DepthFeet,
        MaxPitDepthFeet = cave.MaxPitDepthFeet,
        NumberOfPits = cave.NumberOfPits,
        Narrative = cave.Narrative,
        ReportedOn = cave.ReportedOn,
        GeologyTagIds = cave.GeologyTagIds,
        ReportedByNameTagIds = cave.ReportedByNameTagIds,
        BiologyTagIds = cave.BiologyTagIds,
        ArcheologyTagIds = cave.ArcheologyTagIds,
        CartographerNameTagIds = cave.CartographerNameTagIds,
        MapStatusTagIds = cave.MapStatusTagIds,
        GeologicAgeTagIds = cave.GeologicAgeTagIds,
        PhysiographicProvinceTagIds = cave.PhysiographicProvinceTagIds,
        OtherTagIds = cave.OtherTagIds,
        Files = cave.Files.Select(file => new EditFileMetadataVm
            { Id = file.Id, FileTypeTagId = file.FileTypeTagId, DisplayName = file.DisplayName }).ToList(),
        Entrances = cave.Entrances.Select(entrance => new AddEntranceVm
        {
            Id = entrance.Id,
            IsPrimary = entrance.IsPrimary,
            LocationQualityTagId = entrance.LocationQualityTagId,
            Name = entrance.Name,
            Description = entrance.Description,
            Latitude = entrance.Latitude,
            Longitude = entrance.Longitude,
            ElevationFeet = entrance.ElevationFeet,
            ReportedOn = entrance.ReportedOn,
            PitFeet = entrance.PitFeet,
            EntranceStatusTagIds = entrance.EntranceStatusTagIds,
            FieldIndicationTagIds = entrance.FieldIndicationTagIds,
            EntranceHydrologyTagIds = entrance.EntranceHydrologyTagIds,
            EntranceOtherTagIds = entrance.EntranceOtherTagIds,
            ReportedByNameTagIds = entrance.ReportedByNameTagIds
        }).ToList()
    };

    internal static async Task<(PublishedCaveTestData Cave, string LocationTagId)> CreateMeasuredPublishedCaveAsync(
        PostgresTestDatabase database, char suffix, double? length, double? depth, double? maxPitDepth,
        int? numberOfPits)
    {
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, suffix);
        var locationTag = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", $"locqual00{suffix}");
        await EntranceTestData.AddEntranceAsync(database, cave, $"entrance0{suffix}",
            locationQualityTagId: locationTag.Id);
        string revisionId;
        await using (var manager = database.CreateDbContext("measurement-seed", cave.AccountId))
        {
            var mutations = new CaveMutationRepository(manager, manager.RequestUser,
                new CavePublishedSnapshotRepository(manager, manager.RequestUser));
            revisionId = (await mutations.PublishExistingAsync(cave.CaveId, cave.RevisionId,
                CaveRevisionSource.ManagerEdit, CaveRevisionOperation.Update, entity =>
                {
                    entity.LengthFeet = length;
                    entity.DepthFeet = depth;
                    entity.MaxPitDepthFeet = maxPitDepth;
                    entity.NumberOfPits = numberOfPits;
                })).RevisionId!;
        }
        return (cave with { RevisionId = revisionId }, locationTag.Id);
    }

    internal static Task<string> CurrentVersionAsync(Planarian.Model.Database.PlanarianDbContext db,
        string requestId) => db.CaveChangeRequests.Where(request => request.Id == requestId)
        .Select(request => request.CurrentProposalVersionId!).SingleAsync();

}
