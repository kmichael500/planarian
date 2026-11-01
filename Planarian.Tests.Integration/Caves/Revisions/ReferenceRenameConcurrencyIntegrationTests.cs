using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.Revisions;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Helpers;
using Planarian.Modules.Account.Model;
using Planarian.Modules.Caves.Revisions;
using Planarian.Tests.Integration.Infrastructure.Actors;
using Planarian.Tests.Integration.Infrastructure.Concurrency;
using Planarian.Tests.Integration.Infrastructure.Services;
using Xunit;

using Planarian.Tests;

namespace Planarian.Tests.Integration.Caves.Revisions;

public sealed class ReferenceRenameConcurrencyIntegrationTests(PostgresTestServer fixture)
    : IClassFixture<PostgresTestServer>
{
    [Fact]
    public async Task TagRenameWinsThenWaitingCaveWriterRejectsStaleRevision()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(TagRenameWinsThenWaitingCaveWriterRejectsStaleRevision));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var quality = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        var biology = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.Biology, "Cricket", "biology00a");
        cave = cave with { RevisionId = await AttachBiologyAndPublishAsync(database, cave, biology.Id) };
        await CavePermissions.GrantManagerAsync(database, cave, "writer");

        var pause = new HoldDestructiveTagLockInterceptor();
        await using var admin = database.CreateDbContext("manager", cave.AccountId, pause);
        var rename = IntegrationTestServices.For(admin).Account.CreateOrUpdateTagType(
            new CreateEditTagTypeVm { Name = "Cave Cricket", Key = TagTypeKeyConstant.Biology },
            biology.Id, default);
        await pause.LockAcquired.Task.WaitAsync(TimeSpan.FromSeconds(15));

        await using var writer = database.CreateDbContext("writer", cave.AccountId);
        await CavePermissions.AuthenticateAsync(writer, cave.AccountId);
        await writer.Database.OpenConnectionAsync();
        var writerPid = await PostgresLockAssertions.BackendPidAsync(writer);
        var values = CaveChangeRequestTestSupport.PublishableValues(cave, quality.Id, "Stale writer");
        values.BiologyTagIds = [biology.Id];
        var write = IntegrationTestServices.For(writer).Caves.AddCave(values, default);
        await PostgresLockAssertions.AssertBlockedAsync(database, cave.AccountId, writerPid, write);

        pause.Resume.TrySetResult();
        await rename;
        await Assert.ThrowsAsync<CaveRevisionConflictException>(() => write);

        await using var verify = database.CreateDbContext("verify", cave.AccountId);
        Assert.Equal("Cave Cricket",
            await verify.TagTypes.Where(row => row.Id == biology.Id).Select(row => row.Name).SingleAsync());
        var persisted = await verify.Caves.IgnoreQueryFilters().SingleAsync(row => row.Id == cave.CaveId);
        Assert.Equal(cave.CaveName, persisted.Name);
        var renameRevision = await verify.CaveRevisions.SingleAsync(row => row.Id == persisted.CurrentRevisionId);
        Assert.Equal(cave.RevisionId, renameRevision.PreviousRevisionId);
        Assert.Equal(CaveRevisionSource.AdministrativeChange, renameRevision.Source);
    }

    [Fact]
    public async Task CaveWriterWinsThenWaitingTagRenamePublishesNextRevision()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(CaveWriterWinsThenWaitingTagRenamePublishesNextRevision));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var quality = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        var biology = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.Biology, "Cricket", "biology00a");
        await CavePermissions.GrantManagerAsync(database, cave, "writer");

        var pause = new HoldAcquiredTagReferenceLockInterceptor();
        await using var writer = database.CreateDbContext("writer", cave.AccountId, pause);
        await CavePermissions.AuthenticateAsync(writer, cave.AccountId);
        var values = CaveChangeRequestTestSupport.PublishableValues(cave, quality.Id, "Writer committed first");
        values.BiologyTagIds = [biology.Id];
        var write = IntegrationTestServices.For(writer).Caves.AddCave(values, default);
        await pause.LockAcquired.Task.WaitAsync(TimeSpan.FromSeconds(15));

        await using var admin = database.CreateDbContext("manager", cave.AccountId);
        await admin.Database.OpenConnectionAsync();
        var adminPid = await PostgresLockAssertions.BackendPidAsync(admin);
        var rename = IntegrationTestServices.For(admin).Account.CreateOrUpdateTagType(
            new CreateEditTagTypeVm { Name = "Cave Cricket", Key = TagTypeKeyConstant.Biology },
            biology.Id, default);
        await PostgresLockAssertions.AssertBlockedAsync(database, cave.AccountId, adminPid, rename);

        pause.Resume.TrySetResult();
        await write;
        string writerRevisionId;
        await using (var afterWriter = database.CreateDbContext("after-writer", cave.AccountId))
            writerRevisionId = await afterWriter.Caves.IgnoreQueryFilters()
                .Where(row => row.Id == cave.CaveId).Select(row => row.CurrentRevisionId!).SingleAsync();

        await rename;

        await using var verify = database.CreateDbContext("verify", cave.AccountId);
        var persisted = await verify.Caves.IgnoreQueryFilters().SingleAsync(row => row.Id == cave.CaveId);
        Assert.Equal("Writer committed first", persisted.Name);
        Assert.NotEqual(writerRevisionId, persisted.CurrentRevisionId);
        var renameRevision = await verify.CaveRevisions.SingleAsync(row => row.Id == persisted.CurrentRevisionId);
        Assert.Equal(writerRevisionId, renameRevision.PreviousRevisionId);
        var snapshot = CaveSnapshotJson.Deserialize(renameRevision.SnapshotJson, renameRevision.SnapshotSchemaVersion);
        Assert.Contains(snapshot.Tags,
            tag => tag.TagTypeId == biology.Id && tag.NameAtRevision == "Cave Cricket");
    }

    [Fact]
    public async Task ProposalCreationWinsThenWaitingTagRenameLeavesRequestCompatible()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(ProposalCreationWinsThenWaitingTagRenameLeavesRequestCompatible));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var quality = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        var biology = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.Biology, "Cricket", "biology00a");
        cave = cave with { RevisionId = await AttachBiologyAndPublishAsync(database, cave, biology.Id) };
        await CavePermissions.GrantViewAsync(database, cave, "contributor");

        var pause = new HoldAcquiredCaveKeyShareLockInterceptor();
        await using var contributor = database.CreateDbContext("contributor", cave.AccountId, pause);
        await CavePermissions.AuthenticateAsync(contributor, cave.AccountId);
        var values = CaveChangeRequestTestSupport.PublishableValues(cave, quality.Id, "Created before rename");
        values.BiologyTagIds = [biology.Id];
        var create = IntegrationTestServices.For(contributor).CaveChangeRequests.CreateAsync(
            cave.CaveId, values, cave.RevisionId, default);
        await pause.LockAcquired.Task.WaitAsync(TimeSpan.FromSeconds(15));

        await using var admin = database.CreateDbContext("manager", cave.AccountId);
        await admin.Database.OpenConnectionAsync();
        var adminPid = await PostgresLockAssertions.BackendPidAsync(admin);
        var rename = IntegrationTestServices.For(admin).Account.CreateOrUpdateTagType(
            new CreateEditTagTypeVm { Name = "Cave Cricket", Key = TagTypeKeyConstant.Biology },
            biology.Id, default);
        try
        {
            await PostgresLockAssertions.AssertBlockedAsync(database, cave.AccountId, adminPid, rename);
        }
        finally
        {
            pause.Resume.TrySetResult();
        }

        var requestId = await create;
        await rename;

        await using var reader = await CaveTestActor.CreateAsync(database, cave.AccountId, "contributor");
        var detail = await reader.ChangeRequests.GetAsync(requestId, default);
        Assert.False(detail.Request.IsStale);
        Assert.NotEqual(detail.Request.ProposalBaseRevisionId, detail.Request.CurrentRevisionId);
        Assert.Null(detail.PublishedSinceBase);

        await using var verify = database.CreateDbContext("verify", cave.AccountId);
        var version = await verify.CaveProposalVersions.SingleAsync(row =>
            row.Id == detail.Request.CurrentProposalVersionId);
        var proposal = CaveProposalJson.Deserialize(version.ProposalJson, version.SchemaVersion);
        Assert.Contains(proposal.Tags,
            tag => tag.TagTypeId == biology.Id && tag.NameAtRevision == "Cricket");
        var currentRevision = await verify.CaveRevisions.SingleAsync(row =>
            row.Id == detail.Request.CurrentRevisionId);
        Assert.Equal(CaveRevisionSource.AdministrativeChange, currentRevision.Source);
        var current = CaveSnapshotJson.Deserialize(
            currentRevision.SnapshotJson, currentRevision.SnapshotSchemaVersion);
        Assert.Contains(current.Tags,
            tag => tag.TagTypeId == biology.Id && tag.NameAtRevision == "Cave Cricket");
    }

    [Fact]
    public async Task TagRenameWinsThenWaitingProposalApprovalPublishesAfterRename()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(TagRenameWinsThenWaitingProposalApprovalPublishesAfterRename));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var quality = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        var biology = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.Biology, "Cricket", "biology00a");
        cave = cave with { RevisionId = await AttachBiologyAndPublishAsync(database, cave, biology.Id) };
        await CavePermissions.GrantViewAsync(database, cave, "contributor");
        await CavePermissions.GrantManagerAsync(database, cave, "reviewer");

        string requestId;
        string versionId;
        await using (var contributor = database.CreateDbContext("contributor", cave.AccountId))
        {
            await CavePermissions.AuthenticateAsync(contributor, cave.AccountId);
            var values = CaveChangeRequestTestSupport.PublishableValues(cave, quality.Id, "Approved after rename");
            values.BiologyTagIds = [biology.Id];
            requestId = await IntegrationTestServices.For(contributor).CaveChangeRequests
                .CreateAsync(cave.CaveId, values, cave.RevisionId, default);
            versionId = await CaveChangeRequestTestSupport.CurrentVersionAsync(contributor, requestId);
        }

        var pause = new HoldDestructiveTagLockInterceptor();
        await using var admin = database.CreateDbContext("manager", cave.AccountId, pause);
        var rename = IntegrationTestServices.For(admin).Account.CreateOrUpdateTagType(
            new CreateEditTagTypeVm { Name = "Cave Cricket", Key = TagTypeKeyConstant.Biology },
            biology.Id, default);
        await pause.LockAcquired.Task.WaitAsync(TimeSpan.FromSeconds(15));

        await using var reviewer = database.CreateDbContext("reviewer", cave.AccountId);
        await CavePermissions.AuthenticateAsync(reviewer, cave.AccountId);
        await reviewer.Database.OpenConnectionAsync();
        var reviewerPid = await PostgresLockAssertions.BackendPidAsync(reviewer);
        var approval = IntegrationTestServices.For(reviewer).CaveChangeRequests
            .ApproveAsync(requestId, versionId, null, default);
        try
        {
            await PostgresLockAssertions.AssertBlockedAsync(database, cave.AccountId, reviewerPid, approval);
        }
        finally
        {
            pause.Resume.TrySetResult();
        }

        await rename;
        var decision = await approval;

        await using var verify = database.CreateDbContext("verify", cave.AccountId);
        var accepted = await verify.CaveRevisions.SingleAsync(row => row.Id == decision.PublishedRevisionId);
        var renameRevision = await verify.CaveRevisions.SingleAsync(row => row.Id == accepted.PreviousRevisionId);
        Assert.Equal(CaveRevisionSource.AdministrativeChange, renameRevision.Source);
        Assert.Equal(cave.RevisionId, renameRevision.PreviousRevisionId);
        var snapshot = CaveSnapshotJson.Deserialize(accepted.SnapshotJson, accepted.SnapshotSchemaVersion);
        Assert.Contains(snapshot.Tags,
            tag => tag.TagTypeId == biology.Id && tag.NameAtRevision == "Cave Cricket");
        Assert.Equal(CaveChangeRequestStatus.Approved,
            (await verify.CaveChangeRequests.SingleAsync(row => row.Id == requestId)).Status);
    }

    [Fact]
    public async Task ProposalApprovalWinsThenWaitingTagRenamePublishesAfterApproval()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(ProposalApprovalWinsThenWaitingTagRenamePublishesAfterApproval));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var quality = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        var biology = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.Biology, "Cricket", "biology00a");
        cave = cave with { RevisionId = await AttachBiologyAndPublishAsync(database, cave, biology.Id) };
        await CavePermissions.GrantViewAsync(database, cave, "contributor");
        await CavePermissions.GrantManagerAsync(database, cave, "reviewer");

        string requestId;
        string versionId;
        await using (var contributor = database.CreateDbContext("contributor", cave.AccountId))
        {
            await CavePermissions.AuthenticateAsync(contributor, cave.AccountId);
            var values = CaveChangeRequestTestSupport.PublishableValues(cave, quality.Id, "Approved before rename");
            values.BiologyTagIds = [biology.Id];
            requestId = await IntegrationTestServices.For(contributor).CaveChangeRequests
                .CreateAsync(cave.CaveId, values, cave.RevisionId, default);
            versionId = await CaveChangeRequestTestSupport.CurrentVersionAsync(contributor, requestId);
        }

        var pause = new HoldAcquiredTagReferenceLockInterceptor();
        await using var reviewer = database.CreateDbContext("reviewer", cave.AccountId, pause);
        await CavePermissions.AuthenticateAsync(reviewer, cave.AccountId);
        var approval = IntegrationTestServices.For(reviewer).CaveChangeRequests
            .ApproveAsync(requestId, versionId, null, default);
        await pause.LockAcquired.Task.WaitAsync(TimeSpan.FromSeconds(15));

        await using var admin = database.CreateDbContext("manager", cave.AccountId);
        await admin.Database.OpenConnectionAsync();
        var adminPid = await PostgresLockAssertions.BackendPidAsync(admin);
        var rename = IntegrationTestServices.For(admin).Account.CreateOrUpdateTagType(
            new CreateEditTagTypeVm { Name = "Cave Cricket", Key = TagTypeKeyConstant.Biology },
            biology.Id, default);
        try
        {
            await PostgresLockAssertions.AssertBlockedAsync(database, cave.AccountId, adminPid, rename);
        }
        finally
        {
            pause.Resume.TrySetResult();
        }

        var decision = await approval;
        await rename;

        await using var verify = database.CreateDbContext("verify", cave.AccountId);
        var accepted = await verify.CaveRevisions.SingleAsync(row => row.Id == decision.PublishedRevisionId);
        Assert.Equal(cave.RevisionId, accepted.PreviousRevisionId);
        var acceptedSnapshot = CaveSnapshotJson.Deserialize(accepted.SnapshotJson, accepted.SnapshotSchemaVersion);
        Assert.Contains(acceptedSnapshot.Tags,
            tag => tag.TagTypeId == biology.Id && tag.NameAtRevision == "Cricket");

        var currentId = await verify.Caves.IgnoreQueryFilters().Where(row => row.Id == cave.CaveId)
            .Select(row => row.CurrentRevisionId!).SingleAsync();
        var renameRevision = await verify.CaveRevisions.SingleAsync(row => row.Id == currentId);
        Assert.Equal(CaveRevisionSource.AdministrativeChange, renameRevision.Source);
        Assert.Equal(decision.PublishedRevisionId, renameRevision.PreviousRevisionId);
        var currentSnapshot = CaveSnapshotJson.Deserialize(renameRevision.SnapshotJson, renameRevision.SnapshotSchemaVersion);
        Assert.Contains(currentSnapshot.Tags,
            tag => tag.TagTypeId == biology.Id && tag.NameAtRevision == "Cave Cricket");
    }

    [Fact]
    public async Task CountyRenameWinsThenWaitingProposalApprovalPublishesAfterRename()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(CountyRenameWinsThenWaitingProposalApprovalPublishesAfterRename));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var quality = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        await CavePermissions.GrantViewAsync(database, cave, "contributor");
        await CavePermissions.GrantManagerAsync(database, cave, "reviewer");

        string requestId;
        string versionId;
        await using (var contributor = database.CreateDbContext("contributor", cave.AccountId))
        {
            await CavePermissions.AuthenticateAsync(contributor, cave.AccountId);
            var values = CaveChangeRequestTestSupport.PublishableValues(cave, quality.Id, "Approved after county rename");
            values.IsCountyNumberManuallySet = false;
            requestId = await IntegrationTestServices.For(contributor).CaveChangeRequests
                .CreateAsync(cave.CaveId, values, cave.RevisionId, default);
            versionId = await CaveChangeRequestTestSupport.CurrentVersionAsync(contributor, requestId);
        }

        var pause = new HoldCountyLockInterceptor("for update");
        await using var admin = database.CreateDbContext("manager", cave.AccountId, pause);
        var rename = IntegrationTestServices.For(admin).Account.CreateOrUpdateCounty(
            cave.StateId,
            new CreateCountyVm { Name = "Renamed County", CountyDisplayId = "R99" },
            cave.CountyId, default);
        await pause.LockAcquired.Task.WaitAsync(TimeSpan.FromSeconds(15));

        await using var reviewer = database.CreateDbContext("reviewer", cave.AccountId);
        await CavePermissions.AuthenticateAsync(reviewer, cave.AccountId);
        await reviewer.Database.OpenConnectionAsync();
        var reviewerPid = await PostgresLockAssertions.BackendPidAsync(reviewer);
        var approval = IntegrationTestServices.For(reviewer).CaveChangeRequests
            .ApproveAsync(requestId, versionId, null, default);
        try
        {
            await PostgresLockAssertions.AssertBlockedAsync(database, cave.AccountId, reviewerPid, approval);
        }
        finally
        {
            pause.Resume.TrySetResult();
        }

        await rename;
        var decision = await approval;

        await using var verify = database.CreateDbContext("verify", cave.AccountId);
        var accepted = await verify.CaveRevisions.SingleAsync(row => row.Id == decision.PublishedRevisionId);
        var renameRevision = await verify.CaveRevisions.SingleAsync(row => row.Id == accepted.PreviousRevisionId);
        Assert.Equal(CaveRevisionSource.AdministrativeChange, renameRevision.Source);
        Assert.Equal(cave.RevisionId, renameRevision.PreviousRevisionId);
        var snapshot = CaveSnapshotJson.Deserialize(accepted.SnapshotJson, accepted.SnapshotSchemaVersion);
        Assert.Equal(cave.CountyId, snapshot.County.Id);
        Assert.Equal("Renamed County", snapshot.County.NameAtRevision);
        Assert.Equal("R99", snapshot.County.DisplayIdAtRevision);
    }

    [Fact]
    public async Task ProposalApprovalWinsThenWaitingCountyRenamePublishesAfterApproval()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(ProposalApprovalWinsThenWaitingCountyRenamePublishesAfterApproval));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var quality = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        await CavePermissions.GrantViewAsync(database, cave, "contributor");
        await CavePermissions.GrantManagerAsync(database, cave, "reviewer");

        string requestId;
        string versionId;
        await using (var contributor = database.CreateDbContext("contributor", cave.AccountId))
        {
            await CavePermissions.AuthenticateAsync(contributor, cave.AccountId);
            var values = CaveChangeRequestTestSupport.PublishableValues(cave, quality.Id, "Approved before county rename");
            values.IsCountyNumberManuallySet = false;
            requestId = await IntegrationTestServices.For(contributor).CaveChangeRequests
                .CreateAsync(cave.CaveId, values, cave.RevisionId, default);
            versionId = await CaveChangeRequestTestSupport.CurrentVersionAsync(contributor, requestId);
        }

        var pause = new HoldCountyLockInterceptor("for key share");
        await using var reviewer = database.CreateDbContext("reviewer", cave.AccountId, pause);
        await CavePermissions.AuthenticateAsync(reviewer, cave.AccountId);
        var approval = IntegrationTestServices.For(reviewer).CaveChangeRequests
            .ApproveAsync(requestId, versionId, null, default);
        await pause.LockAcquired.Task.WaitAsync(TimeSpan.FromSeconds(15));

        await using var admin = database.CreateDbContext("manager", cave.AccountId);
        await admin.Database.OpenConnectionAsync();
        var adminPid = await PostgresLockAssertions.BackendPidAsync(admin);
        var rename = IntegrationTestServices.For(admin).Account.CreateOrUpdateCounty(
            cave.StateId,
            new CreateCountyVm { Name = "Renamed County", CountyDisplayId = "R99" },
            cave.CountyId, default);
        try
        {
            await PostgresLockAssertions.AssertBlockedAsync(database, cave.AccountId, adminPid, rename);
        }
        finally
        {
            pause.Resume.TrySetResult();
        }

        var decision = await approval;
        await rename;

        await using var verify = database.CreateDbContext("verify", cave.AccountId);
        var accepted = await verify.CaveRevisions.SingleAsync(row => row.Id == decision.PublishedRevisionId);
        Assert.Equal(cave.RevisionId, accepted.PreviousRevisionId);
        var acceptedSnapshot = CaveSnapshotJson.Deserialize(accepted.SnapshotJson, accepted.SnapshotSchemaVersion);
        Assert.Equal(cave.CountyId, acceptedSnapshot.County.Id);

        var currentId = await verify.Caves.IgnoreQueryFilters().Where(row => row.Id == cave.CaveId)
            .Select(row => row.CurrentRevisionId!).SingleAsync();
        var renameRevision = await verify.CaveRevisions.SingleAsync(row => row.Id == currentId);
        Assert.Equal(CaveRevisionSource.AdministrativeChange, renameRevision.Source);
        Assert.Equal(decision.PublishedRevisionId, renameRevision.PreviousRevisionId);
        var currentSnapshot = CaveSnapshotJson.Deserialize(renameRevision.SnapshotJson, renameRevision.SnapshotSchemaVersion);
        Assert.Equal("Renamed County", currentSnapshot.County.NameAtRevision);
        Assert.Equal("R99", currentSnapshot.County.DisplayIdAtRevision);
    }

    [Fact]
    public async Task CountyRenameWinsThenWaitingCaveWriterRejectsStaleRevision()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(CountyRenameWinsThenWaitingCaveWriterRejectsStaleRevision));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var quality = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        await CavePermissions.GrantManagerAsync(database, cave, "writer");

        var pause = new HoldCountyLockInterceptor("for update");
        await using var admin = database.CreateDbContext("manager", cave.AccountId, pause);
        var rename = IntegrationTestServices.For(admin).Account.CreateOrUpdateCounty(
            cave.StateId,
            new CreateCountyVm { Name = "Renamed County", CountyDisplayId = "R99" },
            cave.CountyId, default);
        await pause.LockAcquired.Task.WaitAsync(TimeSpan.FromSeconds(15));

        await using var writer = database.CreateDbContext("writer", cave.AccountId);
        await CavePermissions.AuthenticateAsync(writer, cave.AccountId);
        await writer.Database.OpenConnectionAsync();
        var writerPid = await PostgresLockAssertions.BackendPidAsync(writer);
        var values = CaveChangeRequestTestSupport.PublishableValues(cave, quality.Id, "Stale writer");
        var write = IntegrationTestServices.For(writer).Caves.AddCave(values, default);
        await PostgresLockAssertions.AssertBlockedAsync(database, cave.AccountId, writerPid, write);

        pause.Resume.TrySetResult();
        await rename;
        await Assert.ThrowsAsync<CaveRevisionConflictException>(() => write);

        await using var verify = database.CreateDbContext("verify", cave.AccountId);
        var county = await verify.Counties.SingleAsync(row => row.Id == cave.CountyId);
        Assert.Equal("Renamed County", county.Name);
        Assert.Equal("R99", county.DisplayId);
        var persisted = await verify.Caves.IgnoreQueryFilters().SingleAsync(row => row.Id == cave.CaveId);
        Assert.Equal(cave.CaveName, persisted.Name);
        var renameRevision = await verify.CaveRevisions.SingleAsync(row => row.Id == persisted.CurrentRevisionId);
        Assert.Equal(cave.RevisionId, renameRevision.PreviousRevisionId);
        Assert.Equal(CaveRevisionSource.AdministrativeChange, renameRevision.Source);
    }

    private static async Task<string> AttachBiologyAndPublishAsync(
        PostgresTestDatabase database, PublishedCaveTestData cave, string tagId)
    {
        await using var db = database.CreateDbContext("attach-biology", cave.AccountId);
        db.BiologyTags.Add(new BiologyTag
            { Id = IdGenerator.Generate(), CaveId = cave.CaveId, TagTypeId = tagId });
        await db.SaveChangesAsync();
        return (await new CaveMutationPersistence(db, db.RequestUser,
                new CavePublishedSnapshotReader(db, db.RequestUser))
            .PublishExistingAsync(cave.CaveId, cave.RevisionId, CaveRevisionSource.ManagerEdit,
                CaveRevisionOperation.Update, _ => { })).RevisionId!;
    }
}

internal sealed class HoldAcquiredCaveKeyShareLockInterceptor : DbCommandInterceptor
{
    private int _paused;
    public TaskCompletionSource LockAcquired { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Resume { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command,
        CommandExecutedEventData eventData, DbDataReader result,
        CancellationToken cancellationToken = default)
    {
        if (command.CommandText.Contains("from \"Caves\"", StringComparison.OrdinalIgnoreCase) &&
            command.CommandText.Contains("for key share", StringComparison.OrdinalIgnoreCase) &&
            Interlocked.Exchange(ref _paused, 1) == 0)
        {
            LockAcquired.TrySetResult();
            await Resume.Task.WaitAsync(cancellationToken);
        }

        return result;
    }
}
