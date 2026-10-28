using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Planarian.Library.Exceptions;
using Planarian.Library.Options;
using Planarian.Modules.Authentication.Services;
using Planarian.Modules.Caves.Controllers;
using Planarian.Modules.Caves.Services;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.Revisions;
using Planarian.Model.Shared;
using Planarian.Modules.Caves.Models;
using Planarian.Modules.Caves.Repositories;
using Planarian.Modules.Caves.Revisions;
using Planarian.Tests;
using Planarian.Tests.Integration.Infrastructure.Actors;
using Planarian.Tests.Integration.Infrastructure.Services;
using Xunit;
using static Planarian.Tests.Integration.Caves.Revisions.CaveChangeRequestTestSupport;

namespace Planarian.Tests.Integration.Caves.Revisions;

public sealed class CaveStaleClientSafetyIntegrationTests(PostgresTestServer fixture)
    : IClassFixture<PostgresTestServer>
{
    private const string GeoJson = """{"type":"FeatureCollection","features":[]}""";

    [Fact]
    public async Task PutControllerRejectsOmittedRevisionAndMissingCaveIdAndMapsStaleRevisionTo409()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(PutControllerRejectsOmittedRevisionAndMissingCaveIdAndMapsStaleRevisionTo409));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var quality = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "qual00000a");
        await CavePermissions.GrantManagerAsync(database, cave, "manager");
        await using var actor = await CaveTestActor.CreateAsync(database, cave.AccountId, "manager");
        await using var verify = database.CreateDbContext("controller-verify", cave.AccountId);
        var repository = new CaveRepository(actor.Db, actor.Db.RequestUser);
        var controller = new CaveController(actor.Db.RequestUser, new TokenService(new AuthOptions()),
            actor.Services.Caves,
            new CaveRevisionService(repository, new CaveRevisionQueryRepository(actor.Db, actor.Db.RequestUser)));
        var before = await CaveDurableStateFingerprint.CaptureAsync(verify);

        var oldClientRequest = PublishableValues(cave, quality.Id, "Old browser editor");
        oldClientRequest.ExpectedRevisionId = null;
        var webJsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
        var obsoleteJson = JsonSerializer.Serialize(oldClientRequest, webJsonOptions);
        using var oldClientDocument = JsonDocument.Parse(obsoleteJson);
        Assert.DoesNotContain(oldClientDocument.RootElement.EnumerateObject(),
            property => string.Equals(property.Name, nameof(AddCaveVm.ExpectedRevisionId),
                StringComparison.OrdinalIgnoreCase));
        var omitted = JsonSerializer.Deserialize<AddCaveVm>(obsoleteJson, webJsonOptions)!;
        var missing = await Assert.ThrowsAsync<ApiException>(() => controller.UpdateCave(omitted, default));
        Assert.Equal(400, missing.StatusCode);
        await CaveDurableStateFingerprint.AssertUnchangedAsync(verify, before);

        // An explicitly null request body is malformed too, not a NullReferenceException.
        var nullBody = await Assert.ThrowsAsync<ApiException>(() => controller.UpdateCave(null!, default));
        Assert.Equal(400, nullBody.StatusCode);
        await CaveDurableStateFingerprint.AssertUnchangedAsync(verify, before);

        // A malformed PUT missing its Cave ID must not silently create another Cave.
        omitted.Id = null;
        var noId = await Assert.ThrowsAsync<ApiException>(() => controller.UpdateCave(omitted, default));
        Assert.Equal(400, noId.StatusCode);
        await CaveDurableStateFingerprint.AssertUnchangedAsync(verify, before);

        omitted.Id = "  ";
        var blankId = await Assert.ThrowsAsync<ApiException>(() => controller.UpdateCave(omitted, default));
        Assert.Equal(400, blankId.StatusCode);
        await CaveDurableStateFingerprint.AssertUnchangedAsync(verify, before);

        omitted.Id = cave.CaveId;
        omitted.ExpectedRevisionId = "wrongrev00";
        var stale = await controller.UpdateCave(omitted, default);
        var conflict = Assert.IsType<ConflictObjectResult>(stale.Result);
        Assert.Equal(409, conflict.StatusCode);
        var body = Assert.IsType<CaveProposalAuthoringConflictVm>(conflict.Value);
        Assert.Equal(CaveProposalAuthoringConflictKind.PublishedCaveChanged, body.ConflictKind);
        Assert.Equal(cave.RevisionId, body.ActualId);
        await CaveDurableStateFingerprint.AssertUnchangedAsync(verify, before);

        omitted.ExpectedRevisionId = cave.RevisionId;
        var successful = await controller.UpdateCave(omitted, default);
        Assert.Equal(cave.CaveId, Assert.IsType<JsonResult>(successful.Result).Value);
        Assert.Equal("Old browser editor", (await actor.Services.Caves.GetCave(cave.CaveId))!.Name);
    }

    [Fact]
    public async Task MissingAndStaleTokensCannotOverwriteACompletePublishedAggregate()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(MissingAndStaleTokensCannotOverwriteACompletePublishedAggregate));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var quality = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "qual00000a");
        var biology = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.Biology, "Bats", "biology00a");
        var file = await FileTestDataFactory.AddFileAsync(database, cave, associateWithCave: true,
            fileId: "clientfilA");
        await CavePermissions.GrantManagerAsync(database, cave, "manager");
        var blobs = new TestFileBlobStore();
        blobs.Seed("test", "seed-a", [1, 2, 3]);

        string publishedRevision;
        string plotId;
        await using (var actor = await CaveTestActor.CreateAsync(database, cave.AccountId, "manager", blobs))
        {
            var first = PublishableValues(cave, quality.Id, "Published multi-asset Cave");
            first.Files = [new() { Id = file.FileId, Name = "Original file", FileTypeTagId = file.FileTypeId }];
            first.BiologyTagIds = [biology.Id];
            first.LinePlots = [new() { Name = "Original plot", GeoJson = GeoJson }];
            await actor.Services.Caves.AddCave(first, default);
            var context = await actor.Services.Caves.GetEditAuthoringContextAsync(cave.CaveId, default);
            publishedRevision = context.Cave.CurrentRevisionId!;
            plotId = Assert.Single(context.LinePlots).Id!;
        }

        await using var verify = database.CreateDbContext("fingerprint", cave.AccountId);
        var original = await CaveDurableStateFingerprint.CaptureAsync(verify);
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

        foreach (var revision in new string?[] { null, "", "  \t", "no-such-id", cave.RevisionId })
        {
            await using var actor = await CaveTestActor.CreateAsync(database, cave.AccountId, "manager", blobs);
            var context = await actor.Services.Caves.GetEditAuthoringContextAsync(cave.CaveId, default);
            var values = ValuesFromCave(context.Cave);
            values.ExpectedRevisionId = revision;
            values.Name = "Old client overwrites everything";
            values.Narrative = "Stale narrative";
            values.DepthFeet = 123;
            values.BiologyTagIds = [];
            values.Entrances.Single().Name = "Stale entrance";
            values.Files!.Single().Name = "Stale file";
            values.LinePlots = [new() { Id = plotId, Name = "Stale plot", GeoJson = GeoJson }];

            // Emulate an old JSON request body: the ExpectedRevisionId member does not exist at all.
            var json = JsonSerializer.Serialize(values, options);
            if (revision is null)
            {
                using var oldClientDocument = JsonDocument.Parse(json);
                Assert.DoesNotContain(oldClientDocument.RootElement.EnumerateObject(),
                    property => string.Equals(property.Name, nameof(AddCaveVm.ExpectedRevisionId),
                        StringComparison.OrdinalIgnoreCase));
            }
            var deserialized = JsonSerializer.Deserialize<AddCaveVm>(json, options)!;
            if (string.IsNullOrWhiteSpace(revision))
            {
                var error = await Assert.ThrowsAsync<ApiException>(() =>
                    actor.Services.Caves.AddCave(deserialized, default));
                Assert.Equal(400, error.StatusCode);
                Assert.Contains("ExpectedRevisionId", error.Message, StringComparison.Ordinal);
            }
            else
            {
                var error = await Assert.ThrowsAsync<CaveRevisionConflictException>(() =>
                    actor.Services.Caves.AddCave(deserialized, default));
                Assert.Equal(revision, error.ExpectedRevisionId);
                Assert.Equal(publishedRevision, error.ActualRevisionId);
            }
            await CaveDurableStateFingerprint.AssertUnchangedAsync(verify, original);
            Assert.True(blobs.Contains("test", "seed-a"));
            Assert.Equal(0, blobs.DeleteIfExistsAttempts);
        }
    }

    [Fact]
    public async Task RejectedLegacyRequestCannotTriggerCleanupOfPreviouslyQueuedObjects()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(RejectedLegacyRequestCannotTriggerCleanupOfPreviouslyQueuedObjects));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var quality = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "qual00000a");
        await CavePermissions.GrantManagerAsync(database, cave, "manager");
        var blobs = new TestFileBlobStore();
        blobs.Seed("test", "previously-removed", [4, 5, 6]);
        await using (var seed = database.CreateDbContext("cleanup-seed", cave.AccountId))
        {
            seed.StorageCleanupTargets.Add(new StorageCleanupTarget
            {
                AccountId = cave.AccountId,
                Purpose = StorageCleanupPurpose.CaveAssetRemoval,
                StoragePartition = "test",
                StorageKey = "previously-removed"
            });
            await seed.SaveChangesAsync();
        }
        // The actor/user is fixture setup, not part of the attempted mutation.
        await using var actor = await CaveTestActor.CreateAsync(database, cave.AccountId, "manager", blobs);
        await using var verify = database.CreateDbContext("fingerprint", cave.AccountId);
        var original = await CaveDurableStateFingerprint.CaptureAsync(verify);
        var invalid = PublishableValues(cave, quality.Id, "Unauthorized stale request");
        invalid.ExpectedRevisionId = null;
        var error = await Assert.ThrowsAsync<ApiException>(() => actor.Services.Caves.AddCave(invalid, default));
        Assert.Equal(400, error.StatusCode);
        await CaveDurableStateFingerprint.AssertUnchangedAsync(verify, original);
        Assert.True(blobs.Contains("test", "previously-removed"));
        Assert.Equal(0, blobs.DeleteIfExistsAttempts);

        // Preserve the existing bounded-retry contract on the success path.
        var valid = PublishableValues(cave, quality.Id, "Authorized update");
        await actor.Services.Caves.AddCave(valid, default);
        Assert.False(blobs.Contains("test", "previously-removed"));
        Assert.False(await verify.StorageCleanupTargets.AnyAsync(row => row.AccountId == cave.AccountId));
        Assert.NotEqual(cave.RevisionId, await verify.Caves.IgnoreQueryFilters()
            .Where(row => row.Id == cave.CaveId).Select(row => row.CurrentRevisionId).SingleAsync());
    }

    [Fact]
    public async Task ForeignFilePlotTagAndCountyReferencesCannotContaminateEitherTenant()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(ForeignFilePlotTagAndCountyReferencesCannotContaminateEitherTenant));
        var own = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var other = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'b');
        var quality = await ReferenceTestData.AddTagAsync(database, own.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "qual00000a");
        var foreignBiology = await ReferenceTestData.AddTagAsync(database, other.AccountId,
            TagTypeKeyConstant.Biology, "Foreign bats", "biology00b");
        var foreignFile = await FileTestDataFactory.AddFileAsync(database, other,
            associateWithCave: true, fileId: "foreignfiB");
        var ownFileType = await ReferenceTestData.AddTagAsync(database, own.AccountId,
            TagTypeKeyConstant.File, "Document", "ownfile00a");
        const string foreignPlotId = "foreignplB";
        await using (var seed = database.CreateDbContext("foreign-plot-seed", other.AccountId))
        {
            seed.CaveGeoJsons.Add(new CaveGeoJson
            { Id = foreignPlotId, CaveId = other.CaveId, Name = "Foreign survey", GeoJson = GeoJson });
            await seed.SaveChangesAsync();
        }
        await CavePermissions.GrantManagerAsync(database, own, "manager");
        await using var manager = await CaveTestActor.CreateAsync(database, own.AccountId, "manager");
        await using var verify = database.CreateDbContext("fingerprint-foreign", own.AccountId);
        var initial = await CaveDurableStateFingerprint.CaptureAsync(verify);

        var attempts = new (string Name, Action<AddCaveVm> Change)[]
        {
            ("foreign File with otherwise valid metadata", values =>
                values.Files = [new() { Id = foreignFile.FileId, FileTypeTagId = ownFileType.Id }]),
            ("foreign line-plot stable ID", values =>
                values.LinePlots = [new() { Id = foreignPlotId, Name = "Hijacked plot", GeoJson = GeoJson }]),
            ("foreign Tag with otherwise valid fields", values =>
                values.BiologyTagIds = [foreignBiology.Id]),
            ("foreign County with otherwise valid fields", values =>
            {
                values.StateId = other.StateId;
                values.CountyId = other.CountyId;
            })
        };
        foreach (var (name, mutate) in attempts)
        {
            var values = PublishableValues(own, quality.Id, $"Unauthorized {name}");
            mutate(values);
            var failure = await Assert.ThrowsAsync<ApiException>(() => manager.Services.Caves.AddCave(values, default));
            Assert.True(failure.StatusCode is 400 or 403 or 404,
                $"Unexpected API status for {name}: {failure.StatusCode}");
            await CaveDurableStateFingerprint.AssertUnchangedAsync(verify, initial);
        }
    }

    [Fact]
    public async Task CurrentRevisionAndNewCaveWithoutTokenCanPublishButNewCaveWithTokenCannot()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(CurrentRevisionAndNewCaveWithoutTokenCanPublishButNewCaveWithTokenCannot));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var quality = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "qual00000a");
        await CavePermissions.GrantManagerAsync(database, cave, "manager");
        await using var actor = await CaveTestActor.CreateAsync(database, cave.AccountId, "manager");
        var current = PublishableValues(cave, quality.Id, "Current-client update");
        Assert.Equal(cave.CaveId, await actor.Services.Caves.AddCave(current, default));
        var updated = await actor.Services.Caves.GetCave(cave.CaveId);
        Assert.NotEqual(cave.RevisionId, updated!.CurrentRevisionId);

        var newCave = PublishableValues(cave, quality.Id, "New Cave");
        newCave.Id = null;
        newCave.ExpectedRevisionId = cave.RevisionId;
        newCave.IsCountyNumberManuallySet = false;
        newCave.CountyNumber = null;
        await using var verify = database.CreateDbContext("verify-new-cave", cave.AccountId);
        var beforeRejectedCreate = await CaveDurableStateFingerprint.CaptureAsync(verify);
        var rejected = await Assert.ThrowsAsync<ApiException>(() => actor.Services.Caves.AddCave(newCave, default));
        Assert.Equal(400, rejected.StatusCode);
        await CaveDurableStateFingerprint.AssertUnchangedAsync(verify, beforeRejectedCreate);
        newCave.ExpectedRevisionId = null;
        var newId = await actor.Services.Caves.AddCave(newCave, default);
        Assert.NotEqual(cave.CaveId, newId);
        Assert.NotNull(await verify.Caves.IgnoreQueryFilters()
            .Where(row => row.AccountId == cave.AccountId && row.Id == newId)
            .Select(row => row.CurrentRevisionId).SingleAsync());
    }

    [Fact]
    public async Task TenantCrossingAndRevokedManagerCannotChangePublishedCave()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(TenantCrossingAndRevokedManagerCannotChangePublishedCave));
        var own = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var foreign = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'b');
        var quality = await ReferenceTestData.AddTagAsync(database, own.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "qual00000a");
        await CavePermissions.GrantManagerAsync(database, own, "manager");
        await CavePermissions.GrantManagerAsync(database, foreign, "foreign-manager");
        // Creating a test actor can seed its user: do it before measuring the read/write contract.
        await using (var initializeManager = await CaveTestActor.CreateAsync(database, own.AccountId, "manager")) { }
        await using var verify = database.CreateDbContext("fingerprint", own.AccountId);
        var original = await CaveDurableStateFingerprint.CaptureAsync(verify);

        await using (var actor = await CaveTestActor.CreateAsync(database, own.AccountId, "manager"))
        {
            var otherTenantEdit = PublishableValues(foreign, quality.Id, "Foreign overwriting");
            var forbiddenEdit = await Assert.ThrowsAsync<ApiException>(() =>
                actor.Services.Caves.AddCave(otherTenantEdit, default));
            Assert.Equal(403, forbiddenEdit.StatusCode);
            var foreignEditContext = await Assert.ThrowsAsync<ApiException>(() =>
                actor.Services.Caves.GetEditAuthoringContextAsync(foreign.CaveId, default));
            Assert.Equal(404, foreignEditContext.StatusCode);
            var foreignProposalContext = await Assert.ThrowsAsync<ApiException>(() =>
                actor.ChangeRequests.GetAuthoringContextAsync(foreign.CaveId, default));
            Assert.Equal(404, foreignProposalContext.StatusCode);
        }
        await CaveDurableStateFingerprint.AssertUnchangedAsync(verify, original);

        await CavePermissions.RevokeManagerAsync(database, own, "manager");
        // The permission revocation is intentional: compare against the post-revocation state.
        var afterRevoke = await CaveDurableStateFingerprint.CaptureAsync(verify);
        await using (var actor = await CaveTestActor.CreateAsync(database, own.AccountId, "manager"))
        {
            var attempt = PublishableValues(own, quality.Id, "Revoked manager edit");
            var revokedEdit = await Assert.ThrowsAsync<ApiException>(() =>
                actor.Services.Caves.AddCave(attempt, default));
            Assert.Equal(403, revokedEdit.StatusCode);
            var revokedContext = await Assert.ThrowsAsync<ApiException>(() =>
                actor.Services.Caves.GetEditAuthoringContextAsync(own.CaveId, default));
            Assert.Equal(404, revokedContext.StatusCode);
        }
        await CaveDurableStateFingerprint.AssertUnchangedAsync(verify, afterRevoke);
    }
}
