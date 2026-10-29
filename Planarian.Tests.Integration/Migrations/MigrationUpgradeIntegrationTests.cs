using Microsoft.EntityFrameworkCore;
using Npgsql;
using Planarian.Model.Database.Entities.RidgeWalker;
using Xunit;
using static Planarian.Tests.MigrationTestSupport;

namespace Planarian.Tests;

[Collection(MigrationIntegrationCollection.Name)]
public sealed class MigrationUpgradeIntegrationTests(PostgresTestServer fixture) : IClassFixture<PostgresTestServer>
{
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
        var v32Schema = await CapturePublicSchemaContractAsync(database);
        await SeedCurrentMainV32RepresentativeDataAsync(database);

        await database.MigrateAsync(null);

        await using (var current = database.CreateDbContext("current-main-upgrade", "mainacct01"))
        {
            var cave = await current.Caves.IgnoreQueryFilters().SingleAsync(row => row.Id == "maincave01");
            Assert.Null(cave.CurrentRevisionId);

            var file = await current.Files.SingleAsync(row => row.Id == "mainfiler1");
            Assert.Equal("existing-cave", file.Name);
            Assert.Equal(".pdf", file.Extension);

            var generatedSetting = await current.FeatureSettings.SingleAsync(setting =>
                setting.AccountId == "mainacct01" &&
                setting.Key == FeatureKey.EnabledFieldEntranceOtherTags);
            Assert.True(generatedSetting.IsEnabled);
            Assert.False(generatedSetting.IsDefault);
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

        var rolledBackSchema = await CapturePublicSchemaContractAsync(database);
        Assert.Equal(v32Schema, rolledBackSchema);
    }

    [Fact]
    public async Task CurrentMainV32RollbackSucceedsAfterNewRevisionSchemaContainsData()
    {
        await using var database = await CreateCurrentMainV32DatabaseAsync(
            fixture, nameof(CurrentMainV32RollbackSucceedsAfterNewRevisionSchemaContainsData));
        var baseline = database.GetMigrationNames().Single(migration =>
            migration.EndsWith(CurrentMainBaselineMigration, StringComparison.Ordinal));
        await database.MigrateAsync(null);

        // Create a real actor through the ordinary test context, then populate every major
        // completion-owned relational area with valid rows. Migration rollback should not
        // depend on those new tables being empty.
        using (database.CreateDbContext("reviewusr1", "mainacct01")) { }

        await using (var connection = new NpgsqlConnection(database.ConnectionString))
        {
            await connection.OpenAsync();
            await using var seedCommand = new NpgsqlCommand("""
                insert into "CaveRevisions"
                    ("Id","AccountId","CaveId","Source","Operation","SnapshotSchemaVersion","SnapshotJson","CreatedOn")
                values ('revbase001','mainacct01','maincave01','SystemBaseline','Create',1,'{}'::jsonb,now());

                update "Caves" set "CurrentRevisionId"='revbase001' where "Id"='maincave01';

                insert into "CaveChangeRequests"
                    ("Id","AccountId","CaveId","BaseRevisionId","Status","CreatedOn")
                values ('request001','mainacct01','maincave01','revbase001','Pending',now());

                insert into "CaveProposalVersions"
                    ("Id","AccountId","ChangeRequestId","CaveId","BaseRevisionId","SchemaVersion","ProposalJson","CreatedOn")
                values ('proposal01','mainacct01','request001','maincave01','revbase001',1,'{}'::jsonb,now());

                update "CaveChangeRequests"
                set "CurrentProposalVersionId"='proposal01'
                where "Id"='request001';

                insert into "CaveChangeRequestStagedFiles"
                    ("Id","AccountId","ChangeRequestId","FileId","CreatedOn")
                values ('staged0001','mainacct01','request001','mainfiler1',now());

                insert into "CaveChangeRequestReviewEvents"
                    ("Id","AccountId","ChangeRequestId","ProposalVersionId","Action","ActorUserId","CreatedOn")
                values ('review0001','mainacct01','request001','proposal01','Submitted','reviewusr1',now());

                insert into "CaveImportBatches"
                    ("Id","AccountId","SyncExisting","Kind","SourceRecordCount","InsertedCount","UpdatedCount","DeletedCount","NoChangeCount","CreatedOn")
                values ('import0001','mainacct01',false,'Cave',1,0,1,0,0,now());

                insert into "CaveRevisions"
                    ("Id","AccountId","CaveId","PreviousRevisionId","Source","Operation","SnapshotSchemaVersion","SnapshotJson","ImportBatchId","CreatedOn")
                values ('revimport1','mainacct01','maincave01','revbase001','Import','Update',1,'{}'::jsonb,'import0001',now());

                """, connection);
            await seedCommand.ExecuteNonQueryAsync();
        }

        await database.MigrateAsync(baseline);

        await using var verify = new NpgsqlConnection(database.ConnectionString);
        await verify.OpenAsync();
        await using var command = new NpgsqlCommand("""
            select
              exists(select 1 from "Caves" where "Id"='maincave01'),
              exists(select 1 from "Files" where "Id"='mainfiler1' and "FileName"='existing-cave.pdf'),
              to_regclass('public."CaveRevisions"') is null,
              to_regclass('public."CaveChangeRequests"') is null,
              to_regclass('public."CaveProposalVersions"') is null,
              to_regclass('public."CaveChangeRequestStagedFiles"') is null,
              to_regclass('public."CaveChangeRequestReviewEvents"') is null,
              to_regclass('public."CaveImportBatches"') is null,
              to_regclass('public."RetainedCaveFileObjects"') is null
            """, verify);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        for (var index = 0; index < 9; index++)
            Assert.True(reader.GetBoolean(index));
    }


