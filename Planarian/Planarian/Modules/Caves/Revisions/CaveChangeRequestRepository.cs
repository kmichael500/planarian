using Microsoft.EntityFrameworkCore;
using Planarian.Library.Exceptions;
using Planarian.Model.Database;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.Revisions;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Helpers;
using Planarian.Modules.Caves.Models;
using Planarian.Modules.Query.Constants;
using Planarian.Modules.Files.Repositories;
using Planarian.Modules.Files.Models;
using Planarian.Modules.Query.Extensions;
using Planarian.Modules.Tags;
using Planarian.Modules.Tags.Repositories;
using Planarian.Shared.Services;
using Planarian.Shared.Base;

using Planarian.Modules.Files.Persistence;
namespace Planarian.Modules.Caves.Revisions;

public sealed class CaveChangeRequestReadRow
{
    public CaveChangeRequest Request { get; init; } = null!;
    public CaveProposalVersion ProposalVersion { get; init; } = null!;
    public CaveRevision? ProposalBaseRevision { get; init; }
    public CaveRevision? CurrentRevision { get; init; }
    public string? LiveCaveName { get; init; }
    public bool CaveExists { get; init; }
    public string? SubmitterName { get; init; }
    public string? ReviewerName { get; init; }
    public bool CurrentCanReview { get; init; }
}

public sealed record CaveProposalVersionReadRow(CaveProposalVersion Version, string? ActorName);
public sealed record CaveProposalVersionDetailReadRow(CaveProposalVersion Version, CaveRevision? BaseRevision,
    CaveProposalVersion? PreviousVersion, CaveRevision? PreviousBaseRevision);
public sealed record CaveChangeRequestReviewEventReadRow(CaveChangeRequestReviewEvent Event, string? ActorName);

public sealed class CaveChangeRequestRepository : AccountScopedRepositoryBase
{
    private static readonly string[] UserViewSatisfyingPermissionKeys =
        PermissionPolicy.GetSatisfyingUserPermissionKeys(PermissionPolicyKey.View);
    private static readonly string[] CaveViewSatisfyingPermissionKeys =
        PermissionPolicy.GetSatisfyingCavePermissionKeys(PermissionPolicyKey.View);
    private static readonly string[] UserManagerSatisfyingPermissionKeys =
        PermissionPolicy.GetSatisfyingUserPermissionKeys(PermissionPolicyKey.Manager);
    private static readonly string[] CaveManagerSatisfyingPermissionKeys =
        PermissionPolicy.GetSatisfyingCavePermissionKeys(PermissionPolicyKey.Manager);
    private readonly CaveRevisionDiffService _diff = new();
    private readonly AuthoringFileStore<PlanarianDbContext> _authoringFileStore;

    public CaveChangeRequestRepository(
        PlanarianDbContext db,
        RequestUser user,
        AuthoringFileStore authoringFileStore) : base(db, user)
    {
        _authoringFileStore = authoringFileStore;
    }

    internal CaveChangeRequestRepository(
        PlanarianDbContext db,
        RequestUser user,
        TimeProvider? timeProvider = null)
        : this(db, user, new AuthoringFileStore(db, user, timeProvider ?? TimeProvider.System))
    {
    }

    public Task<string> CreateAsync(string caveId, string baseRevisionId, CaveProposalSnapshotV1 proposal,
        CancellationToken cancellationToken) =>
        CreateAsync(caveId, baseRevisionId, (resolvedBaseRevisionId, _) =>
        {
            if (resolvedBaseRevisionId != baseRevisionId)
                throw new CaveRevisionConflictException(caveId, baseRevisionId, resolvedBaseRevisionId);
            return Task.FromResult(proposal);
        }, cancellationToken);

    public Task<string> CreateAsync(string caveId, string baseRevisionId,
        Func<CancellationToken, Task<CaveProposalSnapshotV1>> buildProposal,
        CancellationToken cancellationToken) =>
        CreateAsync(caveId, baseRevisionId, (resolvedBaseRevisionId, token) =>
        {
            if (resolvedBaseRevisionId != baseRevisionId)
                throw new CaveRevisionConflictException(caveId, baseRevisionId, resolvedBaseRevisionId);
            return buildProposal(token);
        }, cancellationToken);

    public async Task<string> CreateAsync(string caveId, string baseRevisionId,
        Func<string, CancellationToken, Task<CaveProposalSnapshotV1>> buildProposal,
        CancellationToken cancellationToken)
    {
        await using var transaction = await BeginTransactionAsync(cancellationToken);
        var cave = await DbContext.Caves.FromSqlInterpolated(
                $"SELECT *, xmin FROM \"Caves\" WHERE \"AccountId\" = {Scope.AccountId} AND \"Id\" = {caveId} FOR KEY SHARE")
            .SingleOrDefaultAsync(cancellationToken);
        if (cave is null) throw ApiExceptionDictionary.NotFound("Cave");
        if (cave.CurrentRevisionId is null)
            throw ApiExceptionDictionary.BadRequest("The Cave has no published revision.");

        if (cave.CurrentRevisionId != baseRevisionId)
        {
            var revisions = await DbContext.CaveRevisions.AsNoTracking()
                .Where(row => row.AccountId == Scope.AccountId && row.CaveId == caveId &&
                              (row.Id == baseRevisionId || row.Id == cave.CurrentRevisionId))
                .ToListAsync(cancellationToken);
            var expected = revisions.SingleOrDefault(row => row.Id == baseRevisionId);
            if (expected is null)
                throw new CaveRevisionConflictException(caveId, baseRevisionId, cave.CurrentRevisionId);
            var current = revisions.SingleOrDefault(row => row.Id == cave.CurrentRevisionId)
                          ?? throw new InvalidOperationException(
                              $"Current revision '{cave.CurrentRevisionId}' for Cave '{caveId}' was not found.");
            var expectedSnapshot = CaveSnapshotJson.Deserialize(expected.SnapshotJson, expected.SnapshotSchemaVersion);
            var currentSnapshot = CaveSnapshotJson.Deserialize(current.SnapshotJson, current.SnapshotSchemaVersion);
            if (!_diff.IsProposalCompatible(expectedSnapshot, currentSnapshot))
                throw new CaveRevisionConflictException(caveId, baseRevisionId, cave.CurrentRevisionId);
            baseRevisionId = cave.CurrentRevisionId;
        }

        var proposal = await buildProposal(baseRevisionId, cancellationToken);

        var baseRevision = await DbContext.CaveRevisions.SingleAsync(row => row.AccountId == Scope.AccountId &&
            row.CaveId == caveId && row.Id == baseRevisionId, cancellationToken);
        var baseSnapshot = CaveSnapshotJson.Deserialize(baseRevision.SnapshotJson, baseRevision.SnapshotSchemaVersion);
        var request = new CaveChangeRequest
        {
            AccountId = Scope.AccountId,
            CaveId = caveId,
            BaseRevisionId = baseRevisionId,
            BaseStateId = baseSnapshot.State.Id,
            BaseCountyId = baseSnapshot.County.Id,
            ProposedStateId = proposal.StateId,
            ProposedCountyId = proposal.CountyId,
            Status = CaveChangeRequestStatus.Pending
        };
        DbContext.CaveChangeRequests.Add(request);
        await SaveChangesAsync(cancellationToken);
        await ClaimAuthoringStagedFilesAsync(request, proposal.Files
            .Where(file => file.Disposition == ProposalFileDisposition.PublishStaged)
            .Select(file => file.FileId), cancellationToken);
        var version = NewVersion(request, baseRevisionId, null, proposal);
        DbContext.CaveProposalVersions.Add(version);
        await SaveChangesAsync(cancellationToken);
        request.CurrentProposalVersionId = version.Id;
        await SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return request.Id;
    }

