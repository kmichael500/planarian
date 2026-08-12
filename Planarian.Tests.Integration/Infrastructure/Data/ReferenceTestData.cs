using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using Planarian.Model.Database.Entities;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.Revisions;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Helpers;
using File = Planarian.Model.Database.Entities.RidgeWalker.File;

namespace Planarian.Tests.Integration.Infrastructure.Data;
internal static class ReferenceTestData
{
    public static async Task<TagType> AddTagAsync(PostgresTestDatabase database, string accountId, string key,
        string name, string? id = null, bool isDefault = false)
    {
        var tag = new TagType(name, key)
        {
            Id = id ?? IdGenerator.Generate(),
            AccountId = isDefault ? null : accountId,
            IsDefault = isDefault
        };
        await using var db = database.CreateDbContext("tag-seed", accountId);
        db.TagTypes.Add(tag);
        await db.SaveChangesAsync();
        return tag;
    }

}

internal static class EntranceTestData
{
    public static async Task<string> AddEntranceAsync(PostgresTestDatabase database, PublishedCaveTestData cave,
        string entranceId, bool isPrimary = true, string? locationQualityTagId = null,
        string? entranceStatusTagId = null)
    {
        locationQualityTagId ??= (await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade")).Id;
        await using var db = database.CreateDbContext("entrance-seed", cave.AccountId);
        db.Entrances.Add(new Entrance
        {
            Id = entranceId,
            CaveId = cave.CaveId,
            LocationQualityTagId = locationQualityTagId,
            IsPrimary = isPrimary,
            Location = new Point(new CoordinateZ(-86, 35, 500)) { SRID = 4326 }
        });
        if (entranceStatusTagId is not null)
        {
            db.EntranceStatusTags.Add(new EntranceStatusTag
            {
                Id = IdGenerator.Generate(),
                EntranceId = entranceId,
                TagTypeId = entranceStatusTagId
            });
        }
        await db.SaveChangesAsync();
        return entranceId;
    }

}