    [Fact]
    public async Task NewCaveProposalMigrationRefusesDowngradeWhileCreationProposalExists()
    {
        await using var database = await CreateCurrentMainV32DatabaseAsync(
            fixture, nameof(NewCaveProposalMigrationRefusesDowngradeWhileCreationProposalExists));
        var migrations = database.GetMigrationNames().ToList();
        var supportIndex = migrations.FindIndex(migration =>
            migration.EndsWith("_SupportNewCaveProposals", StringComparison.Ordinal));
        Assert.True(supportIndex > 0);

        await database.MigrateAsync(migrations[supportIndex]);

        await using (var connection = new NpgsqlConnection(database.ConnectionString))
        {
            await connection.OpenAsync();
            await using var seed = new NpgsqlCommand("""
                insert into "CaveChangeRequests"
                    ("Id","AccountId","CaveId","BaseRevisionId","Status","CreatedOn")
                values ('newreq0001','mainacct01','future0001',null,'Pending',now());

                insert into "CaveProposalVersions"
                    ("Id","AccountId","ChangeRequestId","CaveId","BaseRevisionId","SchemaVersion","ProposalJson","CreatedOn")
                values ('newprop001','mainacct01','newreq0001','future0001',null,1,'{}'::jsonb,now());

                update "CaveChangeRequests"
                set "CurrentProposalVersionId"='newprop001'
                where "Id"='newreq0001';
                """, connection);
            await seed.ExecuteNonQueryAsync();
        }

        var exception = await Assert.ThrowsAsync<PostgresException>(() =>
            database.MigrateAsync(migrations[supportIndex - 1]));
        Assert.Equal(PostgresErrorCodes.RaiseException, exception.SqlState);
        Assert.Contains("Cannot downgrade SupportNewCaveProposals", exception.MessageText);

        await using var verify = new NpgsqlConnection(database.ConnectionString);
        await verify.OpenAsync();
        await using var command = new NpgsqlCommand("""
            select
                (select is_nullable='YES'
                 from information_schema.columns
                 where table_name='CaveProposalVersions' and column_name='BaseRevisionId'),
                exists(select 1 from "CaveProposalVersions"
                       where "Id"='newprop001' and "BaseRevisionId" is null),
                exists(select 1 from "__EFMigrationsHistory"
                       where "MigrationId" like '%_SupportNewCaveProposals')
            """, verify);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.True(reader.GetBoolean(0));
        Assert.True(reader.GetBoolean(1));
        Assert.True(reader.GetBoolean(2));
    }