    public async Task<string> CreateNewAsync(string caveId,
        Func<CancellationToken, Task<CaveProposalSnapshotV1>> buildProposal,
        CancellationToken cancellationToken)
    {
        await using var transaction = await BeginTransactionAsync(cancellationToken);
        if (await DbContext.Caves.IgnoreQueryFilters().AnyAsync(cave =>
                cave.AccountId == Scope.AccountId && cave.Id == caveId, cancellationToken))
            throw new InvalidOperationException("The reserved Cave ID is already in use.");

        var proposal = await buildProposal(cancellationToken);
        if (proposal.CaveId != caveId || proposal.AccountId != Scope.AccountId)
            throw new InvalidOperationException("The new-Cave proposal identity does not match the request.");

        var request = new CaveChangeRequest
        {
            AccountId = Scope.AccountId,
            CaveId = caveId,
            BaseRevisionId = null,
            BaseStateId = null,
            BaseCountyId = null,
            ProposedStateId = proposal.StateId,
            ProposedCountyId = proposal.CountyId,
            Status = CaveChangeRequestStatus.Pending
        };
        DbContext.CaveChangeRequests.Add(request);
        await SaveChangesAsync(cancellationToken);
        await ClaimAuthoringStagedFilesAsync(request, proposal.Files
            .Where(file => file.Disposition == ProposalFileDisposition.PublishStaged)
            .Select(file => file.FileId), cancellationToken);
        var version = NewVersion(request, null, null, proposal);
        DbContext.CaveProposalVersions.Add(version);
        await SaveChangesAsync(cancellationToken);
        request.CurrentProposalVersionId = version.Id;
        await SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return request.Id;
    }

    public Task<bool> CaveExistsAsync(string caveId, CancellationToken cancellationToken) =>
        DbContext.Caves.IgnoreQueryFilters().AnyAsync(cave =>
            cave.AccountId == Scope.AccountId && cave.Id == caveId, cancellationToken);

    public Task<string?> GetCurrentRevisionIdAsync(string caveId, CancellationToken cancellationToken) =>
        DbContext.Caves.Where(cave => cave.AccountId == Scope.AccountId && cave.Id == caveId)
            .Select(cave => cave.CurrentRevisionId).SingleOrDefaultAsync(cancellationToken);

