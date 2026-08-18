using Microsoft.EntityFrameworkCore;
using Npgsql;
using Planarian.Model.Database.Entities;
using Planarian.Model.Database.Entities.RidgeWalker;
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
    private const string MainBaselineMigration = "20260423021136_v29";
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
    public async Task ProposalVersionBaseMigrationBackfillsExistingVersionFromItsRequest()
    {
        await using var database = await fixture.CreateUnmigratedDatabaseAsync(
            nameof(ProposalVersionBaseMigrationBackfillsExistingVersionFromItsRequest));
        var previousMigration = database.GetMigrationNames().Single(migration =>
            migration.EndsWith("_StagedFileTenantForeignKey", StringComparison.Ordinal));
        await database.MigrateAsync(previousMigration);
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');

        await using (var connection = new NpgsqlConnection(database.ConnectionString))
        {
            await connection.OpenAsync();
            await using var seed = new NpgsqlCommand("""
                insert into "CaveChangeRequests"
                    ("Id", "AccountId", "CaveId", "BaseRevisionId", "Status", "CreatedOn")
                values ('oldreq0001', @account, @cave, @revision, 'Pending', now());

                insert into "CaveProposalVersions"
                    ("Id", "AccountId", "ChangeRequestId", "SchemaVersion", "ProposalJson", "CreatedOn")
                values ('oldver0001', @account, 'oldreq0001', 1, '{}'::jsonb, now());

                update "CaveChangeRequests" set "CurrentProposalVersionId" = 'oldver0001'
                where "Id" = 'oldreq0001';
                """, connection);
            seed.Parameters.AddWithValue("account", tenant.AccountId);
            seed.Parameters.AddWithValue("cave", tenant.CaveId);
            seed.Parameters.AddWithValue("revision", tenant.RevisionId);
            await seed.ExecuteNonQueryAsync();
        }

        await database.MigrateAsync(null);

        await using var verifyConnection = new NpgsqlConnection(database.ConnectionString);
        await verifyConnection.OpenAsync();
        await using var verify = new NpgsqlCommand("""
            select "CaveId", "BaseRevisionId"
            from "CaveProposalVersions" where "Id" = 'oldver0001'
            """, verifyConnection);
        await using var reader = await verify.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(tenant.CaveId, reader.GetString(0));
        Assert.Equal(tenant.RevisionId, reader.GetString(1));
    }

    [Fact]
    public async Task LegacyReporterUserColumnsAreDroppedWhileExistingCaveEntranceAndPeopleTagDataSurvives()
    {
        await using var database = await fixture.CreateUnmigratedDatabaseAsync(
            nameof(LegacyReporterUserColumnsAreDroppedWhileExistingCaveEntranceAndPeopleTagDataSurvives));
        var previousMigration = database.GetMigrationNames().Single(migration =>
            migration.EndsWith("_CaveProposalVersionBaseRevision", StringComparison.Ordinal));
        await database.MigrateAsync(previousMigration);
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var caveReporter = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.People, "Cave Reporter", "caverep00a");
        var entranceReporter = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.People, "Entrance Reporter", "entrep000a");
        var location = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        const string entranceId = "legacyent1";
        string reporterUserId;

        await using (var seed = database.CreateDbContext("legacy-reporter-seed", tenant.AccountId))
        {
            reporterUserId = await seed.Users.Select(user => user.Id).FirstAsync();
            seed.Entrances.Add(new Entrance
            {
                Id = entranceId,
                CaveId = tenant.CaveId,
                Name = "Legacy entrance",
                IsPrimary = true,
                Description = "Preserve me",
                LocationQualityTagId = location.Id,
                Location = new NetTopologySuite.Geometries.Point(-86.25, 35.15, 612) { SRID = 4326 }
            });
            seed.CaveReportedByNameTags.Add(new CaveReportedByNameTag
                { CaveId = tenant.CaveId, TagTypeId = caveReporter.Id });
            seed.EntranceReportedByNameTags.Add(new EntranceReportedByNameTag
                { EntranceId = entranceId, TagTypeId = entranceReporter.Id });
            await seed.SaveChangesAsync();
        }

        await using (var connection = new NpgsqlConnection(database.ConnectionString))
        {
            await connection.OpenAsync();
            await using var setLegacyReporter = new NpgsqlCommand("""
                update "Caves" set "ReportedByUserId" = @user where "Id" = @cave;
                update "Entrances" set "ReportedByUserId" = @user where "Id" = @entrance;
                """, connection);
            setLegacyReporter.Parameters.AddWithValue("user", reporterUserId);
            setLegacyReporter.Parameters.AddWithValue("cave", tenant.CaveId);
            setLegacyReporter.Parameters.AddWithValue("entrance", entranceId);
            await setLegacyReporter.ExecuteNonQueryAsync();
        }

        await database.MigrateAsync(null);

        await using (var verify = database.CreateDbContext("verify", tenant.AccountId))
        {
            Assert.True(await verify.Caves.IgnoreQueryFilters().AnyAsync(cave => cave.Id == tenant.CaveId));
            var entrance = await verify.Entrances.IgnoreQueryFilters().SingleAsync(row => row.Id == entranceId);
            Assert.Equal("Preserve me", entrance.Description);
            Assert.Equal(-86.25, entrance.Location.X, 6);
            Assert.Equal(35.15, entrance.Location.Y, 6);
            Assert.Equal(612, entrance.Location.Z, 6);
            Assert.True(await verify.CaveReportedByNameTags.AnyAsync(tag =>
                tag.CaveId == tenant.CaveId && tag.TagTypeId == caveReporter.Id));
            Assert.True(await verify.EntranceReportedByNameTags.AnyAsync(tag =>
                tag.EntranceId == entranceId && tag.TagTypeId == entranceReporter.Id));
            Assert.True(await verify.Users.AnyAsync(user => user.Id == reporterUserId));
            Assert.True(await verify.TagTypes.AnyAsync(tag => tag.Id == caveReporter.Id));
            Assert.True(await verify.TagTypes.AnyAsync(tag => tag.Id == entranceReporter.Id));
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
    public async Task ExactMainSchemaUpgradesWithoutCorruptingExistingTenantData()
    {
        // Arrange the exact pre-foundation production schema and representative data.
        await using var database = await fixture.CreateUnmigratedDatabaseAsync(
            nameof(ExactMainSchemaUpgradesWithoutCorruptingExistingTenantData));
        var migrations = database.GetMigrationNames().ToList();
        var baseline = migrations.FindIndex(migration =>
            migration.EndsWith(MainBaselineMigration, StringComparison.Ordinal));
        var foundation = migrations.FindIndex(migration =>
            migration.EndsWith("_CaveRevisionImportFoundation", StringComparison.Ordinal));
        Assert.True(baseline >= 0, $"Expected main baseline migration {MainBaselineMigration} was not found.");
        Assert.Equal(baseline + 1, foundation);
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
    public async Task MigratedLegacyCaveInitializesOneBaselineForProposalAuthoringAndCanBeSubmitted()
    {
        await using var database = await fixture.CreateUnmigratedDatabaseAsync(
            nameof(MigratedLegacyCaveInitializesOneBaselineForProposalAuthoringAndCanBeSubmitted));
        var migrations = database.GetMigrationNames().ToList();
        var baseline = migrations.FindIndex(migration =>
            migration.EndsWith(MainBaselineMigration, StringComparison.Ordinal));
        await database.MigrateAsync(migrations[baseline]);
        await V29DatabaseSeeder.SeedRepresentativeTenantAsync(database);
        await database.MigrateAsync(null);

        const string accountId = "mainacct01";
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
        Assert.Equal(CaveRevisionOperation.Create, revisions[0].Operation);
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
            migration.EndsWith("_RetainedCaveFileObjects", StringComparison.Ordinal));
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
            migration.EndsWith("_RetainedCaveFileObjects", StringComparison.Ordinal));
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
            migration.EndsWith("_RetainedCaveFileObjects", StringComparison.Ordinal));
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
            migration.EndsWith("_RetainedCaveFileObjects", StringComparison.Ordinal));
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
    public async Task FileNameMigrationDownFailsRatherThanTruncatingLegacyDisplayName()
    {
        await using var database = await CreateV29DatabaseAsync(
            nameof(FileNameMigrationDownFailsRatherThanTruncatingLegacyDisplayName));
        await V29DatabaseSeeder.SeedRepresentativeTenantAsync(database);
        await database.MigrateAsync(null);
        var name = new string('n', 101);
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
            migration.EndsWith("_RetainedCaveFileObjects", StringComparison.Ordinal));
        var error = await Assert.ThrowsAsync<PostgresException>(() => database.MigrateAsync(previousMigration));
        Assert.Contains("cannot fit the legacy DisplayName column", error.MessageText, StringComparison.Ordinal);

        await using var verify = new NpgsqlConnection(database.ConnectionString);
        await verify.OpenAsync();
        await using var command = new NpgsqlCommand("""
            select "Name", "Extension",
                   exists(select 1 from information_schema.columns where table_name='Files' and column_name='FileName'),
                   exists(select 1 from information_schema.columns where table_name='Files' and column_name='DisplayName')
            from "Files" where "Id"='mainfiler1'
            """, verify);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(name, reader.GetString(0));
        Assert.Equal(extension, reader.GetString(1));
        Assert.False(reader.GetBoolean(2));
        Assert.False(reader.GetBoolean(3));
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
    public async Task V29UnassignableNullOwnerFileFailsClosedAndLeavesV29Schema()
    {
        await using var database = await CreateV29DatabaseAsync(nameof(V29UnassignableNullOwnerFileFailsClosedAndLeavesV29Schema));
        await V29DatabaseSeeder.SeedFileOwnershipScenarioAsync(database, "orphanfile", null, null,
            "orphan.pdf", "orphan/orphanfile.pdf");

        var error = await Assert.ThrowsAsync<PostgresException>(() => database.MigrateAsync(null));
        Assert.Contains("Cannot migrate Files.AccountId", error.MessageText, StringComparison.Ordinal);

        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("select \"AccountId\" is null, is_nullable from \"Files\" join information_schema.columns on table_name='Files' and column_name='AccountId' where \"Id\"='orphanfile'", connection);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.True(reader.GetBoolean(0));
        Assert.Equal("YES", reader.GetString(1));
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

    private async Task<PostgresTestDatabase> CreateV29DatabaseAsync(string name)
    {
        var database = await fixture.CreateUnmigratedDatabaseAsync(name);
        var migration = database.GetMigrationNames().Single(x => x.EndsWith(MainBaselineMigration, StringComparison.Ordinal));
        await database.MigrateAsync(migration);
        return database;
    }

}