    [Fact]
    public async Task PermissionDomainMigrationRemovesMalformedAdminExpansionWithoutBreakingRealAdmin()
    {
        await using var database = await CreateCurrentMainV32DatabaseAsync(
            fixture, nameof(PermissionDomainMigrationRemovesMalformedAdminExpansionWithoutBreakingRealAdmin));
        var migrations = database.GetMigrationNames().ToList();
        var supportIndex = migrations.FindIndex(migration =>
            migration.EndsWith("_SupportNewCaveProposals", StringComparison.Ordinal));
        var hardeningIndex = migrations.FindIndex(migration =>
            migration.EndsWith("_HardenPermissionDomainProjection", StringComparison.Ordinal));
        var identityIndex = migrations.FindIndex(migration =>
            migration.EndsWith("_EnforcePermissionDomainIdentity", StringComparison.Ordinal));
        Assert.True(supportIndex >= 0);
        Assert.Equal(supportIndex + 1, hardeningIndex);
        Assert.Equal(hardeningIndex + 1, identityIndex);

        await database.MigrateAsync(migrations[supportIndex]);

        await using (var connection = new NpgsqlConnection(database.ConnectionString))
        {
            await connection.OpenAsync();
            await using var seed = new NpgsqlCommand("""
                insert into "Users"("Id","FirstName","LastName","EmailAddress","SessionVersion","IsTemporary","CreatedOn")
                values
                  ('badmigr001','Bad','Migration','bad-migration@example.test',0,false,now()),
                  ('goodmigr01','Good','Migration','good-migration@example.test',0,false,now());

                insert into "AccountUsers"
                    ("AccountId","UserId","Id","InvitationAcceptedOn","CreatedOn")
                values
                  ('mainacct01','badmigr001','badacct001',now(),now()),
                  ('mainacct01','goodmigr01','goodacct01',now(),now());

                insert into "Permissions"
                    ("Id","Name","Description","IsHidden","Key","SortOrder","PermissionType","CreatedOn")
                values
                  ('badadm003','Wrong Type Admin','Regression-only Cave Admin',false,'Admin',99,'Cave',now()),
                  ('goodadm003','Account Admin','Regression-only valid Account Admin',false,'Admin',99,'User',now()),
                  ('migview001','View','Regression-only valid Cave View',false,'View',0,'Cave',now()),
                  ('migmgr0001','Manager','Regression-only valid Cave Manager',false,'Manager',1,'Cave',now());

                insert into "UserPermissions"
                    ("Id","UserId","PermissionId","AccountId","CreatedOn")
                values
                  ('baduperm01','badmigr001','badadm003','mainacct01',now()),
                  ('gooduperm1','goodmigr01','goodadm003','mainacct01',now());

                insert into "CavePermission"
                    ("Id","UserId","PermissionId","AccountId","CreatedOn")
                values ('badcperm01','badmigr001','badadm003','mainacct01',now());
                """, connection);
            await seed.ExecuteNonQueryAsync();

            await using var before = new NpgsqlCommand("""
                select
                  (select count(*) from "UserCavePermissions"
                   where "AccountId"='mainacct01' and "UserId"='badmigr001' and "CaveId" is not null),
                  (select count(*) from "UserCavePermissions"
                   where "AccountId"='mainacct01' and "UserId"='goodmigr01' and "CaveId" is not null)
                """, connection);
            await using var beforeReader = await before.ExecuteReaderAsync();
            Assert.True(await beforeReader.ReadAsync());
            Assert.True(beforeReader.GetInt64(0) > 0);
            Assert.True(beforeReader.GetInt64(1) > 0);
        }

        await database.MigrateAsync(migrations[hardeningIndex]);

        await using var verify = new NpgsqlConnection(database.ConnectionString);
        await verify.OpenAsync();
        await using var after = new NpgsqlCommand("""
            select
              (select count(*) from "UserCavePermissions"
               where "AccountId"='mainacct01' and "UserId"='badmigr001'),
              (select count(*) from "UserCavePermissions"
               where "AccountId"='mainacct01' and "UserId"='goodmigr01' and "CaveId" is not null)
            """, verify);
        await using var afterReader = await after.ExecuteReaderAsync();
        Assert.True(await afterReader.ReadAsync());
        Assert.Equal(0, afterReader.GetInt64(0));
        Assert.True(afterReader.GetInt64(1) > 0);
        await afterReader.DisposeAsync();

        await database.MigrateAsync(migrations[identityIndex]);

        await using var identityVerify = new NpgsqlConnection(database.ConnectionString);
        await identityVerify.OpenAsync();
        await using var identity = new NpgsqlCommand("""
            select
              (select count(*) from "Permissions"
               where "Key"='Admin' and "PermissionType" in ('Cave','User')),
              exists (
                select 1
                from pg_indexes
                where schemaname='public'
                  and tablename='Permissions'
                  and indexname='IX_Permissions_PermissionType_Key'
                  and indexdef like 'CREATE UNIQUE INDEX%'
              )
            """, identityVerify);
        await using var identityReader = await identity.ExecuteReaderAsync();
        Assert.True(await identityReader.ReadAsync());
        Assert.Equal(2, identityReader.GetInt64(0));
        Assert.True(identityReader.GetBoolean(1));
    }