    public async Task<string> AddVersionAsync(string requestId, string? baseRevisionId, string expectedProposalVersionId,
        CaveProposalSnapshotV1 proposal, bool reviewer, bool againstCurrent, CancellationToken cancellationToken)
    {
        await using var transaction = await BeginTransactionAsync(cancellationToken);
        var request = await DbContext.CaveChangeRequests.FromSqlInterpolated(
                $"SELECT *, xmin FROM \"CaveChangeRequests\" WHERE \"AccountId\" = {Scope.AccountId} AND \"Id\" = {requestId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken) ?? throw ApiExceptionDictionary.NotFound("Change request");
        if (!CaveChangeRequestStatePolicy.CanReceiveProposalVersions(request.Status))
            throw ApiExceptionDictionary.BadRequest("This request can no longer be revised.");
        var access = await GetOpenRequestAccessAsync(request, cancellationToken);
        if (!access.Visible) throw ApiExceptionDictionary.NotFound("Change request");
        reviewer = access.Reviewer;
        if (CaveChangeRequestStatePolicy.IsAwaitingAuthor(request.Status) && request.CreatedByUserId != UserId)
            throw ApiExceptionDictionary.Forbidden("Only the submitter can revise a request while changes are requested.");
        if (CaveChangeRequestStatePolicy.IsAwaitingReviewer(request.Status) && !reviewer && request.CreatedByUserId != UserId)
            throw ApiExceptionDictionary.Forbidden("You can only revise your own request.");
        if (request.CurrentProposalVersionId != expectedProposalVersionId)
            throw new CaveProposalVersionConflictException(expectedProposalVersionId,
                request.CurrentProposalVersionId);

        var currentProposal = await DbContext.CaveProposalVersions.SingleAsync(version =>
            version.AccountId == Scope.AccountId && version.CaveId == request.CaveId &&
            version.ChangeRequestId == requestId && version.Id == expectedProposalVersionId, cancellationToken);
        if (request.BaseRevisionId is null)
        {
            if (againstCurrent || baseRevisionId is not null || currentProposal.BaseRevisionId is not null)
                throw ApiExceptionDictionary.BadRequest("A new-Cave proposal has no published base revision.");
        }
        else
        {
            if (!againstCurrent && currentProposal.BaseRevisionId != baseRevisionId)
                throw new CaveRevisionConflictException(request.CaveId, baseRevisionId,
                    currentProposal.BaseRevisionId);

            var currentRevisionId = await DbContext.Caves.Where(cave => cave.AccountId == Scope.AccountId &&
                    cave.Id == request.CaveId).Select(cave => cave.CurrentRevisionId).SingleAsync(cancellationToken);
            if (currentRevisionId != baseRevisionId)
            {
                if (baseRevisionId is null || currentRevisionId is null)
                    throw new CaveRevisionConflictException(request.CaveId, baseRevisionId, currentRevisionId);

                var revisions = await DbContext.CaveRevisions.AsNoTracking()
                    .Where(row => row.AccountId == Scope.AccountId && row.CaveId == request.CaveId &&
                                  (row.Id == baseRevisionId || row.Id == currentRevisionId))
                    .ToListAsync(cancellationToken);
                var expected = revisions.SingleOrDefault(row => row.Id == baseRevisionId);
                var current = revisions.SingleOrDefault(row => row.Id == currentRevisionId);
                if (expected is null || current is null ||
                    !_diff.IsProposalCompatible(
                        CaveSnapshotJson.Deserialize(expected.SnapshotJson, expected.SnapshotSchemaVersion),
                        CaveSnapshotJson.Deserialize(current.SnapshotJson, current.SnapshotSchemaVersion)))
                    throw new CaveRevisionConflictException(request.CaveId, baseRevisionId, currentRevisionId);
            }
        }

        await ClaimAuthoringStagedFilesAsync(request, proposal.Files
            .Where(file => file.Disposition == ProposalFileDisposition.PublishStaged)
            .Select(file => file.FileId), cancellationToken);
        var version = NewVersion(request, baseRevisionId, expectedProposalVersionId, proposal);
        DbContext.CaveProposalVersions.Add(version);
        await SaveChangesAsync(cancellationToken);
        request.CurrentProposalVersionId = version.Id;
        request.ProposedStateId = proposal.StateId;
        request.ProposedCountyId = proposal.CountyId;
        await SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return version.Id;
    }

    internal async Task StageFileAsync(string requestId, string fileId, string fileTypeTagId, string? name,
        bool reviewer, CancellationToken cancellationToken)
    {
        await using var transaction = await BeginTransactionAsync(cancellationToken);
        var request = await LockedAsync(requestId, cancellationToken);
        if (!CaveChangeRequestStatePolicy.CanReceiveProposalVersions(request.Status))
            throw ApiExceptionDictionary.BadRequest("This request can no longer receive files.");
        var access = await GetOpenRequestAccessAsync(request, cancellationToken);
        if (!access.Visible) throw ApiExceptionDictionary.NotFound("Change request");
        reviewer = access.Reviewer;
        if (CaveChangeRequestStatePolicy.IsAwaitingAuthor(request.Status) && request.CreatedByUserId != UserId)
            throw ApiExceptionDictionary.Forbidden("Only the submitter can add files while changes are requested.");
        if (CaveChangeRequestStatePolicy.IsAwaitingReviewer(request.Status) && !reviewer && request.CreatedByUserId != UserId)
            throw ApiExceptionDictionary.Forbidden("You can only add files to your own request.");

        var fileSnapshot = await (from file in DbContext.Files.AsNoTracking()
                from tag in DbContext.TagTypes.AsNoTracking()
                where file.AccountId == Scope.AccountId && file.Id == fileId && file.CaveId == null &&
                      tag.Id == fileTypeTagId
                select new { file.Name, file.Extension, FileTypeName = tag.Name })
            .SingleOrDefaultAsync(cancellationToken);
        if (fileSnapshot is null) throw ApiExceptionDictionary.NotFound("File");

        var current = await DbContext.CaveProposalVersions.SingleAsync(version =>
            version.AccountId == Scope.AccountId && version.ChangeRequestId == requestId &&
            version.Id == request.CurrentProposalVersionId, cancellationToken);
        var proposal = CaveProposalJson.Deserialize(current.ProposalJson, current.SchemaVersion);
        var files = proposal.Files.Where(file => file.FileId != fileId).Append(
            new ProposalFileIntent(fileId, ProposalFileDisposition.PublishStaged, fileTypeTagId,
                string.IsNullOrWhiteSpace(name) ? fileSnapshot.Name : name, fileSnapshot.Extension,
                fileSnapshot.FileTypeName)).ToList();
        var nextProposal = proposal with { Files = files };
        var nextVersion = NewVersion(request, current.BaseRevisionId, request.CurrentProposalVersionId, nextProposal);
        DbContext.CaveChangeRequestStagedFiles.Add(new CaveChangeRequestStagedFile
        {
            AccountId = Scope.AccountId,
            ChangeRequestId = requestId,
            FileId = fileId
        });
        DbContext.CaveProposalVersions.Add(nextVersion);
        await SaveChangesAsync(cancellationToken);
        request.CurrentProposalVersionId = nextVersion.Id;
        await SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, CaveFileSnapshotV1>> GetFileSnapshotsAsync(
        IEnumerable<string> fileIds, CancellationToken cancellationToken)
    {
        var ids = fileIds.Distinct(StringComparer.Ordinal).ToList();
        return await (from file in DbContext.Files.AsNoTracking()
                join tag in DbContext.TagTypes.AsNoTracking() on file.FileTypeTagId equals tag.Id
                where file.AccountId == Scope.AccountId && ids.Contains(file.Id)
                select new CaveFileSnapshotV1
                {
                    Id = file.Id,
                    FileTypeTagId = file.FileTypeTagId,
                    FileTypeNameAtRevision = tag.Name,
                    Name = file.Name,
                    Extension = file.Extension
                })
            .ToDictionaryAsync(file => file.Id, cancellationToken);
    }

    public Task<bool> IsStagedFileAsync(string requestId, string fileId, CancellationToken cancellationToken) =>
        DbContext.CaveChangeRequestStagedFiles.AsNoTracking().AnyAsync(staged =>
            staged.AccountId == Scope.AccountId && staged.ChangeRequestId == requestId && staged.FileId == fileId,
            cancellationToken);

    public async Task<IReadOnlySet<string>> GetStagedFileIdsAsync(string requestId,
        CancellationToken cancellationToken) =>
        (await DbContext.CaveChangeRequestStagedFiles.AsNoTracking()
            .Where(staged => staged.AccountId == Scope.AccountId && staged.ChangeRequestId == requestId)
            .Select(staged => staged.FileId).ToListAsync(cancellationToken)).ToHashSet(StringComparer.Ordinal);

    public Task<IReadOnlySet<string>> GetAuthorableStagedFileIdsAsync(
        IEnumerable<string> fileIds, string? requestId, CancellationToken cancellationToken) =>
        _authoringFileStore.GetAuthorableIdsAsync(fileIds, requestId, cancellationToken);

    public Task<IReadOnlyList<StagedCaveFilePublication>> PlanStagedFilePublicationsAsync(
        string requestId, IReadOnlyDictionary<string, string> extensionsByFileId,
        CancellationToken cancellationToken) =>
        _authoringFileStore.PlanRequestPublicationsAsync(
            requestId, extensionsByFileId, cancellationToken);

    public Task<PagedResult<CaveChangeRequestReadRow>> ListMineAsync(int pageNumber, int pageSize,
        CancellationToken cancellationToken, CaveChangeRequestStatus? status = null,
        CaveChangeRequestSortBy sortBy = CaveChangeRequestSortBy.UpdatedOn, bool sortDescending = true) =>
        PageAsync(Order(Query(createdByUserId: UserId, status: status), sortBy, sortDescending, openFirst: true),
            pageNumber, pageSize, cancellationToken);

    public Task<PagedResult<CaveChangeRequestReadRow>> ListForReviewAsync(int pageNumber, int pageSize,
        CancellationToken cancellationToken, CaveChangeRequestStatus? status = null,
        CaveChangeRequestSortBy sortBy = CaveChangeRequestSortBy.SubmittedOn,
        bool sortDescending = false) => PageAsync(Order(Query(status: status)
            .Where(row => row.CurrentCanReview &&
                          row.Request.Status != CaveChangeRequestStatePolicy.AwaitingAuthorStatus),
            sortBy, sortDescending, openFirst: true),
            pageNumber, pageSize, cancellationToken);

    public Task<CaveChangeRequestReadRow?> GetAsync(string requestId, CancellationToken cancellationToken) =>
        Query(requestId: requestId).SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<CaveProposalVersionReadRow>> ListVersionsAsync(string requestId,
        CancellationToken cancellationToken)
    {
        var versions = await LoadValidatedVersionChainAsync(requestId, cancellationToken);
        if (versions is null) return [];
        var actorIds = versions.Select(version => version.CreatedByUserId)
            .Where(id => id is not null).Select(id => id!).Distinct().ToList();
        var actorNames = await DbContext.Users.AsNoTracking().Where(actor => actorIds.Contains(actor.Id))
            .ToDictionaryAsync(actor => actor.Id, actor => actor.FirstName + " " + actor.LastName,
                cancellationToken);
        return versions.Select(version => new CaveProposalVersionReadRow(version,
            version.CreatedByUserId is null ? null : actorNames.GetValueOrDefault(version.CreatedByUserId))).ToList();
    }

    public async Task<CaveProposalVersionDetailReadRow?> GetVersionAsync(string requestId, string versionId,
        CancellationToken cancellationToken)
    {
        var versions = await LoadValidatedVersionChainAsync(requestId, cancellationToken);
        if (versions is null) return null;
        var versionIndex = versions.ToList().FindIndex(version => version.Id == versionId);
        if (versionIndex < 0) return null;
        var currentVersion = versions[versionIndex];
        var previousVersion = versionIndex == 0 ? null : versions[versionIndex - 1];
        if (currentVersion.PreviousProposalVersionId != previousVersion?.Id)
            throw new InvalidOperationException("The proposal version history has an invalid predecessor.");

        var baseRevisionIds = new[] { currentVersion.BaseRevisionId, previousVersion?.BaseRevisionId }
            .OfType<string>().Distinct(StringComparer.Ordinal).ToList();
        var baseRevisions = await DbContext.CaveRevisions.AsNoTracking().Where(revision =>
                revision.AccountId == Scope.AccountId && revision.CaveId == currentVersion.CaveId &&
                baseRevisionIds.Contains(revision.Id))
            .ToDictionaryAsync(revision => revision.Id, cancellationToken);
        CaveRevision? currentBaseRevision = null;
        if (currentVersion.BaseRevisionId is not null &&
            !baseRevisions.TryGetValue(currentVersion.BaseRevisionId, out currentBaseRevision))
            throw new InvalidOperationException("The proposal version has a missing base revision.");
        CaveRevision? previousBaseRevision = null;
        if (previousVersion?.BaseRevisionId is not null &&
            !baseRevisions.TryGetValue(previousVersion.BaseRevisionId, out previousBaseRevision))
            throw new InvalidOperationException("The previous proposal version has a missing base revision.");

        return new CaveProposalVersionDetailReadRow(currentVersion, currentBaseRevision,
            previousVersion, previousBaseRevision);
    }

    public async Task<IReadOnlyList<TagNameCandidate>> GetTagCandidatesByIdsAsync(IEnumerable<string> ids,
        CancellationToken cancellationToken)
    {
        var distinct = ids.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToList();
        return await DbContext.TagTypes.AsNoTracking()
            .Where(tag => distinct.Contains(tag.Id))
            .Select(tag => new TagNameCandidate(tag.Id, tag.Name, tag.Key, tag.AccountId, tag.IsDefault))
            .ToListAsync(cancellationToken);
    }

    public Task<IReadOnlyList<TagNameCandidate>> GetEligiblePeopleCandidatesAsync(
        CancellationToken cancellationToken) =>
        EligiblePeopleTagLookup.GetEligibleAsync(DbContext, Scope.AccountId, cancellationToken);

    public async Task<(string StateName, string? StateAbbreviation, string CountyName, string CountyDisplayId)>
        GetLocationLabelsAsync(string stateId, string countyId, CancellationToken cancellationToken)
    {
        var pair = await (from county in DbContext.Counties.AsNoTracking()
                join state in DbContext.States.AsNoTracking() on county.StateId equals state.Id
                where county.AccountId == Scope.AccountId && county.Id == countyId &&
                      county.StateId == stateId && state.Id == stateId
                select new { StateName = state.Name, state.Abbreviation, CountyName = county.Name, county.DisplayId })
            .SingleOrDefaultAsync(cancellationToken);
        if (pair is null) throw ApiExceptionDictionary.BadRequest("The selected County does not belong to the selected State.");
        return (pair.StateName, pair.Abbreviation, pair.CountyName, pair.DisplayId);
    }

    public async Task<IReadOnlyList<CaveChangeRequestReviewEventReadRow>> ListReviewEventsAsync(string requestId,
        CancellationToken cancellationToken) =>
        await (from reviewEvent in DbContext.CaveChangeRequestReviewEvents.AsNoTracking()
               join actorValue in DbContext.Users.AsNoTracking() on reviewEvent.ActorUserId equals actorValue.Id into actors
               from actor in actors.DefaultIfEmpty()
               where reviewEvent.AccountId == Scope.AccountId && reviewEvent.ChangeRequestId == requestId
               orderby reviewEvent.CreatedOn, reviewEvent.Id
               select new CaveChangeRequestReviewEventReadRow(reviewEvent,
                   actor == null ? null : actor.FirstName + " " + actor.LastName)).ToListAsync(cancellationToken);

    public async Task RequestChangesAsync(string requestId, string expectedProposalVersionId, string message,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(message))
            throw ApiExceptionDictionary.BadRequest("A message is required when requesting changes.");
        await using var transaction = await BeginTransactionAsync(cancellationToken);
        var request = await LockedAsync(requestId, cancellationToken);
        RequireAwaitingReviewer(request.Status);
        RequireExpectedProposalVersion(request, expectedProposalVersionId);
        request.Status = CaveChangeRequestStatePolicy.AwaitingAuthorStatus;
        AddReviewEvent(request, expectedProposalVersionId, CaveChangeRequestReviewAction.ChangesRequested, message.Trim());
        await SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task ResubmitAsync(string requestId, string expectedProposalVersionId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await BeginTransactionAsync(cancellationToken);
        var request = await LockedAsync(requestId, cancellationToken);
        if (!CaveChangeRequestStatePolicy.IsAwaitingAuthor(request.Status))
            throw ApiExceptionDictionary.BadRequest("This request is not awaiting changes from the submitter.");
        if (request.CreatedByUserId != UserId)
            throw ApiExceptionDictionary.Forbidden("Only the submitter can resubmit requested changes.");
        RequireExpectedProposalVersion(request, expectedProposalVersionId);
        var requestedVersionId = await DbContext.CaveChangeRequestReviewEvents.AsNoTracking()
            .Where(reviewEvent => reviewEvent.AccountId == Scope.AccountId &&
                                  reviewEvent.ChangeRequestId == requestId &&
                                  reviewEvent.Action == CaveChangeRequestReviewAction.ChangesRequested)
            .OrderByDescending(reviewEvent => reviewEvent.CreatedOn).ThenByDescending(reviewEvent => reviewEvent.Id)
            .Select(reviewEvent => reviewEvent.ProposalVersionId).FirstOrDefaultAsync(cancellationToken);
        if (requestedVersionId is null)
            throw new InvalidOperationException("A changes-requested request is missing its review event.");
        if (requestedVersionId == expectedProposalVersionId)
            throw ApiExceptionDictionary.BadRequest("Save a newer proposal version before resubmitting for review.");
        request.Status = CaveChangeRequestStatePolicy.AwaitingReviewerStatus;
        AddReviewEvent(request, expectedProposalVersionId, CaveChangeRequestReviewAction.Resubmitted, null);
        await SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> RejectAsync(string requestId,
        string expectedProposalVersionId, string? notes,
        CancellationToken cancellationToken)
    {
        await using var transaction = await BeginTransactionAsync(cancellationToken);
        var request = await LockedAsync(requestId, cancellationToken);
        RequireAwaitingReviewer(request.Status);
        RequireExpectedProposalVersion(request, expectedProposalVersionId);
        if (string.IsNullOrWhiteSpace(notes))
            throw ApiExceptionDictionary.BadRequest("A reason is required to reject requested changes.");
        request.Status = CaveChangeRequestStatus.Rejected;
        request.ReviewerUserId = UserId;
        request.ReviewerNotes = notes.Trim();
        request.ReviewedOn = DateTime.UtcNow;
        AddReviewEvent(request, expectedProposalVersionId, CaveChangeRequestReviewAction.Rejected, request.ReviewerNotes);
        var cleanupTargets = await RemoveRemainingStagedFilesAsync(requestId, cancellationToken);
        await SaveChangesAsync(cancellationToken);
        var cleanupTargetIds = cleanupTargets.Select(target => target.Id)
            .Distinct(StringComparer.Ordinal).ToList();
        await transaction.CommitAsync(cancellationToken);
        return cleanupTargetIds;
    }

    public async Task GrantSubmitterCaveViewAsync(string userId, string caveId,
        CancellationToken cancellationToken)
    {
        if (DbContext.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Submitter View grants require an active PostgreSQL transaction.");

        // Lock current membership so approval and account removal cannot race into a stale
        // CavePermission. If removal wins first there is no membership and no grant; if
        // approval wins first, removal waits and then deletes the newly-created grant.
        var accountMember = await DbContext.AccountUsers.FromSqlInterpolated($"""
                SELECT * FROM "AccountUsers"
                WHERE "AccountId" = {Scope.AccountId} AND "UserId" = {userId}
                FOR KEY SHARE
                """)
            .IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        if (accountMember is null) return;

        var permissionId = await DbContext.Permissions.AsNoTracking()
            .Where(permission => permission.Key == PermissionPolicyKey.View &&
                                 permission.PermissionType == PermissionType.Cave)
            .Select(permission => permission.Id)
            .SingleAsync(cancellationToken);
        var alreadyGranted = await DbContext.CavePermissions.IgnoreQueryFilters().AnyAsync(permission =>
            permission.AccountId == Scope.AccountId && permission.UserId == userId &&
            permission.CaveId == caveId && permission.PermissionId == permissionId,
            cancellationToken);
        if (alreadyGranted) return;

        DbContext.CavePermissions.Add(new CavePermission
        {
            AccountId = Scope.AccountId,
            UserId = userId,
            PermissionId = permissionId,
            CaveId = caveId
        });
        await SaveChangesAsync(cancellationToken);
    }

    public async Task RequireCreationApprovalReadyAsync(string requestId,
        string expectedProposalVersionId, CancellationToken cancellationToken)
    {
        var request = await LockedAsync(requestId, cancellationToken);
        if (request.BaseRevisionId is not null)
            throw new InvalidOperationException("Creation approval lock was requested for an existing-Cave proposal.");
        RequireAwaitingReviewer(request.Status);
        RequireExpectedProposalVersion(request, expectedProposalVersionId);
    }

    public async Task<IReadOnlyList<string>> MarkApprovedAsync(string requestId,
        string expectedProposalVersionId,
        CaveMutationResult mutation, string? notes, CancellationToken cancellationToken)
    {
        if (!mutation.CreatedRevision || mutation.RevisionId is null)
            throw ApiExceptionDictionary.BadRequest("The proposal does not contain a publishable change.");
        var request = await LockedAsync(requestId, cancellationToken);
        RequireAwaitingReviewer(request.Status);
        RequireExpectedProposalVersion(request, expectedProposalVersionId);
        request.Status = CaveChangeRequestStatus.Approved;
        request.ApprovedRevisionId = mutation.RevisionId;
        request.ReviewerUserId = UserId;
        request.ReviewerNotes = notes?.Trim();
        request.ReviewedOn = DateTime.UtcNow;
        AddReviewEvent(request, expectedProposalVersionId, CaveChangeRequestReviewAction.Approved, request.ReviewerNotes);
        var cleanupTargets = await RemoveRemainingStagedFilesAsync(requestId, cancellationToken);
        await SaveChangesAsync(cancellationToken);
        return cleanupTargets.Select(target => target.Id).Distinct(StringComparer.Ordinal).ToList();
    }

    private Task<IReadOnlyList<StorageCleanupTarget>> RemoveRemainingStagedFilesAsync(
        string requestId, CancellationToken cancellationToken) =>
        _authoringFileStore.RemoveRequestClaimsAndUnreferencedAsync(requestId, cancellationToken);

    private Task ClaimAuthoringStagedFilesAsync(
        CaveChangeRequest request, IEnumerable<string> fileIds,
        CancellationToken cancellationToken) =>
        _authoringFileStore.ClaimAsync(request.Id, fileIds, cancellationToken);

    private static void RequireAwaitingReviewer(CaveChangeRequestStatus status)
    {
        if (CaveChangeRequestStatePolicy.IsAwaitingReviewer(status)) return;
        if (!CaveChangeRequestStatePolicy.IsOpen(status))
            throw ApiExceptionDictionary.BadRequest("This request has already been reviewed.");
        throw ApiExceptionDictionary.BadRequest("This request is not awaiting review.");
    }

    private static void RequireExpectedProposalVersion(CaveChangeRequest request, string expectedProposalVersionId)
    {
        if (request.CurrentProposalVersionId != expectedProposalVersionId)
            throw new CaveProposalVersionConflictException(expectedProposalVersionId, request.CurrentProposalVersionId);
    }

    private void AddReviewEvent(CaveChangeRequest request, string proposalVersionId,
        CaveChangeRequestReviewAction action, string? message)
    {
        DbContext.CaveChangeRequestReviewEvents.Add(new CaveChangeRequestReviewEvent
        {
            AccountId = request.AccountId,
            ChangeRequestId = request.Id,
            ProposalVersionId = proposalVersionId,
            Action = action,
            ActorUserId = UserId,
            Message = string.IsNullOrWhiteSpace(message) ? null : message.Trim()
        });
    }

    private async Task<(bool Visible, bool Reviewer)> GetOpenRequestAccessAsync(
        CaveChangeRequest request, CancellationToken cancellationToken)
    {
        if (request.BaseRevisionId is null)
        {
            var reviewer = await CanManageProposedLocationAsync(request, cancellationToken);
            var submitterVisible = request.CreatedByUserId == UserId &&
                                   await HasCurrentViewCapabilityAsync(cancellationToken);
            return (submitterVisible || reviewer, reviewer);
        }

        var visible = await DbContext.UserCavePermissionView.AnyAsync(permission =>
            permission.AccountId == Scope.AccountId && permission.UserId == UserId &&
            permission.CaveId == request.CaveId, cancellationToken);
        var existingReviewer = visible && await DbContext.UserCavePermissionView.AnyAsync(permission =>
            permission.AccountId == Scope.AccountId && permission.UserId == UserId &&
            permission.CaveId == request.CaveId &&
            CaveManagerSatisfyingPermissionKeys.Contains(permission.PermissionKey),
            cancellationToken);
        return (visible, existingReviewer);
    }

    private async Task<bool> HasCurrentViewCapabilityAsync(CancellationToken cancellationToken)
    {
        if (await DbContext.UserPermissions.AnyAsync(permission =>
                permission.UserId == UserId && permission.AccountId == Scope.AccountId &&
                permission.Permission!.PermissionType == PermissionType.User &&
                UserViewSatisfyingPermissionKeys.Contains(permission.Permission.Key), cancellationToken))
            return true;

        return await DbContext.CavePermissions.AnyAsync(permission =>
            permission.UserId == UserId && permission.AccountId == Scope.AccountId &&
            permission.Permission!.PermissionType == PermissionType.Cave &&
            CaveViewSatisfyingPermissionKeys.Contains(permission.Permission.Key), cancellationToken);
    }

    private async Task<bool> CanManageProposedLocationAsync(CaveChangeRequest request,
        CancellationToken cancellationToken)
    {
        if (await DbContext.UserPermissions.AnyAsync(permission =>
                permission.UserId == UserId && permission.AccountId == Scope.AccountId &&
                permission.Permission!.PermissionType == PermissionType.User &&
                UserManagerSatisfyingPermissionKeys.Contains(permission.Permission.Key), cancellationToken))
            return true;

        return await DbContext.CavePermissions.AnyAsync(permission =>
            permission.UserId == UserId && permission.AccountId == Scope.AccountId &&
            permission.Permission!.PermissionType == PermissionType.Cave &&
            CaveManagerSatisfyingPermissionKeys.Contains(permission.Permission.Key) &&
            permission.CaveId == null &&
            ((permission.StateId == null && permission.CountyId == null) ||
             permission.CountyId == request.ProposedCountyId ||
             (permission.CountyId == null && permission.StateId == request.ProposedStateId)),
            cancellationToken);
    }

    private async Task<CaveChangeRequest> LockedAsync(string requestId, CancellationToken cancellationToken) =>
        await DbContext.CaveChangeRequests.FromSqlInterpolated(
                $"SELECT *, xmin FROM \"CaveChangeRequests\" WHERE \"AccountId\" = {Scope.AccountId} AND \"Id\" = {requestId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken) ?? throw ApiExceptionDictionary.NotFound("Change request");

    private IQueryable<CaveChangeRequestReadRow> Query(string? requestId = null, string? createdByUserId = null,
        CaveChangeRequestStatus? status = null) =>
        from request in DbContext.CaveChangeRequests.AsNoTracking()
        join proposal in DbContext.CaveProposalVersions.AsNoTracking()
            on new { request.AccountId, ChangeRequestId = request.Id, Id = request.CurrentProposalVersionId }
            equals new { proposal.AccountId, proposal.ChangeRequestId, Id = (string?)proposal.Id }
        join caveValue in DbContext.Caves.IgnoreQueryFilters().AsNoTracking()
                .Where(cave => cave.AccountId == Scope.AccountId)
            on new { request.AccountId, Id = request.CaveId }
            equals new { caveValue.AccountId, caveValue.Id } into caves
        from cave in caves.DefaultIfEmpty()
        join proposalBaseRevisionValue in DbContext.CaveRevisions.AsNoTracking()
            on new { proposal.AccountId, proposal.CaveId, Id = proposal.BaseRevisionId }
            equals new { proposalBaseRevisionValue.AccountId, proposalBaseRevisionValue.CaveId,
                Id = (string?)proposalBaseRevisionValue.Id } into proposalBaseRevisions
        from proposalBaseRevision in proposalBaseRevisions.DefaultIfEmpty()
        join currentRevisionValue in DbContext.CaveRevisions.AsNoTracking()
            on new { request.AccountId, Id = cave == null ? null : cave.CurrentRevisionId }
            equals new { currentRevisionValue.AccountId, Id = (string?)currentRevisionValue.Id } into currentRevisions
        from currentRevision in currentRevisions.DefaultIfEmpty()
        join submitterValue in DbContext.Users.AsNoTracking() on request.CreatedByUserId equals submitterValue.Id into submitters
        from submitter in submitters.DefaultIfEmpty()
        join reviewerValue in DbContext.Users.AsNoTracking() on request.ReviewerUserId equals reviewerValue.Id into reviewers
        from reviewer in reviewers.DefaultIfEmpty()
        let canViewCave = cave != null && DbContext.UserCavePermissionView.Any(permission =>
            permission.AccountId == Scope.AccountId && permission.UserId == UserId &&
            permission.CaveId == request.CaveId)
        let canManageExistingCave = cave != null && DbContext.UserCavePermissionView.Any(permission =>
            permission.AccountId == Scope.AccountId && permission.UserId == UserId &&
            permission.CaveId == request.CaveId &&
            CaveManagerSatisfyingPermissionKeys.Contains(permission.PermissionKey))
        let hasCurrentViewCapability =
            DbContext.UserPermissions.Any(permission =>
                permission.UserId == UserId && permission.AccountId == Scope.AccountId &&
                permission.Permission!.PermissionType == PermissionType.User &&
                UserViewSatisfyingPermissionKeys.Contains(permission.Permission.Key)) ||
            DbContext.CavePermissions.Any(permission =>
                permission.UserId == UserId && permission.AccountId == Scope.AccountId &&
                permission.Permission!.PermissionType == PermissionType.Cave &&
                CaveViewSatisfyingPermissionKeys.Contains(permission.Permission.Key))
        let canManageProposedLocation =
            DbContext.UserPermissions.Any(permission =>
                permission.UserId == UserId && permission.AccountId == Scope.AccountId &&
                permission.Permission!.PermissionType == PermissionType.User &&
                UserManagerSatisfyingPermissionKeys.Contains(permission.Permission.Key)) ||
            DbContext.CavePermissions.Any(permission =>
                permission.UserId == UserId && permission.AccountId == Scope.AccountId &&
                permission.Permission!.PermissionType == PermissionType.Cave &&
                CaveManagerSatisfyingPermissionKeys.Contains(permission.Permission.Key) &&
                permission.CaveId == null &&
                ((permission.StateId == null && permission.CountyId == null) ||
                 permission.CountyId == request.ProposedCountyId ||
                 (permission.CountyId == null && permission.StateId == request.ProposedStateId)))
        let isCreate = request.BaseRevisionId == null
        where request.AccountId == Scope.AccountId &&
              ((!isCreate && canViewCave) ||
               (isCreate && (cave == null
                   ? request.ApprovedRevisionId == null &&
                     ((request.CreatedByUserId == UserId && hasCurrentViewCapability) || canManageProposedLocation)
                   : canViewCave))) &&
              (requestId == null || request.Id == requestId) &&
              (createdByUserId == null || request.CreatedByUserId == createdByUserId) &&
              (status == null || request.Status == status)
        select new CaveChangeRequestReadRow
        {
            Request = request, ProposalVersion = proposal, ProposalBaseRevision = proposalBaseRevision,
            CurrentRevision = currentRevision, LiveCaveName = cave == null ? null : cave.Name,
            CaveExists = cave != null,
            SubmitterName = submitter == null ? null : submitter.FirstName + " " + submitter.LastName,
            ReviewerName = reviewer == null ? null : reviewer.FirstName + " " + reviewer.LastName,
            CurrentCanReview = isCreate && cave == null ? canManageProposedLocation : canManageExistingCave
        };

    private static IOrderedQueryable<CaveChangeRequestReadRow> Order(
        IQueryable<CaveChangeRequestReadRow> query, CaveChangeRequestSortBy sortBy, bool descending, bool openFirst)
    {
        IOrderedQueryable<CaveChangeRequestReadRow> ordered;
        if (openFirst)
        {
            var open = query.OrderByDescending(row => CaveChangeRequestStatePolicy.OpenStatuses.Contains(row.Request.Status));
            ordered = sortBy switch
            {
                CaveChangeRequestSortBy.SubmittedOn => descending
                    ? open.ThenByDescending(row => row.Request.CreatedOn)
                    : open.ThenBy(row => row.Request.CreatedOn),
                CaveChangeRequestSortBy.CaveName => descending
                    ? open.ThenByDescending(row => row.LiveCaveName)
                    : open.ThenBy(row => row.LiveCaveName),
                _ => descending
                    ? open.ThenByDescending(row => row.Request.ModifiedOn ?? row.Request.CreatedOn)
                    : open.ThenBy(row => row.Request.ModifiedOn ?? row.Request.CreatedOn),
            };
        }
        else
        {
            ordered = sortBy switch
            {
                CaveChangeRequestSortBy.SubmittedOn => descending
                    ? query.OrderByDescending(row => row.Request.CreatedOn)
                    : query.OrderBy(row => row.Request.CreatedOn),
                CaveChangeRequestSortBy.CaveName => descending
                    ? query.OrderByDescending(row => row.LiveCaveName)
                    : query.OrderBy(row => row.LiveCaveName),
                _ => descending
                    ? query.OrderByDescending(row => row.Request.ModifiedOn ?? row.Request.CreatedOn)
                    : query.OrderBy(row => row.Request.ModifiedOn ?? row.Request.CreatedOn),
            };
        }

        return descending ? ordered.ThenByDescending(row => row.Request.Id) : ordered.ThenBy(row => row.Request.Id);
    }

    private static async Task<PagedResult<CaveChangeRequestReadRow>> PageAsync(
        IQueryable<CaveChangeRequestReadRow> query, int pageNumber, int pageSize,
        CancellationToken cancellationToken)
    {
        pageNumber = Math.Max(1, pageNumber);
        pageSize = pageSize < 1 ? QueryConstants.DefaultPageSize : Math.Min(pageSize, QueryConstants.MaxPageSize);
        var count = await query.CountAsync(cancellationToken);
        var totalPages = Math.Max(1, (int)Math.Ceiling(count / (double)pageSize));
        pageNumber = Math.Min(pageNumber, totalPages);
        var rows = await query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new PagedResult<CaveChangeRequestReadRow>(pageNumber, pageSize, count, rows);
    }

    private static CaveProposalVersion NewVersion(CaveChangeRequest request, string? baseRevisionId, string? previousId,
        CaveProposalSnapshotV1 proposal) => new()
    {
        AccountId = request.AccountId,
        ChangeRequestId = request.Id,
        CaveId = request.CaveId,
        BaseRevisionId = baseRevisionId,
        PreviousProposalVersionId = previousId,
        SchemaVersion = proposal.SchemaVersion,
        ProposalJson = CaveProposalJson.Serialize(proposal)
    };

    private async Task<IReadOnlyList<CaveProposalVersion>?> LoadValidatedVersionChainAsync(string requestId,
        CancellationToken cancellationToken)
    {
        var request = await DbContext.CaveChangeRequests.AsNoTracking()
            .Where(row => row.AccountId == Scope.AccountId && row.Id == requestId)
            .Select(row => new { row.CaveId, row.CurrentProposalVersionId })
            .SingleOrDefaultAsync(cancellationToken);
        if (request is null) return null;
        if (request.CurrentProposalVersionId is null)
            throw new InvalidOperationException("The proposal version history is missing its current version.");
        var versions = await DbContext.CaveProposalVersions.AsNoTracking().Where(version =>
                version.AccountId == Scope.AccountId && version.ChangeRequestId == requestId)
            .ToListAsync(cancellationToken);
        if (versions.Any(version => version.CaveId != request.CaveId))
            throw new InvalidOperationException("The proposal version history contains a version for another Cave.");
        return OrderVersionsByChain(request.CurrentProposalVersionId, versions);
    }

    private static IReadOnlyList<CaveProposalVersion> OrderVersionsByChain(string currentVersionId,
        IReadOnlyList<CaveProposalVersion> rows)
    {
        var byId = rows.ToDictionary(row => row.Id, StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var newestFirst = new List<CaveProposalVersion>(rows.Count);
        string? nextId = currentVersionId;
        while (nextId is not null)
        {
            if (!visited.Add(nextId))
                throw new InvalidOperationException("The proposal version history contains a cycle.");
            if (!byId.TryGetValue(nextId, out var row))
                throw new InvalidOperationException($"The proposal version history is missing version '{nextId}'.");
            newestFirst.Add(row);
            nextId = row.PreviousProposalVersionId;
        }

        if (visited.Count != rows.Count)
            throw new InvalidOperationException("The proposal version history contains versions outside the current chain.");

        newestFirst.Reverse();
        return newestFirst;
    }
}

public sealed class CaveProposalVersionConflictException : InvalidOperationException
{
    public CaveProposalVersionConflictException(string expectedProposalVersionId, string? actualProposalVersionId)
        : base("The active proposal version changed while it was being edited.")
    {
        ExpectedProposalVersionId = expectedProposalVersionId;
        ActualProposalVersionId = actualProposalVersionId;
    }

    public string ExpectedProposalVersionId { get; }
    public string? ActualProposalVersionId { get; }
}
