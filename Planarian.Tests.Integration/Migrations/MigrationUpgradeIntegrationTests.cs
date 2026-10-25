using Microsoft.EntityFrameworkCore;
using Npgsql;
using Planarian.Model.Database.Entities;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.Revisions;
using Planarian.Model.Shared;
using Planarian.Modules.Caves.Models;
using Planarian.Modules.Caves.Repositories;
using Planarian.Modules.Caves.Revisions;
using Planarian.Modules.Caves.Services;
using Planarian.Modules.Files.Controllers;
using Planarian.Modules.Tags.Repositories;
using Xunit;
using Planarian.Tests.Integration.Infrastructure.Services;

namespace Planarian.Tests;

[Collection(MigrationIntegrationCollection.Name)]
public sealed class MigrationUpgradeIntegrationTests(PostgresTestServer fixture) : IClassFixture<PostgresTestServer>
{
    private const string HistoricalV29BaselineMigration = "20260423021136_v29";
    private const string CurrentMainBaselineMigration = "20260831132210_v32";
    private const string CaveRevisionsCompletionMigrationSuffix = "_CaveRevisionsCompletion";
    [Fact]
    public async Task EmptyDatabaseMigratesToLatest()
    {
        await using var database = await fixture.CreateUnmigratedDatabaseAsync(nameof(EmptyDatabaseMigratesToLatest));

        await using (var pristine = new NpgsqlConnection(database.ConnectionString))
        {
            await pristine.OpenAsync();
            await using var history = new NpgsqlCommand(
                "select to_regclass('public.\"__EFMigrationsHistory\"') is null", pristine);
            Assert.True((bool)(await history.ExecuteScalarAsync())!);
        }

        await database.MigrateAsync(null);

        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""
            select exists(select 1 from information_schema.tables where table_name = 'CaveRevisions'),
                   exists(select 1 from information_schema.tables where table_name = 'CaveChangeRequests'),
                   postgis_version()
            """, connection);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.True(reader.GetBoolean(0));
        Assert.True(reader.GetBoolean(1));
        Assert.False(string.IsNullOrWhiteSpace(reader.GetString(2)));
    }

    [Fact]
    public async Task CurrentMainV32UpgradesToLatestAndRollsBackToTheV32SchemaContract()
    {
        await using var database = await fixture.CreateUnmigratedDatabaseAsync(
            nameof(CurrentMainV32UpgradesToLatestAndRollsBackToTheV32SchemaContract));
        var migrations = database.GetMigrationNames().ToList();
        var baseline = migrations.FindIndex(migration =>
            migration.EndsWith(CurrentMainBaselineMigration, StringComparison.Ordinal));
        var completion = migrations.FindIndex(migration =>
            migration.EndsWith(CaveRevisionsCompletionMigrationSuffix, StringComparison.Ordinal));
        var backfill = migrations.FindIndex(migration =>
            migration.EndsWith("_BackfillEntranceOtherFeatureSetting", StringComparison.Ordinal));

        Assert.True(baseline >= 0, $"Expected current main baseline migration {CurrentMainBaselineMigration} was not found.");
        Assert.Equal(baseline + 1, completion);
        Assert.Equal(completion + 1, backfill);

        await database.MigrateAsync(migrations[baseline]);
        await SeedCurrentMainV32RepresentativeDataAsync(database);

        await database.MigrateAsync(null);

        CaveRevisionRolloutResult rolloutResult;
        await using (var rolloutContext = database.CreateDbContext("revision-rollout", "mainacct01"))
        {
            var rollout = new CaveRevisionRolloutRepository(rolloutContext);
            await Assert.ThrowsAsync<InvalidOperationException>(() => rollout.EnsureCompleteAsync());
            rolloutResult = await rollout.InitializeAsync();
            Assert.False(rolloutResult.AlreadyComplete);
            Assert.Equal(1, rolloutResult.InitializedCaveCount);
            await rollout.EnsureCompleteAsync();
        }

        await using (var current = database.CreateDbContext("current-main-upgrade", "mainacct01"))
        {
            var cave = await current.Caves.IgnoreQueryFilters().SingleAsync(row => row.Id == "maincave01");
            Assert.NotNull(cave.CurrentRevisionId);

            var revision = await current.CaveRevisions.IgnoreQueryFilters()
                .SingleAsync(row => row.Id == cave.CurrentRevisionId);
            Assert.Equal(CaveRevisionSource.SystemBaseline, revision.Source);
            Assert.Equal(CaveRevisionOperation.Baseline, revision.Operation);
            Assert.Null(revision.PreviousRevisionId);
            Assert.Null(revision.CreatedByUserId);
            Assert.Equal(rolloutResult.RevisionHistoryStartedOn, revision.CreatedOn);
            var snapshot = CaveSnapshotJson.Deserialize(revision.SnapshotJson, revision.SnapshotSchemaVersion);
            Assert.Equal("Existing Main Cave", snapshot.Name);
            var snapshotFile = Assert.Single(snapshot.Files);
            Assert.Equal("existing-cave", snapshotFile.Name);
            Assert.Equal(".pdf", snapshotFile.Extension);

            var file = await current.Files.SingleAsync(row => row.Id == "mainfiler1");
            Assert.Equal("existing-cave", file.Name);
            Assert.Equal(".pdf", file.Extension);

            var generatedSetting = await current.FeatureSettings.SingleAsync(setting =>
                setting.AccountId == "mainacct01" &&
                setting.Key == FeatureKey.EnabledFieldEntranceOtherTags);
            Assert.True(generatedSetting.IsEnabled);
            Assert.False(generatedSetting.IsDefault);
        }

        await using (var rerunContext = database.CreateDbContext("revision-rollout-rerun", "mainacct01"))
        {
            var rerun = await new CaveRevisionRolloutRepository(rerunContext).InitializeAsync();
            Assert.True(rerun.AlreadyComplete);
            Assert.Equal(0, rerun.InitializedCaveCount);
            Assert.Equal(rolloutResult.RevisionHistoryStartedOn, rerun.RevisionHistoryStartedOn);
            Assert.Single(await rerunContext.CaveRevisions.IgnoreQueryFilters().ToListAsync());
        }

        await database.MigrateAsync(migrations[baseline]);

        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using (var verify = new NpgsqlCommand("""
            select f."DisplayName", f."FileName",
                   to_regclass('public."CaveRevisions"') is null,
                   not exists(select 1 from information_schema.columns where table_name='Caves' and column_name='CurrentRevisionId'),
                   exists(select 1 from information_schema.columns where table_name='Caves' and column_name='ReportedByUserId'),
                   not exists(select 1 from "FeatureSettings" where "AccountId"='mainacct01' and "Key"='EnabledFieldEntranceOtherTags'),
                   (select column_default is null from information_schema.columns where table_name='Files' and column_name='FileName'),
                   to_regclass('public."__CaveRevisionsCompletionLegacyFileNames"') is null
            from "Files" f where f."Id"='mainfiler1'
            """, connection))
        await using (var reader = await verify.ExecuteReaderAsync())
        {
            Assert.True(await reader.ReadAsync());
            Assert.Equal("existing-cave", reader.GetString(0));
            Assert.Equal("existing-cave.pdf", reader.GetString(1));
            Assert.True(reader.GetBoolean(2));
            Assert.True(reader.GetBoolean(3));
            Assert.True(reader.GetBoolean(4));
            Assert.True(reader.GetBoolean(5));
            Assert.True(reader.GetBoolean(6));
            Assert.True(reader.GetBoolean(7));
        }

        await using var history = new NpgsqlCommand(
            "select \"MigrationId\" from \"__EFMigrationsHistory\" order by \"MigrationId\" desc limit 1", connection);
        Assert.Equal(CurrentMainBaselineMigration, (string?)await history.ExecuteScalarAsync());
    }

    [Fact]
    public async Task CurrentMainV32RoundTripPreservesDistinctLegacyFileName()
    {
        await using var database = await fixture.CreateUnmigratedDatabaseAsync(
            nameof(CurrentMainV32RoundTripPreservesDistinctLegacyFileName));
        var baseline = database.GetMigrationNames().Single(migration =>
            migration.EndsWith(CurrentMainBaselineMigration, StringComparison.Ordinal));
        await database.MigrateAsync(baseline);
        await SeedCurrentMainV32RepresentativeDataAsync(database);

        await using (var seed = new NpgsqlConnection(database.ConnectionString))
        {
            await seed.OpenAsync();
            await using var command = new NpgsqlCommand("""
                update "Files"
                set "FileName" = 'legacy-original.pdf', "DisplayName" = 'Friendly Label'
                where "Id" = 'mainfiler1'
                """, seed);
            await command.ExecuteNonQueryAsync();
        }

        await database.MigrateAsync(null);

        await using (var upgraded = new NpgsqlConnection(database.ConnectionString))
        {
            await upgraded.OpenAsync();
            await using var command = new NpgsqlCommand("""
                select f."Name", f."Extension", legacy."OriginalFileName",
                       legacy."MigratedName", legacy."MigratedExtension"
                from "Files" f
                join "__CaveRevisionsCompletionLegacyFileNames" legacy on legacy."FileId" = f."Id"
                where f."Id" = 'mainfiler1'
                """, upgraded);
            await using var reader = await command.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.Equal("Friendly Label", reader.GetString(0));
            Assert.Equal(".pdf", reader.GetString(1));
            Assert.Equal("legacy-original.pdf", reader.GetString(2));
            Assert.Equal("Friendly Label", reader.GetString(3));
            Assert.Equal(".pdf", reader.GetString(4));
        }

        await database.MigrateAsync(baseline);

        await using var verify = new NpgsqlConnection(database.ConnectionString);
        await verify.OpenAsync();
        await using var verifyCommand = new NpgsqlCommand("""
            select f."DisplayName", f."FileName",
                   (select column_default is null
                    from information_schema.columns
                    where table_name='Files' and column_name='FileName'),
                   to_regclass('public."__CaveRevisionsCompletionLegacyFileNames"') is null
            from "Files" f
            where f."Id" = 'mainfiler1'
            """, verify);
        await using var verifyReader = await verifyCommand.ExecuteReaderAsync();
        Assert.True(await verifyReader.ReadAsync());
        Assert.Equal("Friendly Label", verifyReader.GetString(0));
        Assert.Equal("legacy-original.pdf", verifyReader.GetString(1));
        Assert.True(verifyReader.GetBoolean(2));
        Assert.True(verifyReader.GetBoolean(3));
    }

    [Fact]
    public async Task CurrentMainV32RollbackUsesCurrentNameWhenFileMetadataChangedAfterUpgrade()
    {
        await using var database = await fixture.CreateUnmigratedDatabaseAsync(
            nameof(CurrentMainV32RollbackUsesCurrentNameWhenFileMetadataChangedAfterUpgrade));
        var baseline = database.GetMigrationNames().Single(migration =>
            migration.EndsWith(CurrentMainBaselineMigration, StringComparison.Ordinal));
        await database.MigrateAsync(baseline);
        await SeedCurrentMainV32RepresentativeDataAsync(database);

        await using (var seed = new NpgsqlConnection(database.ConnectionString))
        {
            await seed.OpenAsync();
            await using var command = new NpgsqlCommand("""
                update "Files"
                set "FileName" = 'legacy-original.pdf', "DisplayName" = 'Friendly Label'
                where "Id" = 'mainfiler1'
                """, seed);
            await command.ExecuteNonQueryAsync();
        }

        await database.MigrateAsync(null);

        await using (var edit = new NpgsqlConnection(database.ConnectionString))
        {
            await edit.OpenAsync();
            await using var command = new NpgsqlCommand("""
                update "Files"
                set "Name" = 'Renamed After Upgrade'
                where "Id" = 'mainfiler1'
                """, edit);
            await command.ExecuteNonQueryAsync();
        }

        await database.MigrateAsync(baseline);

        await using var verify = new NpgsqlConnection(database.ConnectionString);
        await verify.OpenAsync();
        await using var verifyCommand = new NpgsqlCommand("""
            select "DisplayName", "FileName"
            from "Files"
            where "Id" = 'mainfiler1'
            """, verify);
        await using var reader = await verifyCommand.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal("Renamed After Upgrade", reader.GetString(0));
        Assert.Equal("Renamed After Upgrade.pdf", reader.GetString(1));
    }

    [Fact]
    public async Task CurrentMainV32MigrationRejectsCaveFileOwnedByDifferentAccount()
    {
        await using var database = await fixture.CreateUnmigratedDatabaseAsync(
            nameof(CurrentMainV32MigrationRejectsCaveFileOwnedByDifferentAccount));
        var baseline = database.GetMigrationNames().Single(migration =>
            migration.EndsWith(CurrentMainBaselineMigration, StringComparison.Ordinal));
        await database.MigrateAsync(baseline);
        await SeedCurrentMainV32RepresentativeDataAsync(database);

        await using (var seed = new NpgsqlConnection(database.ConnectionString))
        {
            await seed.OpenAsync();
            await using var command = new NpgsqlCommand("""
                insert into "Accounts"("Id","Name","CountyIdDelimiter","DefaultViewAccessAllCaves","ExportEnabled","CreatedOn")
                values('otheracct1','Other Account','-',false,true,now());

                update "Files"
                set "AccountId" = 'otheracct1'
                where "Id" = 'mainfiler1';
                """, seed);
            await command.ExecuteNonQueryAsync();
        }

        var error = await Assert.ThrowsAsync<PostgresException>(() => database.MigrateAsync(null));
        Assert.Contains("does not match its owning Cave account", error.MessageText, StringComparison.Ordinal);

        await using var verify = new NpgsqlConnection(database.ConnectionString);
        await verify.OpenAsync();
        await using var verifyCommand = new NpgsqlCommand("""
            select f."AccountId",
                   exists(select 1 from information_schema.columns where table_name='Files' and column_name='DisplayName'),
                   not exists(select 1 from information_schema.columns where table_name='Files' and column_name='Name'),
                   (select "MigrationId" from "__EFMigrationsHistory" order by "MigrationId" desc limit 1)
            from "Files" f
            where f."Id" = 'mainfiler1'
            """, verify);
        await using var reader = await verifyCommand.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal("otheracct1", reader.GetString(0));
        Assert.True(reader.GetBoolean(1));
        Assert.True(reader.GetBoolean(2));
        Assert.Equal(CurrentMainBaselineMigration, reader.GetString(3));
    }

    [Fact]
    public async Task LegacyReporterUserColumnsAreDroppedWhileExistingCaveEntranceAndPeopleTagDataSurvives()
    {
        await using var database = await fixture.CreateUnmigratedDatabaseAsync(
            nameof(LegacyReporterUserColumnsAreDroppedWhileExistingCaveEntranceAndPeopleTagDataSurvives));
        var baseline = database.GetMigrationNames().Single(migration =>
            migration.EndsWith(CurrentMainBaselineMigration, StringComparison.Ordinal));
        await database.MigrateAsync(baseline);
        await SeedCurrentMainV32RepresentativeDataAsync(database);

        const string reporterUserId = "reportusr1";
        const string caveReporterId = "caverep00a";
        const string entranceReporterId = "entrep000a";
        const string entranceId = "legacyent1";

        await using (var connection = new NpgsqlConnection(database.ConnectionString))
        {
            await connection.OpenAsync();
            await using var seed = new NpgsqlCommand("""
                insert into "Users"("Id","EmailAddress","FirstName","LastName","IsTemporary","SessionVersion","CreatedOn")
                values ('reportusr1','reporter@example.test','Legacy','Reporter',false,0,now());

                insert into "TagTypes"("Id","AccountId","Key","Name","IsDefault","CreatedOn") values
                  ('caverep00a','mainacct01','People','Cave Reporter',false,now()),
                  ('entrep000a','mainacct01','People','Entrance Reporter',false,now()),
                  ('locqual00a','mainacct01','LocationQuality','Survey Grade',false,now());

                insert into "Entrances"("Id","CaveId","LocationQualityTagId","Name","IsPrimary","Description","Location","CreatedOn")
                values ('legacyent1','maincave01','locqual00a','Legacy entrance',true,'Preserve me',
                        ST_SetSRID(ST_MakePoint(-86.25,35.15,612),4326),now());

                insert into "CaveReportedByNameTags"("Id","TagTypeId","CaveId","CreatedOn")
                values ('caverept01','caverep00a','maincave01',now());
                insert into "EntranceReportedByNameTags"("Id","TagTypeId","EntranceId","CreatedOn")
                values ('entrept001','entrep000a','legacyent1',now());

                update "Caves" set "ReportedByUserId" = 'reportusr1' where "Id" = 'maincave01';
                update "Entrances" set "ReportedByUserId" = 'reportusr1' where "Id" = 'legacyent1';
                """, connection);
            await seed.ExecuteNonQueryAsync();
        }

        await database.MigrateAsync(null);

        await using (var verify = database.CreateDbContext("verify", "mainacct01"))
        {
            Assert.True(await verify.Caves.IgnoreQueryFilters().AnyAsync(cave => cave.Id == "maincave01"));
            var entrance = await verify.Entrances.IgnoreQueryFilters().SingleAsync(row => row.Id == entranceId);
            Assert.Equal("Preserve me", entrance.Description);
            Assert.Equal(-86.25, entrance.Location.X, 6);
            Assert.Equal(35.15, entrance.Location.Y, 6);
            Assert.Equal(612, entrance.Location.Z, 6);
            Assert.True(await verify.CaveReportedByNameTags.AnyAsync(tag =>
                tag.CaveId == "maincave01" && tag.TagTypeId == caveReporterId));
            Assert.True(await verify.EntranceReportedByNameTags.AnyAsync(tag =>
                tag.EntranceId == entranceId && tag.TagTypeId == entranceReporterId));
            Assert.True(await verify.Users.AnyAsync(user => user.Id == reporterUserId));
            Assert.True(await verify.TagTypes.AnyAsync(tag => tag.Id == caveReporterId));
            Assert.True(await verify.TagTypes.AnyAsync(tag => tag.Id == entranceReporterId));
        }

        await using var catalog = new NpgsqlConnection(database.ConnectionString);
        await catalog.OpenAsync();
        await using var command = new NpgsqlCommand("""
            select
              not exists(select 1 from information_schema.columns where table_name='Caves' and column_name='ReportedByUserId'),
              not exists(select 1 from information_schema.columns where table_name='Entrances' and column_name='ReportedByUserId'),
              not exists(select 1 from pg_constraint where conname in ('FK_Caves_Users_ReportedByUserId', 'FK_Entrances_Users_ReportedByUserId')),
              not exists(select 1 from pg_indexes where indexname in ('IX_Caves_ReportedByUserId', 'IX_Entrances_ReportedByUserId'))
            """, catalog);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.True(reader.GetBoolean(0));
        Assert.True(reader.GetBoolean(1));
        Assert.True(reader.GetBoolean(2));
        Assert.True(reader.GetBoolean(3));
    }

    [Fact]
    public async Task EntranceOtherBackfillRollbackPreservesPreexistingAccountOverride()
    {
        await using var database = await fixture.CreateUnmigratedDatabaseAsync(
            nameof(EntranceOtherBackfillRollbackPreservesPreexistingAccountOverride));
        var migrations = database.GetMigrationNames().ToList();
        var previous = migrations.FindIndex(migration =>
            migration.EndsWith(CaveRevisionsCompletionMigrationSuffix, StringComparison.Ordinal));
        Assert.True(previous >= 0);
        await database.MigrateAsync(migrations[previous]);
        var account = await AccountTestDataFactory.CreateAccountWithCountyAsync(database, 'a');
        var generatedAccount = await AccountTestDataFactory.CreateAccountWithCountyAsync(database, 'b');

        await using (var seed = database.CreateDbContext("seed", account.AccountId))
        {
            seed.FeatureSettings.Add(new FeatureSetting
            {
                Id = "manualothr",
                AccountId = account.AccountId,
                Key = FeatureKey.EnabledFieldEntranceOtherTags,
                IsEnabled = false,
                IsDefault = false
            });
            await seed.SaveChangesAsync();
        }

        await database.MigrateAsync(null);
        await using (var afterUp = database.CreateDbContext("after-up", generatedAccount.AccountId))
        {
            var generated = await afterUp.FeatureSettings.SingleAsync(row =>
                row.AccountId == generatedAccount.AccountId &&
                row.Key == FeatureKey.EnabledFieldEntranceOtherTags);
            Assert.True(generated.IsEnabled);
            Assert.False(generated.IsDefault);
        }

        await database.MigrateAsync(migrations[previous]);

        await using var verify = database.CreateDbContext("verify", account.AccountId);
        var setting = await verify.FeatureSettings.SingleAsync(row => row.Id == "manualothr");
        Assert.False(setting.IsEnabled);
        Assert.Equal(FeatureKey.EnabledFieldEntranceOtherTags, setting.Key);
        Assert.False(await verify.FeatureSettings.IgnoreQueryFilters().AnyAsync(row =>
            row.AccountId == generatedAccount.AccountId &&
            row.Key == FeatureKey.EnabledFieldEntranceOtherTags));
    }

    [Fact]
    public async Task V29HistoricalBaselineUpgradesWithoutCorruptingExistingTenantData()
    {
        // Arrange the frozen historical v29 schema and representative data.
        await using var database = await fixture.CreateUnmigratedDatabaseAsync(
            nameof(V29HistoricalBaselineUpgradesWithoutCorruptingExistingTenantData));
        var migrations = database.GetMigrationNames().ToList();
        var baseline = migrations.FindIndex(migration =>
            migration.EndsWith(HistoricalV29BaselineMigration, StringComparison.Ordinal));
        var currentMain = migrations.FindIndex(migration =>
            migration.EndsWith(CurrentMainBaselineMigration, StringComparison.Ordinal));
        var completion = migrations.FindIndex(migration =>
            migration.EndsWith(CaveRevisionsCompletionMigrationSuffix, StringComparison.Ordinal));
        Assert.True(baseline >= 0, $"Expected historical v29 baseline migration {HistoricalV29BaselineMigration} was not found.");
        Assert.True(currentMain > baseline);
        Assert.Equal(currentMain + 1, completion);
        await database.MigrateAsync(migrations[baseline]);
        await V29DatabaseSeeder.SeedRepresentativeTenantAsync(database);

        // Act
        await database.MigrateAsync(null);

        // Assert historical rows and spatial values survived unchanged.
        await using (var verify = database.CreateDbContext("main", "mainacct01"))
        {
            var cave = await verify.Caves.IgnoreQueryFilters().SingleAsync(candidate => candidate.Id == "maincave01");
            Assert.Equal(("Existing Main Cave", "mainacct01", null),
                (cave.Name, cave.AccountId, cave.CurrentRevisionId));
            Assert.True(await verify.AccountStates.AnyAsync(state =>
                state.AccountId == "mainacct01" && state.StateId == "mainstate1"));

            var geology = await verify.GeologyTags.SingleAsync(tag => tag.CaveId == "maincave01");
            Assert.Equal("maingeo001", geology.TagTypeId);

            var entrance = await verify.Entrances.IgnoreQueryFilters()
                .SingleAsync(candidate => candidate.Id == "mainentr01");
            Assert.Equal("Historic Entrance", entrance.Name);
            Assert.True(entrance.IsPrimary);
            Assert.Equal("Preserved entrance", entrance.Description);
            Assert.Equal(-86.25, entrance.Location.X, 6);
            Assert.Equal(35.15, entrance.Location.Y, 6);
            Assert.Equal(612, entrance.Location.Z, 6);
            Assert.Equal(4326, entrance.Location.SRID);
            Assert.Equal(18, entrance.PitDepthFeet);
            Assert.True(await verify.EntranceStatusTags.AnyAsync(tag =>
                tag.EntranceId == "mainentr01" && tag.TagTypeId == "mainstat01"));

            var files = await verify.Files.OrderBy(file => file.Id).ToListAsync();
            Assert.Equal(2, files.Count);
            Assert.Equal(("mainacct01", "maincave01", "existing-cave", ".pdf"),
                (files[0].AccountId, files[0].CaveId, files[0].Name, files[0].Extension));
            Assert.Equal(("mainacct01", (string?)null, "temporary", ".csv"),
                (files[1].AccountId, files[1].CaveId, files[1].Name, files[1].Extension));

            var entranceOtherSetting = await verify.FeatureSettings.SingleAsync(setting =>
                setting.AccountId == "mainacct01" &&
                setting.Key == FeatureKey.EnabledFieldEntranceOtherTags);
            Assert.True(entranceOtherSetting.IsEnabled);
            Assert.False(entranceOtherSetting.IsDefault);
        }

        // The newly-added xmin concurrency token must also work after upgrade.
        await using var currentDb = database.CreateDbContext("current", "mainacct01");
        await using var staleDb = database.CreateDbContext("stale", "mainacct01");
        var current = await currentDb.Caves.IgnoreQueryFilters().SingleAsync(cave => cave.Id == "maincave01");
        var stale = await staleDb.Caves.IgnoreQueryFilters().SingleAsync(cave => cave.Id == "maincave01");
        var originalVersion = current.Version;
        current.Name = "Updated after upgrade";
        await currentDb.SaveChangesAsync();
        Assert.NotEqual(originalVersion, current.Version);
        stale.Name = "Stale";
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => staleDb.SaveChangesAsync());
    }

    [Fact]
    public async Task MigratedLegacyCaveUsesRolloutBaselineForProposalAuthoringAndCanBeSubmitted()
    {
        await using var database = await fixture.CreateUnmigratedDatabaseAsync(
            nameof(MigratedLegacyCaveUsesRolloutBaselineForProposalAuthoringAndCanBeSubmitted));
        var migrations = database.GetMigrationNames().ToList();
        var baseline = migrations.FindIndex(migration =>
            migration.EndsWith(HistoricalV29BaselineMigration, StringComparison.Ordinal));
        await database.MigrateAsync(migrations[baseline]);
        await V29DatabaseSeeder.SeedRepresentativeTenantAsync(database);
        await database.MigrateAsync(null);

        const string accountId = "mainacct01";
        await using (var rollout = database.CreateDbContext("revision-rollout", accountId))
        {
            var result = await new CaveRevisionRolloutRepository(rollout).InitializeAsync();
            Assert.Equal(1, result.InitializedCaveCount);
        }


        const string caveId = "maincave01";
        const string contributorId = "contrib001";
        await using (var permissions = database.CreateDbContext(contributorId, accountId))
        {
            permissions.AccountUsers.Add(new AccountUser
            {
                AccountId = accountId,
                UserId = permissions.RequestUser.Id,
                InvitationAcceptedOn = DateTime.UtcNow
            });
            permissions.Permissions.Add(new Permission
            {
                Id = "legacyview", Key = "View", Name = "View", Description = "View Caves",
                PermissionType = "Cave"
            });
            permissions.CavePermissions.Add(new CavePermission
            {
                AccountId = accountId, CaveId = caveId, UserId = permissions.RequestUser.Id,
                PermissionId = "legacyview"
            });
            await permissions.SaveChangesAsync();
        }

        async Task<CaveProposalAuthoringContextVm> InitializeAsync(string contextName)
        {
            await using var db = database.CreateDbContext(contextName, accountId);
            await db.RequestUser.Initialize(accountId, contributorId);
            return await IntegrationTestServices.For(db).CaveChangeRequests.GetAuthoringContextAsync(caveId, default);
        }

        var contexts = await Task.WhenAll(InitializeAsync("legacy-init-a"), InitializeAsync("legacy-init-b"));
        Assert.All(contexts, context => Assert.False(string.IsNullOrWhiteSpace(context.ExpectedBaseRevisionId)));
        Assert.Equal(contexts[0].ExpectedBaseRevisionId, contexts[1].ExpectedBaseRevisionId);

        string requestId;
        await using (var contributor = database.CreateDbContext(contributorId, accountId))
        {
            await contributor.RequestUser.Initialize(accountId, contributorId);
            var service = IntegrationTestServices.For(contributor).CaveChangeRequests;
            var authoring = await service.GetAuthoringContextAsync(caveId, default);
            var values = LegacyValues(authoring.Cave);
            values.Name = "Proposed legacy Cave name";
            var preview = await service.PreviewAsync(caveId, values, authoring.ExpectedBaseRevisionId, default);
            Assert.Equal("Proposed legacy Cave name", preview.Proposed.Name);
            requestId = await service.CreateAsync(caveId, values, authoring.ExpectedBaseRevisionId, default);
        }

        await using var verify = database.CreateDbContext("verify", accountId);
        var cave = await verify.Caves.IgnoreQueryFilters().SingleAsync(row => row.Id == caveId);
        Assert.Equal(contexts[0].ExpectedBaseRevisionId, cave.CurrentRevisionId);
        var revisions = await verify.CaveRevisions.Where(revision => revision.CaveId == caveId).ToListAsync();
        Assert.Single(revisions);
        Assert.Equal(CaveRevisionSource.SystemBaseline, revisions[0].Source);
        Assert.Equal(CaveRevisionOperation.Baseline, revisions[0].Operation);
        Assert.Null(revisions[0].CreatedByUserId);
        var request = await verify.CaveChangeRequests.SingleAsync(row => row.Id == requestId);
        Assert.Equal(CaveChangeRequestStatus.Pending, request.Status);
        Assert.NotNull(request.CurrentProposalVersionId);
    }

    [Fact]
    public async Task FileNameMigrationPreservesLegacyDisplayNameAndBackfillsNameAndExtensionEdgeCases()
    {
        await using var database = await CreateV29DatabaseAsync(
            nameof(FileNameMigrationPreservesLegacyDisplayNameAndBackfillsNameAndExtensionEdgeCases));
        await V29DatabaseSeeder.SeedRepresentativeTenantAsync(database);
        var previousMigration = database.GetMigrationNames().Single(migration =>
            migration.EndsWith(CurrentMainBaselineMigration, StringComparison.Ordinal));
        await database.MigrateAsync(previousMigration);

        await using (var connection = new NpgsqlConnection(database.ConnectionString))
        {
            await connection.OpenAsync();
            await using var seed = new NpgsqlCommand("""
                update "Files" set "DisplayName" = 'Entrance Survey' where "Id" = 'mainfiler1';
                insert into "Files"("Id","AccountId","CaveId","FileTypeTagId","FileName","BlobKey","BlobContainer","CreatedOn") values
                  ('filemulti1','mainacct01','maincave01','mainfile01','survey.final.PDF','multi','main',now()),
                  ('fileplain1','mainacct01','maincave01','mainfile01','README','plain','main',now()),
                  ('filedot001','mainacct01','maincave01','mainfile01','.gitignore','dot','main',now()),
                  ('filetrail1','mainacct01','maincave01','mainfile01','survey.','trail','main',now()),
                  ('fileutf001','mainacct01','maincave01','mainfile01','Mügelhöhle.pdf','utf','main',now());
                update "Files" set "DisplayName" = '   ' where "Id" = 'fileplain1';
                """, connection);
            await seed.ExecuteNonQueryAsync();
        }

        await database.MigrateAsync(null);

        await using var verify = new NpgsqlConnection(database.ConnectionString);
        await verify.OpenAsync();
        var results = new Dictionary<string, (string Name, string Extension)>(StringComparer.Ordinal);
        await using (var command = new NpgsqlCommand(
                         "select \"Id\", \"Name\", \"Extension\" from \"Files\" order by \"Id\"", verify))
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
                results.Add(reader.GetString(0), (reader.GetString(1), reader.GetString(2)));
        }

        Assert.Equal(("Entrance Survey", ".pdf"), results["mainfiler1"]);
        Assert.Equal(("temporary", ".csv"), results["mainfilet1"]);
        Assert.Equal(("survey.final", ".PDF"), results["filemulti1"]);
        Assert.Equal(("README", ""), results["fileplain1"]);
        Assert.Equal((".gitignore", ""), results["filedot001"]);
        Assert.Equal(("survey.", ""), results["filetrail1"]);
        Assert.Equal(("Mügelhöhle", ".pdf"), results["fileutf001"]);

        await using var schema = new NpgsqlCommand("""
            select exists(select 1 from information_schema.columns where table_name='Files' and column_name='Name'),
                   exists(select 1 from information_schema.columns where table_name='Files' and column_name='Extension'),
                   exists(select 1 from information_schema.columns where table_name='Files' and column_name='FileName'),
                   exists(select 1 from information_schema.columns where table_name='Files' and column_name='DisplayName'),
                   exists(select 1 from pg_constraint where conname='CK_Files_ValidNameAndExtension')
            """, verify);
        await using var schemaReader = await schema.ExecuteReaderAsync();
        Assert.True(await schemaReader.ReadAsync());
        Assert.True(schemaReader.GetBoolean(0));
        Assert.True(schemaReader.GetBoolean(1));
        Assert.False(schemaReader.GetBoolean(2));
        Assert.False(schemaReader.GetBoolean(3));
        Assert.True(schemaReader.GetBoolean(4));
    }

    [Fact]
    public async Task FileNameMigrationFailsRatherThanTruncatingLegacyFileMetadata()
    {
        await using var database = await CreateV29DatabaseAsync(
            nameof(FileNameMigrationFailsRatherThanTruncatingLegacyFileMetadata));
        await V29DatabaseSeeder.SeedRepresentativeTenantAsync(database);
        var previousMigration = database.GetMigrationNames().Single(migration =>
            migration.EndsWith(CurrentMainBaselineMigration, StringComparison.Ordinal));
        await database.MigrateAsync(previousMigration);
        var displayName = new string('n', 100);
        var fileName = "x." + new string('e', 998);

        await using (var connection = new NpgsqlConnection(database.ConnectionString))
        {
            await connection.OpenAsync();
            await using var seed = new NpgsqlCommand("""
                update "Files"
                set "DisplayName" = @display, "FileName" = @file
                where "Id" = 'mainfiler1'
                """, connection);
            seed.Parameters.AddWithValue("display", displayName);
            seed.Parameters.AddWithValue("file", fileName);
            await seed.ExecuteNonQueryAsync();
        }

        var error = await Assert.ThrowsAsync<PostgresException>(() => database.MigrateAsync(null));
        Assert.Contains("would exceed 1000 characters", error.MessageText, StringComparison.Ordinal);

        await using var verify = new NpgsqlConnection(database.ConnectionString);
        await verify.OpenAsync();
        await using var command = new NpgsqlCommand("""
            select "DisplayName", "FileName",
                   exists(select 1 from information_schema.columns where table_name='Files' and column_name='Name'),
                   exists(select 1 from information_schema.columns where table_name='Files' and column_name='Extension')
            from "Files" where "Id"='mainfiler1'
            """, verify);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(displayName, reader.GetString(0));
        Assert.Equal(fileName, reader.GetString(1));
        Assert.False(reader.GetBoolean(2));
        Assert.False(reader.GetBoolean(3));
    }

    [Theory]
    [InlineData("nested/report", "path-separator")]
    [InlineData("\u00A0", "unicode-whitespace")]
    public async Task FileNameMigrationFailsWhenLegacyDisplayNameViolatesTheNewNamePolicy(
        string legacyDisplayName, string databaseSuffix)
    {
        await using var database = await CreateV29DatabaseAsync(
            $"{nameof(FileNameMigrationFailsWhenLegacyDisplayNameViolatesTheNewNamePolicy)}_{databaseSuffix}");
        await V29DatabaseSeeder.SeedRepresentativeTenantAsync(database);
        var previousMigration = database.GetMigrationNames().Single(migration =>
            migration.EndsWith(CurrentMainBaselineMigration, StringComparison.Ordinal));
        await database.MigrateAsync(previousMigration);

        await using (var connection = new NpgsqlConnection(database.ConnectionString))
        {
            await connection.OpenAsync();
            await using var update = new NpgsqlCommand("""
                update "Files"
                set "DisplayName" = @displayName
                where "Id" = 'mainfiler1'
                """, connection);
            update.Parameters.AddWithValue("displayName", legacyDisplayName);
            await update.ExecuteNonQueryAsync();
        }

        var error = await Assert.ThrowsAsync<PostgresException>(() => database.MigrateAsync(null));
        Assert.Contains("legacy DisplayName contains a file name that is invalid", error.MessageText,
            StringComparison.Ordinal);

        await using var verify = new NpgsqlConnection(database.ConnectionString);
        await verify.OpenAsync();
        await using var command = new NpgsqlCommand("""
            select "DisplayName", "FileName",
                   exists(select 1 from information_schema.columns where table_name='Files' and column_name='Name'),
                   exists(select 1 from pg_constraint where conname='CK_Files_ValidNameAndExtension')
            from "Files" where "Id"='mainfiler1'
            """, verify);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(legacyDisplayName, reader.GetString(0));
        Assert.Equal("existing-cave.pdf", reader.GetString(1));
        Assert.False(reader.GetBoolean(2));
        Assert.False(reader.GetBoolean(3));
    }

    [Fact]
    public async Task FileNameMigrationDownRestoresLegacyNameColumnsWithoutChangingTheFilename()
    {
        await using var database = await CreateV29DatabaseAsync(
            nameof(FileNameMigrationDownRestoresLegacyNameColumnsWithoutChangingTheFilename));
        await V29DatabaseSeeder.SeedRepresentativeTenantAsync(database);
        await database.MigrateAsync(null);

        await using (var connection = new NpgsqlConnection(database.ConnectionString))
        {
            await connection.OpenAsync();
            await using var update = new NpgsqlCommand("""
                update "Files"
                set "Name" = 'Entrance Survey', "Extension" = '.PDF'
                where "Id" = 'mainfiler1'
                """, connection);
            await update.ExecuteNonQueryAsync();
        }

        var previousMigration = database.GetMigrationNames().Single(migration =>
            migration.EndsWith(CurrentMainBaselineMigration, StringComparison.Ordinal));
        await database.MigrateAsync(previousMigration);

        await using var verify = new NpgsqlConnection(database.ConnectionString);
        await verify.OpenAsync();
        await using var command = new NpgsqlCommand("""
            select "DisplayName", "FileName",
                   exists(select 1 from information_schema.columns where table_name='Files' and column_name='Name'),
                   exists(select 1 from information_schema.columns where table_name='Files' and column_name='Extension'),
                   exists(select 1 from pg_constraint where conname='CK_Files_ValidNameAndExtension')
            from "Files" where "Id"='mainfiler1'
            """, verify);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal("Entrance Survey", reader.GetString(0));
        Assert.Equal("Entrance Survey.PDF", reader.GetString(1));
        Assert.False(reader.GetBoolean(2));
        Assert.False(reader.GetBoolean(3));
        Assert.False(reader.GetBoolean(4));
    }

    [Fact]
    public async Task FileNameMigrationDownPreservesTheV32DisplayNameWidth()
    {
        await using var database = await CreateV29DatabaseAsync(
            nameof(FileNameMigrationDownPreservesTheV32DisplayNameWidth));
        await V29DatabaseSeeder.SeedRepresentativeTenantAsync(database);
        await database.MigrateAsync(null);
        var name = new string('n', 996);
        const string extension = ".pdf";

        await using (var connection = new NpgsqlConnection(database.ConnectionString))
        {
            await connection.OpenAsync();
            await using var update = new NpgsqlCommand("""
                update "Files"
                set "Name" = @name, "Extension" = @extension
                where "Id" = 'mainfiler1'
                """, connection);
            update.Parameters.AddWithValue("name", name);
            update.Parameters.AddWithValue("extension", extension);
            await update.ExecuteNonQueryAsync();
        }

        var previousMigration = database.GetMigrationNames().Single(migration =>
            migration.EndsWith(CurrentMainBaselineMigration, StringComparison.Ordinal));
        await database.MigrateAsync(previousMigration);

        await using var verify = new NpgsqlConnection(database.ConnectionString);
        await verify.OpenAsync();
        await using var command = new NpgsqlCommand("""
            select "DisplayName", "FileName",
                   character_maximum_length
            from "Files"
            join information_schema.columns on table_name='Files' and column_name='DisplayName'
            where "Id"='mainfiler1'
            """, verify);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(name, reader.GetString(0));
        Assert.Equal(name + extension, reader.GetString(1));
        Assert.Equal(1000, reader.GetInt32(2));
    }

    [Fact]
    public async Task V29LegacyCaveFileWithNullOwnerIsBackfilledFromItsCave()
    {
        await using var database = await CreateV29DatabaseAsync(nameof(V29LegacyCaveFileWithNullOwnerIsBackfilledFromItsCave));
        await V29DatabaseSeeder.SeedFileOwnershipScenarioAsync(database, "legfile01", accountId: null, caveId: "legcave01",
            fileName: "legacy-cave.pdf", blobKey: "caves/legcave01/files/legfile01.pdf");

        await database.MigrateAsync(null);

        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""
            select f."AccountId", f."CaveId", f."Name", f."Extension", f."BlobKey",
                   exists(select 1 from pg_constraint where conname = 'FK_Files_Accounts_AccountId'),
                   exists(select 1 from pg_constraint where conname = 'AK_Files_AccountId_Id'),
                   exists(select 1 from pg_constraint where conname = 'FK_CaveChangeRequestStagedFiles_Files_AccountId_FileId')
            from "Files" f where f."Id" = 'legfile01'
            """, connection);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal("legacyacct", reader.GetString(0));
        Assert.Equal("legcave01", reader.GetString(1));
        Assert.Equal("legacy-cave", reader.GetString(2));
        Assert.Equal(".pdf", reader.GetString(3));
        Assert.Equal("caves/legcave01/files/legfile01.pdf", reader.GetString(4));
        Assert.True(reader.GetBoolean(5));
        Assert.True(reader.GetBoolean(7));
    }

    [Fact]
    public async Task V29TemporaryAccountFileSurvivesOwnershipMigrationUnchanged()
    {
        await using var database = await CreateV29DatabaseAsync(nameof(V29TemporaryAccountFileSurvivesOwnershipMigrationUnchanged));
        await V29DatabaseSeeder.SeedFileOwnershipScenarioAsync(database, "temporary1", "legacyacct", null,
            "temporary.csv", "temp/import/temporary1.csv");

        await database.MigrateAsync(null);

        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("select \"AccountId\", \"CaveId\", \"Name\", \"Extension\", \"BlobKey\" from \"Files\" where \"Id\"='temporary1'", connection);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal("legacyacct", reader.GetString(0));
        Assert.True(reader.IsDBNull(1));
        Assert.Equal("temporary", reader.GetString(2));
        Assert.Equal(".csv", reader.GetString(3));
        Assert.Equal("temp/import/temporary1.csv", reader.GetString(4));
    }

    [Fact]
    public async Task V29UnassignableNullOwnerFileFailsClosedAndLeavesCurrentMainSchema()
    {
        await using var database = await CreateV29DatabaseAsync(nameof(V29UnassignableNullOwnerFileFailsClosedAndLeavesCurrentMainSchema));
        await V29DatabaseSeeder.SeedFileOwnershipScenarioAsync(database, "orphanfile", null, null,
            "orphan.pdf", "orphan/orphanfile.pdf");

        var error = await Assert.ThrowsAsync<PostgresException>(() => database.MigrateAsync(null));
        Assert.Contains("Cannot migrate Files.AccountId", error.MessageText, StringComparison.Ordinal);

        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""
            select "AccountId" is null, is_nullable,
                   (select "MigrationId" from "__EFMigrationsHistory" order by "MigrationId" desc limit 1)
            from "Files"
            join information_schema.columns on table_name='Files' and column_name='AccountId'
            where "Id"='orphanfile'
            """, connection);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.True(reader.GetBoolean(0));
        Assert.Equal("YES", reader.GetString(1));
        Assert.Equal(CurrentMainBaselineMigration, reader.GetString(2));
    }

    private static AddCaveVm LegacyValues(CaveVm cave) => new()
    {
        Id = cave.Id,
        Name = cave.Name,
        AlternateNames = cave.AlternateNames,
        StateId = cave.StateId,
        CountyId = cave.CountyId,
        CountyNumber = cave.CountyNumber,
        IsCountyNumberManuallySet = true,
        LengthFeet = cave.LengthFeet ?? 0,
        DepthFeet = cave.DepthFeet ?? 0,
        MaxPitDepthFeet = cave.MaxPitDepthFeet ?? 0,
        NumberOfPits = cave.NumberOfPits ?? 0,
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
            { Id = file.Id, FileTypeTagId = file.FileTypeTagId, Name = file.Name }).ToList(),
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

    private static async Task SeedCurrentMainV32RepresentativeDataAsync(PostgresTestDatabase database)
    {
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            insert into "States"("Id","Name","Abbreviation","CreatedOn")
            values('mainstate1','Tennessee','TN',now());

            insert into "Accounts"("Id","Name","CountyIdDelimiter","DefaultViewAccessAllCaves","ExportEnabled","CreatedOn")
            values('mainacct01','Existing Main Account','-',false,true,now());

            insert into "Counties"("Id","AccountId","StateId","DisplayId","Name","CreatedOn")
            values('maincnty01','mainacct01','mainstate1','MAIN','Existing County',now());

            insert into "Caves"("Id","AccountId","StateId","CountyId","Name","AlternateNames","CountyNumber","IsArchived","CreatedOn")
            values('maincave01','mainacct01','mainstate1','maincnty01','Existing Main Cave','[]',42,false,now());

            insert into "TagTypes"("Id","AccountId","Key","Name","IsDefault","CreatedOn")
            values('mainfile01','mainacct01','File','Existing file',false,now());

            insert into "Files"("Id","AccountId","CaveId","FileTypeTagId","FileName","DisplayName","BlobKey","BlobContainer","CreatedOn")
            values('mainfiler1','mainacct01','maincave01','mainfile01','existing-cave.pdf','existing-cave',
                   'caves/maincave01/files/mainfiler1.pdf','main',now());
            """;
        await command.ExecuteNonQueryAsync();
    }

    private async Task<PostgresTestDatabase> CreateV29DatabaseAsync(string name)
    {
        var database = await fixture.CreateUnmigratedDatabaseAsync(name);
        var migration = database.GetMigrationNames().Single(x => x.EndsWith(HistoricalV29BaselineMigration, StringComparison.Ordinal));
        await database.MigrateAsync(migration);
        return database;
    }

}