    [Fact]
    public async Task PermissionDomainIdentityMigrationFailsClosedOnLegacySameDomainDuplicates()
    {
        await using var database = await CreateCurrentMainV32DatabaseAsync(
            fixture, nameof(PermissionDomainIdentityMigrationFailsClosedOnLegacySameDomainDuplicates));
        var migrations = database.GetMigrationNames().ToList();
        var hardeningIndex = migrations.FindIndex(migration =>
            migration.EndsWith("_HardenPermissionDomainProjection", StringComparison.Ordinal));
        var identityIndex = migrations.FindIndex(migration =>
            migration.EndsWith("_EnforcePermissionDomainIdentity", StringComparison.Ordinal));
        Assert.True(hardeningIndex >= 0);
        Assert.Equal(hardeningIndex + 1, identityIndex);

        await database.MigrateAsync(migrations[hardeningIndex]);

        await using (var connection = new NpgsqlConnection(database.ConnectionString))
        {
            await connection.OpenAsync();
            await using var seed = new NpgsqlCommand("""
                insert into "Permissions"
                    ("Id","Name","Description","IsHidden","Key","SortOrder","PermissionType","CreatedOn")
                values
                  ('dupadmin01','Duplicate Admin One','Legacy duplicate',false,'DuplicateRole',1,'User',now()),
                  ('dupadmin02','Duplicate Admin Two','Legacy duplicate',false,'DuplicateRole',2,'User',now());
                """, connection);
            await seed.ExecuteNonQueryAsync();
        }

        var failure = await Assert.ThrowsAsync<PostgresException>(
            () => database.MigrateAsync(migrations[identityIndex]));
        Assert.Equal("P0001", failure.SqlState);
        Assert.Contains("duplicate (PermissionType, Key) rows exist", failure.MessageText);

        await using var verify = new NpgsqlConnection(database.ConnectionString);
        await verify.OpenAsync();
        await using var index = new NpgsqlCommand("""
            select exists (
              select 1
              from pg_indexes
              where schemaname='public'
                and tablename='Permissions'
                and indexname='IX_Permissions_PermissionType_Key'
                and indexdef like 'CREATE UNIQUE INDEX%'
            )
            """, verify);
        Assert.False((bool)(await index.ExecuteScalarAsync())!);
    }


