using Microsoft.EntityFrameworkCore;
using Npgsql;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.Revisions;
using Planarian.Model.Shared;
using Planarian.Modules.Caves.Revisions;
using Xunit;
using static Planarian.Tests.MigrationTestSupport;

namespace Planarian.Tests;

[Collection(MigrationIntegrationCollection.Name)]
public sealed class CurrentMainDatabasePreservationMigrationIntegrationTests(PostgresTestServer fixture)
    : IClassFixture<PostgresTestServer>
{
    [Fact]
    public async Task CurrentMainV32CompletionMigrationPreservesCompleteDurableDatabase()
    {
        await using var database = await fixture.CreateUnmigratedDatabaseAsync(
            nameof(CurrentMainV32CompletionMigrationPreservesCompleteDurableDatabase));
        var baseline = database.GetMigrationNames().Single(migration =>
            migration.EndsWith(CurrentMainBaselineMigration, StringComparison.Ordinal));
        await database.MigrateAsync(baseline);

        Assert.Equal(
            CurrentMainV32DatabaseCensus.BaselineTables.Order(StringComparer.Ordinal),
            await CurrentMainV32DatabaseCensus.GetPublicApplicationTablesAsync(database));

        await CurrentMainV32CensusSeeder.SeedCompleteDurableDatabaseAsync(database);
        var counts = await CurrentMainV32DatabaseCensus.CaptureTableCountsAsync(
            database, CurrentMainV32DatabaseCensus.BaselineTables);
        Assert.Equal(CurrentMainV32DatabaseCensus.BaselineTables.Length, counts.Count);
        Assert.All(counts, pair => Assert.True(pair.Value > 0,
            $"The v32 preservation fixture must populate {pair.Key}; an empty table cannot prove row preservation."));

        var exactBefore = await CurrentMainV32DatabaseCensus.CaptureExactTablesAsync(
            database, CurrentMainV32DatabaseCensus.ExactBaselineTables);
        var cavesBefore = await CurrentMainV32DatabaseCensus.CaptureCaveMigrationRowsAsync(database);
        var entrancesBefore = await CurrentMainV32DatabaseCensus.CaptureEntranceMigrationRowsAsync(database);
        var filesBefore = await CurrentMainV32DatabaseCensus.CaptureFileStableRowsAsync(database);
        var featureSettingsBefore = await CurrentMainV32DatabaseCensus.CaptureSeededFeatureSettingsAsync(database);
        var effectivePermissionsBefore = await CurrentMainV32DatabaseCensus.CaptureUserCavePermissionsAsync(database);

        await database.MigrateAsync(null);

        Assert.Equal(
            CurrentMainV32DatabaseCensus.ExpectedLatestTables,
            await CurrentMainV32DatabaseCensus.GetPublicApplicationTablesAsync(database));

        var exactAfter = await CurrentMainV32DatabaseCensus.CaptureExactTablesAsync(
            database, CurrentMainV32DatabaseCensus.ExactBaselineTables);
        foreach (var table in CurrentMainV32DatabaseCensus.ExactBaselineTables)
            Assert.Equal(exactBefore[table], exactAfter[table]);

        Assert.Equal(cavesBefore, await CurrentMainV32DatabaseCensus.CaptureCaveMigrationRowsAsync(database));
        Assert.Equal(entrancesBefore, await CurrentMainV32DatabaseCensus.CaptureEntranceMigrationRowsAsync(database));
        Assert.Equal(filesBefore, await CurrentMainV32DatabaseCensus.CaptureFileStableRowsAsync(database));
        Assert.Equal(featureSettingsBefore,
            await CurrentMainV32DatabaseCensus.CaptureSeededFeatureSettingsAsync(database));
        Assert.Equal(effectivePermissionsBefore,
            await CurrentMainV32DatabaseCensus.CaptureUserCavePermissionsAsync(database));

        await AssertExpectedFileMigrationAsync(database);
        await AssertExpectedFeatureSettingBackfillAsync(database);
        await AssertExpectedMigrationOwnedCompatibilityRowsAsync(database);
        await AssertLegacyReporterColumnsWereOnlyIntentionallyRemovedAsync(database);

        var completionCounts = await CurrentMainV32DatabaseCensus.CaptureTableCountsAsync(
            database, CurrentMainV32DatabaseCensus.CompletionOwnedTables);
        Assert.All(completionCounts, pair => Assert.Equal(0, pair.Value));

        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var revisions = new NpgsqlCommand(
            """select count(*) from "Caves" where "CurrentRevisionId" is not null""", connection);
        Assert.Equal(0L, (long)(await revisions.ExecuteScalarAsync())!);

        await using var history = new NpgsqlCommand(
            """select "MigrationId" from "__EFMigrationsHistory" order by "MigrationId" desc limit 1""", connection);
        Assert.Equal(database.GetMigrationNames().Last(), (string?)await history.ExecuteScalarAsync());
    }

    [Fact]
    public async Task RevisionRolloutOnlyCreatesBaselineHistoryAndAdvancesCavePointers()
    {
        await using var database = await fixture.CreateUnmigratedDatabaseAsync(
            nameof(RevisionRolloutOnlyCreatesBaselineHistoryAndAdvancesCavePointers));
        var baseline = database.GetMigrationNames().Single(migration =>
            migration.EndsWith(CurrentMainBaselineMigration, StringComparison.Ordinal));
        await database.MigrateAsync(baseline);
        await CurrentMainV32CensusSeeder.SeedCompleteDurableDatabaseAsync(database);
        await database.MigrateAsync(null);

        await using var rolloutDb = database.CreateDbContext(
            CurrentMainV32CensusSeeder.UserAlpha, CurrentMainV32CensusSeeder.AccountAlpha);
        var nonRolloutTables = CurrentMainV32DatabaseCensus.ExpectedLatestTables
            .Except(["Caves", "CaveRevisions"], StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var untouchedBefore = await CurrentMainV32DatabaseCensus.CaptureExactTablesAsync(
            database, nonRolloutTables);
        var caveRowsBefore = await CurrentMainV32DatabaseCensus.CaptureCaveRolloutRowsAsync(database);
        var effectivePermissionsBefore = await CurrentMainV32DatabaseCensus.CaptureUserCavePermissionsAsync(database);

        var result = await new CaveRevisionRolloutRepository(rolloutDb).InitializeAsync();

        Assert.False(result.AlreadyComplete);
        Assert.Equal(3, result.InitializedCaveCount);

        var untouchedAfter = await CurrentMainV32DatabaseCensus.CaptureExactTablesAsync(
            database, nonRolloutTables);
        foreach (var table in nonRolloutTables)
            Assert.Equal(untouchedBefore[table], untouchedAfter[table]);
        Assert.Equal(caveRowsBefore, await CurrentMainV32DatabaseCensus.CaptureCaveRolloutRowsAsync(database));
        Assert.Equal(effectivePermissionsBefore,
            await CurrentMainV32DatabaseCensus.CaptureUserCavePermissionsAsync(database));

        await AssertRolloutRevisionsMatchSeededV32StateAsync(database, result.RevisionHistoryStartedOn);

        var beforeRerun = await CurrentMainV32DatabaseCensus.CaptureExactTablesAsync(
            database, CurrentMainV32DatabaseCensus.ExpectedLatestTables);
        var rerun = await new CaveRevisionRolloutRepository(rolloutDb).InitializeAsync();
        var afterRerun = await CurrentMainV32DatabaseCensus.CaptureExactTablesAsync(
            database, CurrentMainV32DatabaseCensus.ExpectedLatestTables);

        Assert.True(rerun.AlreadyComplete);
        Assert.Equal(0, rerun.InitializedCaveCount);
        Assert.Equal(result.RevisionHistoryStartedOn, rerun.RevisionHistoryStartedOn);
        foreach (var table in CurrentMainV32DatabaseCensus.ExpectedLatestTables)
            Assert.Equal(beforeRerun[table], afterRerun[table]);
    }

    private static async Task AssertExpectedFileMigrationAsync(PostgresTestDatabase database)
    {
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""
            select "Id", "AccountId", "CaveId", "Name", "Extension"
            from "Files"
            order by "Id" collate "C"
            """, connection);
        await using var reader = await command.ExecuteReaderAsync();

        var actual = new List<(string Id, string AccountId, string? CaveId, string Name, string Extension)>();
        while (await reader.ReadAsync())
            actual.Add((
                reader.GetString(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4)));

        Assert.Equal(
        [
            ("filealpha1", "acctalpha1", "cavealpha1", "Survey Map", ".pdf"),
            ("filealpha2", "acctalpha1", "cavealpha1", "photos.tar", ".gz"),
            ("filealpha3", "acctalpha1", null, "temporary", ".csv"),
            ("filealpha4", "acctalpha1", "cavealpha2", "Field Notes", ""),
            ("filebeta01", "acctbeta01", "cavebeta01", "beta", ".gpx")
        ], actual);
    }

    private static async Task AssertExpectedFeatureSettingBackfillAsync(PostgresTestDatabase database)
    {
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();

        await using (var total = new NpgsqlCommand("select count(*) from \"FeatureSettings\"", connection))
            Assert.Equal(CurrentMainV32CensusSeeder.SeededFeatureSettingIds.Length + 2,
                (long)(await total.ExecuteScalarAsync())!);

        await using (var command = new NpgsqlCommand("""
            select "AccountId", "Id", "IsEnabled", "IsDefault"
            from "FeatureSettings"
            where "Key"='EnabledFieldEntranceOtherTags'
            order by "AccountId" nulls first
            """, connection))
        await using (var reader = await command.ExecuteReaderAsync())
        {
            var rows = new List<(string? AccountId, string Id, bool Enabled, bool IsDefault)>();
            while (await reader.ReadAsync())
                rows.Add((
                    reader.IsDBNull(0) ? null : reader.GetString(0),
                    reader.GetString(1),
                    reader.GetBoolean(2),
                    reader.GetBoolean(3)));

            Assert.Equal(4, rows.Count);
            Assert.Contains(rows, row => row == (null, "fsetglobal", true, true));
            Assert.Contains(rows, row => row == ("acctbeta01", "fsetbeta01", false, false));
            Assert.Contains(rows, row => row.AccountId == "acctalpha1" && row.Enabled && !row.IsDefault);
            Assert.Contains(rows, row => row.AccountId == "acctempty1" && row.Enabled && !row.IsDefault);
        }

        await using var markers = new NpgsqlCommand("""
            select inserted."AccountId", inserted."FeatureSettingId",
                   exists (
                       select 1
                       from "FeatureSettings" setting
                       where setting."Id" = inserted."FeatureSettingId"
                         and setting."AccountId" = inserted."AccountId"
                         and setting."Key" = 'EnabledFieldEntranceOtherTags')
            from "__BackfillEntranceOtherFeatureSettingRows" inserted
            order by inserted."AccountId" collate "C"
            """, connection);
        await using var markerReader = await markers.ExecuteReaderAsync();
        var provenance = new List<(string AccountId, string FeatureSettingId, bool MatchesSetting)>();
        while (await markerReader.ReadAsync())
            provenance.Add((markerReader.GetString(0), markerReader.GetString(1), markerReader.GetBoolean(2)));

        Assert.Equal(["acctalpha1", "acctempty1"], provenance.Select(row => row.AccountId));
        Assert.All(provenance, row => Assert.True(row.MatchesSetting,
            $"Backfill provenance for {row.AccountId} must point to the setting created for that account."));
    }

    private static async Task AssertExpectedMigrationOwnedCompatibilityRowsAsync(PostgresTestDatabase database)
    {
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""
            select "FileId", "OriginalFileName", "MigratedName", "MigratedExtension"
            from "__CaveRevisionsCompletionLegacyFileNames"
            order by "FileId" collate "C"
            """, connection);
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<(string FileId, string Original, string Name, string Extension)>();
        while (await reader.ReadAsync())
            rows.Add((reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3)));

        Assert.Equal(
        [
            ("filealpha1", "survey.v1.pdf", "Survey Map", ".pdf"),
            ("filealpha4", "README", "Field Notes", "")
        ], rows);
    }

    private static async Task AssertLegacyReporterColumnsWereOnlyIntentionallyRemovedAsync(
        PostgresTestDatabase database)
    {
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""
            select
              not exists(select 1 from information_schema.columns
                         where table_schema='public' and table_name='Caves' and column_name='ReportedByUserId'),
              not exists(select 1 from information_schema.columns
                         where table_schema='public' and table_name='Entrances' and column_name='ReportedByUserId'),
              (select count(*) from "Users" where "Id" in ('useralpha1','userbeta01','userinvite')),
              exists(select 1 from "CaveReportedByNameTags"
                     where "CaveId"='cavealpha1' and "TagTypeId"='tagpeople1'),
              exists(select 1 from "EntranceReportedByNameTags"
                     where "EntranceId"='entalpha01' and "TagTypeId"='tagpeople1')
            """, connection);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.True(reader.GetBoolean(0));
        Assert.True(reader.GetBoolean(1));
        Assert.Equal(3L, reader.GetInt64(2));
        Assert.True(reader.GetBoolean(3));
        Assert.True(reader.GetBoolean(4));
    }

    private static async Task AssertRolloutRevisionsMatchSeededV32StateAsync(
        PostgresTestDatabase database, DateTime expectedBoundary)
    {
        await using var verify = database.CreateDbContext(
            CurrentMainV32CensusSeeder.UserAlpha, CurrentMainV32CensusSeeder.AccountAlpha);
        var revisions = await verify.CaveRevisions.IgnoreQueryFilters().AsNoTracking()
            .OrderBy(revision => revision.CaveId)
            .ToListAsync();

        Assert.Equal(3, revisions.Count);
        Assert.All(revisions, revision =>
        {
            Assert.Equal(CaveRevisionSource.SystemBaseline, revision.Source);
            Assert.Equal(CaveRevisionOperation.Baseline, revision.Operation);
            Assert.Equal(expectedBoundary, revision.CreatedOn);
            Assert.Null(revision.PreviousRevisionId);
            Assert.Null(revision.ChangeRequestId);
            Assert.Null(revision.ImportBatchId);
            Assert.Null(revision.CreatedByUserId);
            Assert.Null(revision.ModifiedByUserId);
            Assert.Null(revision.ModifiedOn);
        });
        var caves = await verify.Caves.IgnoreQueryFilters().AsNoTracking()
            .OrderBy(cave => cave.Id)
            .Select(cave => new { cave.Id, cave.AccountId, cave.CurrentRevisionId })
            .ToListAsync();
        Assert.Equal(3, caves.Count);
        foreach (var cave in caves)
        {
            var revision = revisions.Single(row => row.CaveId == cave.Id);
            Assert.Equal(cave.AccountId, revision.AccountId);
            Assert.Equal(revision.Id, cave.CurrentRevisionId);
        }

        var expected = ExpectedRolloutSnapshots();
        Assert.Equal(expected.Keys.Order(StringComparer.Ordinal),
            revisions.Select(revision => revision.CaveId).Order(StringComparer.Ordinal));
        foreach (var revision in revisions)
        {
            var snapshot = CaveSnapshotJson.Deserialize(revision.SnapshotJson, revision.SnapshotSchemaVersion);
            Assert.Equal(
                CaveSnapshotJson.Serialize(expected[revision.CaveId]),
                CaveSnapshotJson.Serialize(snapshot));
        }
    }

    private static IReadOnlyDictionary<string, CavePublishedSnapshotV1> ExpectedRolloutSnapshots()
    {
        // Keep these expectations independent from the production snapshot projector. Building expected values by
        // reading the migrated Cave graph back through that projector would make this regression test circular.
        var alphaEntranceTags = new[]
        {
            new SnapshotTagReference(SnapshotTagRole.EntranceStatus, "tagstat001", "Open"),
            new SnapshotTagReference(SnapshotTagRole.EntranceHydrology, "taghydr001", "Wet"),
            new SnapshotTagReference(SnapshotTagRole.FieldIndication, "tagfield01", "Sinkhole"),
            new SnapshotTagReference(SnapshotTagRole.EntranceReportedBy, "tagpeople1", "Alice Cartographer"),
            new SnapshotTagReference(SnapshotTagRole.EntranceOther, "tagother01", "Restricted")
        };

        return new Dictionary<string, CavePublishedSnapshotV1>(StringComparer.Ordinal)
        {
            ["cavealpha1"] = new()
            {
                CaveId = "cavealpha1",
                AccountId = "acctalpha1",
                Name = "Alpha Cave",
                AlternateNames = ["Alpha Cavern", "Old Alpha"],
                State = new("state00001", "Tennessee", null, "TN"),
                County = new("cntyalpha1", "Alpha County", "A01"),
                CountyNumber = 101,
                LengthFeet = 1234.5,
                DepthFeet = 210.25,
                MaxPitDepthFeet = 86.75,
                NumberOfPits = 7,
                Narrative = "Rich narrative with apostrophe: 'preserved' and unicode Ω.",
                ReportedOn = new DateTime(2020, 5, 6, 7, 8, 9, DateTimeKind.Utc),
                IsArchived = false,
                Tags =
                [
                    new(SnapshotTagRole.Geology, "taggeo0001", "Limestone"),
                    new(SnapshotTagRole.GeologicAge, "tagage0001", "Mississippian"),
                    new(SnapshotTagRole.MapStatus, "tagmap0001", "Mapped"),
                    new(SnapshotTagRole.PhysiographicProvince, "tagphys001", "Highland Rim"),
                    new(SnapshotTagRole.Archeology, "tagarch001", "Artifact"),
                    new(SnapshotTagRole.Biology, "tagbio0001", "Bats"),
                    new(SnapshotTagRole.CaveOther, "tagother01", "Restricted"),
                    new(SnapshotTagRole.Cartographer, "tagpeople1", "Alice Cartographer"),
                    new(SnapshotTagRole.CaveReportedBy, "tagpeople1", "Alice Cartographer")
                ],
                Entrances =
                [
                    new()
                    {
                        Id = "entalpha01",
                        Name = "Main Entrance",
                        IsPrimary = true,
                        Description = "Primary entrance",
                        Latitude = 36.1234,
                        Longitude = -86.7654,
                        Elevation = 612.25,
                        Srid = 4326,
                        LocationQualityTagId = "taglocq001",
                        LocationQualityNameAtRevision = "Survey Grade",
                        ReportedOn = new DateTime(2020, 5, 6, 7, 8, 9, DateTimeKind.Utc),
                        PitDepthFeet = 42.5,
                        Tags = alphaEntranceTags
                    },
                    new()
                    {
                        Id = "entalpha02",
                        IsPrimary = false,
                        Latitude = 0,
                        Longitude = 0,
                        Elevation = 0,
                        Srid = 4326,
                        LocationQualityTagId = "taglocq001",
                        LocationQualityNameAtRevision = "Survey Grade",
                        PitDepthFeet = 0
                    }
                ],
                Files =
                [
                    new()
                    {
                        Id = "filealpha1", FileTypeTagId = "tagfile001",
                        FileTypeNameAtRevision = "Document", Name = "Survey Map", Extension = ".pdf"
                    },
                    new()
                    {
                        Id = "filealpha2", FileTypeTagId = "tagfile001",
                        FileTypeNameAtRevision = "Document", Name = "photos.tar", Extension = ".gz"
                    }
                ],
                LinePlots =
                [
                    new()
                    {
                        Id = "linealpha1",
                        Name = "Alpha Centerline",
                        ContentHash = CaveJsonContent.NormalizeAndHash(
                            """{"type":"FeatureCollection","features":[{"type":"Feature","properties":{"kind":"centerline","order":1},"geometry":{"type":"LineString","coordinates":[[-86.7,36.1],[-86.6,36.2]]}}]}""").ContentHash
                    },
                    new()
                    {
                        Id = "linealpha2",
                        Name = "Alpha Detail",
                        ContentHash = CaveJsonContent.NormalizeAndHash(
                            """{"type":"FeatureCollection","features":[]}""").ContentHash
                    }
                ]
            },
            ["cavealpha2"] = new()
            {
                CaveId = "cavealpha2",
                AccountId = "acctalpha1",
                Name = "Archived Alpha",
                AlternateNames = ["Old Archive"],
                State = new("state00001", "Tennessee", null, "TN"),
                County = new("cntyalpha2", "Archive County", "A02"),
                CountyNumber = 7,
                IsArchived = true,
                Files =
                [
                    new()
                    {
                        Id = "filealpha4", FileTypeTagId = "tagfile001",
                        FileTypeNameAtRevision = "Document", Name = "Field Notes", Extension = ""
                    }
                ]
            },
            ["cavebeta01"] = new()
            {
                CaveId = "cavebeta01",
                AccountId = "acctbeta01",
                Name = "Beta Cave",
                State = new("state00002", "Kentucky", null, "KY"),
                County = new("cntybeta01", "Beta County", "B01"),
                CountyNumber = 3,
                LengthFeet = 0,
                DepthFeet = 0,
                MaxPitDepthFeet = 0,
                NumberOfPits = 0,
                Narrative = "Beta narrative",
                ReportedOn = new DateTime(2022, 3, 4, 0, 0, 0, DateTimeKind.Utc),
                IsArchived = false,
                Entrances =
                [
                    new()
                    {
                        Id = "entbeta001",
                        Name = "Beta Entrance",
                        IsPrimary = true,
                        Description = "Beta entrance",
                        Latitude = 37.5,
                        Longitude = -85.5,
                        Elevation = 450,
                        Srid = 4326,
                        LocationQualityTagId = "taglocq002",
                        LocationQualityNameAtRevision = "GPS",
                        ReportedOn = new DateTime(2022, 3, 4, 0, 0, 0, DateTimeKind.Utc)
                    }
                ],
                Files =
                [
                    new()
                    {
                        Id = "filebeta01", FileTypeTagId = "tagfile002",
                        FileTypeNameAtRevision = "Beta Document", Name = "beta", Extension = ".gpx"
                    }
                ],
                LinePlots =
                [
                    new()
                    {
                        Id = "linebeta01",
                        Name = "Beta Plot",
                        ContentHash = CaveJsonContent.NormalizeAndHash(
                            """{"type":"FeatureCollection","features":[{"type":"Feature","properties":{},"geometry":{"type":"Point","coordinates":[-85.5,37.5]}}]}""").ContentHash
                    }
                ]
            }
        };
    }
}