    [Fact]
    public async Task StorageCleanupMigrationPreservesResetDebtAndConvertsRetainedFilesToAssetCleanupDebt()
    {
        await using var database = await fixture.CreateUnmigratedDatabaseAsync(
            nameof(StorageCleanupMigrationPreservesResetDebtAndConvertsRetainedFilesToAssetCleanupDebt));
        var previous = database.GetMigrationNames().Single(migration =>
            migration.EndsWith("_AccountResetCleanupOutbox", StringComparison.Ordinal));
        await database.MigrateAsync(previous);

        await using (var connection = new NpgsqlConnection(database.ConnectionString))
        {
            await connection.OpenAsync();
            await using var seed = new NpgsqlCommand("""
                insert into "AccountResetCleanupTargets"
                    ("Id","AccountId","StoragePartition","StorageKey","IsContainer","CreatedOn")
                values ('sameid0001','mainacct01','portable-store','reset/pending',false,now());

                insert into "RetainedCaveFileObjects"
                    ("Id","AccountId","CaveId","FileId","StoragePartition","StorageKey","CreatedOn")
                values ('sameid0001','mainacct01','maincave01','removed001','portable-store','files/removed.pdf',now());
                """, connection);
            await seed.ExecuteNonQueryAsync();
        }

        await database.MigrateAsync(null);

        await using var verify = new NpgsqlConnection(database.ConnectionString);
        await verify.OpenAsync();
        await using (var rows = new NpgsqlCommand("""
            select "Purpose", "StoragePartition", "StorageKey"
            from "StorageCleanupTargets"
            where "AccountId"='mainacct01'
            order by "Purpose" collate "C", "StorageKey" collate "C"
            """, verify))
        await using (var reader = await rows.ExecuteReaderAsync())
        {
            var actual = new List<(string Purpose, string Partition, string Key)>();
            while (await reader.ReadAsync())
                actual.Add((reader.GetString(0), reader.GetString(1), reader.GetString(2)));
            Assert.Equal(
            [
                ("AccountReset", "portable-store", "reset/pending"),
                ("CaveAssetRemoval", "portable-store", "files/removed.pdf")
            ], actual);
        }

        await using var removed = new NpgsqlCommand(
            "select to_regclass('public.\"RetainedCaveFileObjects\"') is null", verify);
        Assert.True((bool)(await removed.ExecuteScalarAsync())!);
    }

    [Theory]
    [InlineData("CaveAssetRemoval")]
    [InlineData("UnpublishedFileRemoval")]
    public async Task StorageCleanupMigrationRefusesDowngradeWhileOrdinaryFileCleanupDebtExists(string purpose)
    {
        await using var database = await fixture.CreateUnmigratedDatabaseAsync(
            $"{nameof(StorageCleanupMigrationRefusesDowngradeWhileOrdinaryFileCleanupDebtExists)}_{purpose}");
        var migrations = database.GetMigrationNames().ToList();
        var previous = migrations.Single(migration =>
            migration.EndsWith("_AccountResetCleanupOutbox", StringComparison.Ordinal));
        await database.MigrateAsync(null);

        await using (var connection = new NpgsqlConnection(database.ConnectionString))
        {
            await connection.OpenAsync();
            await using var seed = new NpgsqlCommand("""
                insert into "StorageCleanupTargets"
                    ("Id","AccountId","StoragePartition","StorageKey","IsContainer","Purpose","CreatedOn")
                values ('cleanup001','mainacct01','portable-store','files/pending.pdf',false,@purpose,now());
                """, connection);
            seed.Parameters.AddWithValue("purpose", purpose);
            await seed.ExecuteNonQueryAsync();
        }

        await Assert.ThrowsAsync<PostgresException>(() => database.MigrateAsync(previous));
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

}
