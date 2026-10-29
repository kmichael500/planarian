using Microsoft.EntityFrameworkCore;
using Planarian.Library.Exceptions;
using Planarian.Model.Database;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.Revisions;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Helpers;
using Planarian.Modules.Caves.Models;
using Planarian.Modules.Query.Constants;
using Planarian.Modules.Query.Extensions;
using Planarian.Modules.Tags;
using Planarian.Modules.Tags.Repositories;

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

public sealed class CaveChangeRequestRepository
{
    private const int CleanupLookupBatchSize = 500;
    private static readonly string[] UserViewSatisfyingPermissionKeys =
        PermissionPolicy.GetSatisfyingUserPermissionKeys(PermissionPolicyKey.View);
    private static readonly string[] CaveViewSatisfyingPermissionKeys =
        PermissionPolicy.GetSatisfyingCavePermissionKeys(PermissionPolicyKey.View);
    private static readonly string[] UserManagerSatisfyingPermissionKeys =
        PermissionPolicy.GetSatisfyingUserPermissionKeys(PermissionPolicyKey.Manager);
    private static readonly string[] CaveManagerSatisfyingPermissionKeys =
        PermissionPolicy.GetSatisfyingCavePermissionKeys(PermissionPolicyKey.Manager);
    private readonly PlanarianDbContext _db;
    private readonly RequestUser _user;
    private readonly AccountExecutionScope _scope;

    public CaveChangeRequestRepository(PlanarianDbContext db, RequestUser user)
    {
        _db = db;
        _user = user;
        _scope = AccountExecutionScope.Require(user);
    }

    public Task<string> CreateAsync(string caveId, string baseRevisionId, CaveProposalSnapshotV1 proposal,
        CancellationToken cancellationToken) =>
        CreateAsync(caveId, baseRevisionId, _ => Task.FromResult(proposal), cancellationToken);

    public async Task<string> CreateAsync(string caveId, string baseRevisionId,
        Func<CancellationToken, Task<CaveProposalSnapshotV1>> buildProposal,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var cave = await _db.Caves.FromSqlInterpolated(
                $"SELECT *, xmin FROM \"Caves\" WHERE \"AccountId\" = {_scope.AccountId} AND \"Id\" = {caveId} FOR KEY SHARE")
            .SingleOrDefaultAsync(cancellationToken);
        if (cave is null) throw ApiExceptionDictionary.NotFound("Cave");
        if (cave.CurrentRevisionId != baseRevisionId)
            throw new CaveRevisionConflictException(caveId, baseRevisionId, cave.CurrentRevisionId);

        var proposal = await buildProposal(cancellationToken);

        var baseRevision = await _db.CaveRevisions.SingleAsync(row => row.AccountId == _scope.AccountId &&
            row.CaveId == caveId && row.Id == baseRevisionId, cancellationToken);
        var baseSnapshot = CaveSnapshotJson.Deserialize(baseRevision.SnapshotJson, baseRevision.SnapshotSchemaVersion);
        var request = new CaveChangeRequest
        {
            AccountId = _scope.AccountId,
            CaveId = caveId,
            BaseRevisionId = baseRevisionId,
            BaseStateId = baseSnapshot.State.Id,
            BaseCountyId = baseSnapshot.County.Id,
            ProposedStateId = proposal.StateId,
            ProposedCountyId = proposal.CountyId,
            Status = CaveChangeRequestStatus.Pending
        };
        _db.CaveChangeRequests.Add(request);
        await _db.SaveChangesAsync(cancellationToken);
        await ClaimAuthoringStagedFilesAsync(request, proposal.Files
            .Where(file => file.Disposition == ProposalFileDisposition.PublishStaged)
            .Select(file => file.FileId), cancellationToken);
        var version = NewVersion(request, baseRevisionId, null, proposal);
        _db.CaveProposalVersions.Add(version);
        await _db.SaveChangesAsync(cancellationToken);
        request.CurrentProposalVersionId = version.Id;
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return request.Id;
    }

    public async Task<string> CreateNewAsync(string caveId,
        Func<CancellationToken, Task<CaveProposalSnapshotV1>> buildProposal,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        if (await _db.Caves.IgnoreQueryFilters().AnyAsync(cave =>
                cave.AccountId == _scope.AccountId && cave.Id == caveId, cancellationToken))
            throw new InvalidOperationException("The reserved Cave ID is already in use.");

        var proposal = await buildProposal(cancellationToken);
        if (proposal.CaveId != caveId || proposal.AccountId != _scope.AccountId)
            throw new InvalidOperationException("The new-Cave proposal identity does not match the request.");

        var request = new CaveChangeRequest
        {
            AccountId = _scope.AccountId,
            CaveId = caveId,
            BaseRevisionId = null,
            BaseStateId = null,
            BaseCountyId = null,
            ProposedStateId = proposal.StateId,
            ProposedCountyId = proposal.CountyId,
            Status = CaveChangeRequestStatus.Pending
        };
        _db.CaveChangeRequests.Add(request);
        await _db.SaveChangesAsync(cancellationToken);
        await ClaimAuthoringStagedFilesAsync(request, proposal.Files
            .Where(file => file.Disposition == ProposalFileDisposition.PublishStaged)
            .Select(file => file.FileId), cancellationToken);
        var version = NewVersion(request, null, null, proposal);
        _db.CaveProposalVersions.Add(version);
        await _db.SaveChangesAsync(cancellationToken);
        request.CurrentProposalVersionId = version.Id;
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return request.Id;
    }

    public Task<bool> CaveExistsAsync(string caveId, CancellationToken cancellationToken) =>
        _db.Caves.IgnoreQueryFilters().AnyAsync(cave =>
            cave.AccountId == _scope.AccountId && cave.Id == caveId, cancellationToken);

    public Task<string?> GetCurrentRevisionIdAsync(string caveId, CancellationToken cancellationToken) =>
        _db.Caves.Where(cave => cave.AccountId == _scope.AccountId && cave.Id == caveId)
            .Select(cave => cave.CurrentRevisionId).SingleOrDefaultAsync(cancellationToken);

    public async Task<string> AddVersionAsync(string requestId, string? baseRevisionId, string expectedProposalVersionId,
        CaveProposalSnapshotV1 proposal, bool reviewer, bool againstCurrent, CancellationToken cancellationToken)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var request = await _db.CaveChangeRequests.FromSqlInterpolated(
                $"SELECT *, xmin FROM \"CaveChangeRequests\" WHERE \"AccountId\" = {_scope.AccountId} AND \"Id\" = {requestId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken) ?? throw ApiExceptionDictionary.NotFound("Change request");
        if (!CaveChangeRequestStatePolicy.CanReceiveProposalVersions(request.Status))
            throw ApiExceptionDictionary.BadRequest("This request can no longer be revised.");
        var access = await GetOpenRequestAccessAsync(request, cancellationToken);
        if (!access.Visible) throw ApiExceptionDictionary.NotFound("Change request");
        reviewer = access.Reviewer;
        if (CaveChangeRequestStatePolicy.IsAwaitingAuthor(request.Status) && request.CreatedByUserId != _user.Id)
            throw ApiExceptionDictionary.Forbidden("Only the submitter can revise a request while changes are requested.");
        if (CaveChangeRequestStatePolicy.IsAwaitingReviewer(request.Status) && !reviewer && request.CreatedByUserId != _user.Id)
            throw ApiExceptionDictionary.Forbidden("You can only revise your own request.");
        if (request.CurrentProposalVersionId != expectedProposalVersionId)
            throw new CaveProposalVersionConflictException(expectedProposalVersionId,
                request.CurrentProposalVersionId);

        var currentProposal = await _db.CaveProposalVersions.SingleAsync(version =>
            version.AccountId == _scope.AccountId && version.CaveId == request.CaveId &&
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

            var currentRevisionId = await _db.Caves.Where(cave => cave.AccountId == _scope.AccountId &&
                    cave.Id == request.CaveId).Select(cave => cave.CurrentRevisionId).SingleAsync(cancellationToken);
            if (currentRevisionId != baseRevisionId)
                throw new CaveRevisionConflictException(request.CaveId, baseRevisionId, currentRevisionId);
        }

        await ClaimAuthoringStagedFilesAsync(request, proposal.Files
            .Where(file => file.Disposition == ProposalFileDisposition.PublishStaged)
            .Select(file => file.FileId), cancellationToken);
        var version = NewVersion(request, baseRevisionId, expectedProposalVersionId, proposal);
        _db.CaveProposalVersions.Add(version);
        await _db.SaveChangesAsync(cancellationToken);
        request.CurrentProposalVersionId = version.Id;
        request.ProposedStateId = proposal.StateId;
        request.ProposedCountyId = proposal.CountyId;
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return version.Id;
    }

    internal async Task StageFileAsync(string requestId, string fileId, string fileTypeTagId, string? name,
        bool reviewer, CancellationToken cancellationToken)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var request = await LockedAsync(requestId, cancellationToken);
        if (!CaveChangeRequestStatePolicy.CanReceiveProposalVersions(request.Status))
            throw ApiExceptionDictionary.BadRequest("This request can no longer receive files.");
        var access = await GetOpenRequestAccessAsync(request, cancellationToken);
        if (!access.Visible) throw ApiExceptionDictionary.NotFound("Change request");
        reviewer = access.Reviewer;
        if (CaveChangeRequestStatePolicy.IsAwaitingAuthor(request.Status) && request.CreatedByUserId != _user.Id)
            throw ApiExceptionDictionary.Forbidden("Only the submitter can add files while changes are requested.");
        if (CaveChangeRequestStatePolicy.IsAwaitingReviewer(request.Status) && !reviewer && request.CreatedByUserId != _user.Id)
            throw ApiExceptionDictionary.Forbidden("You can only add files to your own request.");

        var fileSnapshot = await (from file in _db.Files.AsNoTracking()
                from tag in _db.TagTypes.AsNoTracking()
                where file.AccountId == _scope.AccountId && file.Id == fileId && file.CaveId == null &&
                      tag.Id == fileTypeTagId
                select new { file.Name, file.Extension, FileTypeName = tag.Name })
            .SingleOrDefaultAsync(cancellationToken);
        if (fileSnapshot is null) throw ApiExceptionDictionary.NotFound("File");

        var current = await _db.CaveProposalVersions.SingleAsync(version =>
            version.AccountId == _scope.AccountId && version.ChangeRequestId == requestId &&
            version.Id == request.CurrentProposalVersionId, cancellationToken);
        var proposal = CaveProposalJson.Deserialize(current.ProposalJson, current.SchemaVersion);
        var files = proposal.Files.Where(file => file.FileId != fileId).Append(
            new ProposalFileIntent(fileId, ProposalFileDisposition.PublishStaged, fileTypeTagId,
                string.IsNullOrWhiteSpace(name) ? fileSnapshot.Name : name, fileSnapshot.Extension,
                fileSnapshot.FileTypeName)).ToList();
        var nextProposal = proposal with { Files = files };
        var nextVersion = NewVersion(request, current.BaseRevisionId, request.CurrentProposalVersionId, nextProposal);
        _db.CaveChangeRequestStagedFiles.Add(new CaveChangeRequestStagedFile
        {
            AccountId = _scope.AccountId,
            ChangeRequestId = requestId,
            FileId = fileId
        });
        _db.CaveProposalVersions.Add(nextVersion);
        await _db.SaveChangesAsync(cancellationToken);
        request.CurrentProposalVersionId = nextVersion.Id;
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, CaveFileSnapshotV1>> GetFileSnapshotsAsync(
        IEnumerable<string> fileIds, CancellationToken cancellationToken)
    {
        var ids = fileIds.Distinct(StringComparer.Ordinal).ToList();
        return await (from file in _db.Files.AsNoTracking()
                join tag in _db.TagTypes.AsNoTracking() on file.FileTypeTagId equals tag.Id
                where file.AccountId == _scope.AccountId && ids.Contains(file.Id)
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
        _db.CaveChangeRequestStagedFiles.AsNoTracking().AnyAsync(staged =>
            staged.AccountId == _scope.AccountId && staged.ChangeRequestId == requestId && staged.FileId == fileId,
            cancellationToken);

    public async Task<IReadOnlySet<string>> GetStagedFileIdsAsync(string requestId,
        CancellationToken cancellationToken) =>
        (await _db.CaveChangeRequestStagedFiles.AsNoTracking()
            .Where(staged => staged.AccountId == _scope.AccountId && staged.ChangeRequestId == requestId)
            .Select(staged => staged.FileId).ToListAsync(cancellationToken)).ToHashSet(StringComparer.Ordinal);

    public async Task<IReadOnlySet<string>> GetAuthorableStagedFileIdsAsync(IEnumerable<string> fileIds,
        string? requestId, CancellationToken cancellationToken)
    {
        var ids = fileIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToList();
        if (ids.Count == 0) return new HashSet<string>(StringComparer.Ordinal);

        var now = DateTime.UtcNow;
        var rows = await _db.Files.IgnoreQueryFilters().AsNoTracking()
            .Where(file => file.AccountId == _scope.AccountId && ids.Contains(file.Id) && file.CaveId == null &&
                           ((requestId != null && _db.CaveChangeRequestStagedFiles.IgnoreQueryFilters().Any(staged =>
                                staged.AccountId == _scope.AccountId && staged.ChangeRequestId == requestId &&
                                staged.FileId == file.Id)) ||
                            (file.CreatedByUserId == _user.Id && file.ExpiresOn != null && file.ExpiresOn > now &&
                             !_db.CaveChangeRequestStagedFiles.IgnoreQueryFilters().Any(staged =>
                                 staged.AccountId == _scope.AccountId && staged.FileId == file.Id))))
            .Select(file => file.Id)
            .ToListAsync(cancellationToken);
        return rows.ToHashSet(StringComparer.Ordinal);
    }

    public async Task<IReadOnlyList<StagedCaveFilePublication>> PlanStagedFilePublicationsAsync(
        string requestId, IReadOnlyDictionary<string, string> extensionsByFileId,
        CancellationToken cancellationToken)
    {
        var ids = extensionsByFileId.Keys.OrderBy(id => id, StringComparer.Ordinal).ToList();
        if (ids.Count == 0) return [];

        var rows = await (from staged in _db.CaveChangeRequestStagedFiles.IgnoreQueryFilters()
                join file in _db.Files.IgnoreQueryFilters()
                    on new { staged.AccountId, staged.FileId } equals new { file.AccountId, FileId = file.Id }
                where staged.AccountId == _scope.AccountId && staged.ChangeRequestId == requestId &&
                      ids.Contains(staged.FileId) && file.CaveId == null
                select file)
            .ToListAsync(cancellationToken);
        if (rows.Count != ids.Count || rows.Select(row => row.Id).Distinct(StringComparer.Ordinal).Count() != ids.Count)
            throw ApiExceptionDictionary.NotFound("Staged file");

        var sharedPendingFileId = await (from staged in _db.CaveChangeRequestStagedFiles.IgnoreQueryFilters()
                join request in _db.CaveChangeRequests.IgnoreQueryFilters()
                    on new { staged.AccountId, Id = staged.ChangeRequestId }
                    equals new { request.AccountId, request.Id }
                where staged.AccountId == _scope.AccountId && ids.Contains(staged.FileId) &&
                      staged.ChangeRequestId != requestId && CaveChangeRequestStatePolicy.OpenStatuses.Contains(request.Status)
                select staged.FileId)
            .FirstOrDefaultAsync(cancellationToken);
        if (sharedPendingFileId is not null)
            throw ApiExceptionDictionary.BadRequest(
                "A staged file shared with another pending request cannot be published.");

        return rows.Select(file =>
        {
            if (string.IsNullOrWhiteSpace(file.BlobKey) || string.IsNullOrWhiteSpace(file.BlobContainer))
                throw ApiExceptionDictionary.NotFound("Staged file");
            return new StagedCaveFilePublication(file.Id, file.BlobKey, file.BlobContainer,
                extensionsByFileId[file.Id]);
        }).OrderBy(publication => publication.FileId, StringComparer.Ordinal).ToList();
    }

    public Task<PagedResult<CaveChangeRequestReadRow>> ListMineAsync(int pageNumber, int pageSize,
        CancellationToken cancellationToken, CaveChangeRequestStatus? status = null,
        CaveChangeRequestSortBy sortBy = CaveChangeRequestSortBy.UpdatedOn, bool sortDescending = true) =>
        PageAsync(Order(Query(createdByUserId: _user.Id, status: status), sortBy, sortDescending, openFirst: true),
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
        var actorNames = await _db.Users.AsNoTracking().Where(actor => actorIds.Contains(actor.Id))
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
        var baseRevisions = await _db.CaveRevisions.AsNoTracking().Where(revision =>
                revision.AccountId == _scope.AccountId && revision.CaveId == currentVersion.CaveId &&
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
        return await _db.TagTypes.AsNoTracking()
            .Where(tag => distinct.Contains(tag.Id))
            .Select(tag => new TagNameCandidate(tag.Id, tag.Name, tag.Key, tag.AccountId, tag.IsDefault))
            .ToListAsync(cancellationToken);
    }

    public Task<IReadOnlyList<TagNameCandidate>> GetEligiblePeopleCandidatesAsync(
        CancellationToken cancellationToken) =>
        EligiblePeopleTagLookup.GetEligibleAsync(_db, _scope.AccountId, cancellationToken);

    public async Task<(string StateName, string? StateAbbreviation, string CountyName, string CountyDisplayId)>
        GetLocationLabelsAsync(string stateId, string countyId, CancellationToken cancellationToken)
    {
        var pair = await (from county in _db.Counties.AsNoTracking()
                join state in _db.States.AsNoTracking() on county.StateId equals state.Id
                where county.AccountId == _scope.AccountId && county.Id == countyId &&
                      county.StateId == stateId && state.Id == stateId
                select new { StateName = state.Name, state.Abbreviation, CountyName = county.Name, county.DisplayId })
            .SingleOrDefaultAsync(cancellationToken);
        if (pair is null) throw ApiExceptionDictionary.BadRequest("The selected County does not belong to the selected State.");
        return (pair.StateName, pair.Abbreviation, pair.CountyName, pair.DisplayId);
    }

    public async Task<IReadOnlyList<CaveChangeRequestReviewEventReadRow>> ListReviewEventsAsync(string requestId,
        CancellationToken cancellationToken) =>
        await (from reviewEvent in _db.CaveChangeRequestReviewEvents.AsNoTracking()
               join actorValue in _db.Users.AsNoTracking() on reviewEvent.ActorUserId equals actorValue.Id into actors
               from actor in actors.DefaultIfEmpty()
               where reviewEvent.AccountId == _scope.AccountId && reviewEvent.ChangeRequestId == requestId
               orderby reviewEvent.CreatedOn, reviewEvent.Id
               select new CaveChangeRequestReviewEventReadRow(reviewEvent,
                   actor == null ? null : actor.FirstName + " " + actor.LastName)).ToListAsync(cancellationToken);

    public async Task RequestChangesAsync(string requestId, string expectedProposalVersionId, string message,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(message))
            throw ApiExceptionDictionary.BadRequest("A message is required when requesting changes.");
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var request = await LockedAsync(requestId, cancellationToken);
        RequireAwaitingReviewer(request.Status);
        RequireExpectedProposalVersion(request, expectedProposalVersionId);
        request.Status = CaveChangeRequestStatePolicy.AwaitingAuthorStatus;
        AddReviewEvent(request, expectedProposalVersionId, CaveChangeRequestReviewAction.ChangesRequested, message.Trim());
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task ResubmitAsync(string requestId, string expectedProposalVersionId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var request = await LockedAsync(requestId, cancellationToken);
        if (!CaveChangeRequestStatePolicy.IsAwaitingAuthor(request.Status))
            throw ApiExceptionDictionary.BadRequest("This request is not awaiting changes from the submitter.");
        if (request.CreatedByUserId != _user.Id)
            throw ApiExceptionDictionary.Forbidden("Only the submitter can resubmit requested changes.");
        RequireExpectedProposalVersion(request, expectedProposalVersionId);
        var requestedVersionId = await _db.CaveChangeRequestReviewEvents.AsNoTracking()
            .Where(reviewEvent => reviewEvent.AccountId == _scope.AccountId &&
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
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> RejectAsync(string requestId,
        string expectedProposalVersionId, string? notes,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var request = await LockedAsync(requestId, cancellationToken);
        RequireAwaitingReviewer(request.Status);
        RequireExpectedProposalVersion(request, expectedProposalVersionId);
        if (string.IsNullOrWhiteSpace(notes))
            throw ApiExceptionDictionary.BadRequest("A reason is required to reject requested changes.");
        request.Status = CaveChangeRequestStatus.Rejected;
        request.ReviewerUserId = _user.Id;
        request.ReviewerNotes = notes.Trim();
        request.ReviewedOn = DateTime.UtcNow;
        AddReviewEvent(request, expectedProposalVersionId, CaveChangeRequestReviewAction.Rejected, request.ReviewerNotes);
        var cleanupTargets = await RemoveRemainingStagedFilesAsync(requestId, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        var cleanupTargetIds = cleanupTargets.Select(target => target.Id)
            .Distinct(StringComparer.Ordinal).ToList();
        await transaction.CommitAsync(cancellationToken);
        return cleanupTargetIds;
    }

    public async Task GrantSubmitterCaveViewAsync(string userId, string caveId,
        CancellationToken cancellationToken)
    {
        if (_db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Submitter View grants require an active PostgreSQL transaction.");

        // Lock current membership so approval and account removal cannot race into a stale
        // CavePermission. If removal wins first there is no membership and no grant; if
        // approval wins first, removal waits and then deletes the newly-created grant.
        var accountMember = await _db.AccountUsers.FromSqlInterpolated($"""
                SELECT * FROM "AccountUsers"
                WHERE "AccountId" = {_scope.AccountId} AND "UserId" = {userId}
                FOR KEY SHARE
                """)
            .IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        if (accountMember is null) return;

        var permissionId = await _db.Permissions.AsNoTracking()
            .Where(permission => permission.Key == PermissionPolicyKey.View &&
                                 permission.PermissionType == PermissionType.Cave)
            .Select(permission => permission.Id)
            .SingleAsync(cancellationToken);
        var alreadyGranted = await _db.CavePermissions.IgnoreQueryFilters().AnyAsync(permission =>
            permission.AccountId == _scope.AccountId && permission.UserId == userId &&
            permission.CaveId == caveId && permission.PermissionId == permissionId,
            cancellationToken);
        if (alreadyGranted) return;

        _db.CavePermissions.Add(new CavePermission
        {
            AccountId = _scope.AccountId,
            UserId = userId,
            PermissionId = permissionId,
            CaveId = caveId
        });
        await _db.SaveChangesAsync(cancellationToken);
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
        request.ReviewerUserId = _user.Id;
        request.ReviewerNotes = notes?.Trim();
        request.ReviewedOn = DateTime.UtcNow;
        AddReviewEvent(request, expectedProposalVersionId, CaveChangeRequestReviewAction.Approved, request.ReviewerNotes);
        var cleanupTargets = await RemoveRemainingStagedFilesAsync(requestId, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return cleanupTargets.Select(target => target.Id).Distinct(StringComparer.Ordinal).ToList();
    }

    private async Task<IReadOnlyList<StorageCleanupTarget>> RemoveRemainingStagedFilesAsync(
        string requestId, CancellationToken cancellationToken)
    {
        var stagedFileIds = await _db.CaveChangeRequestStagedFiles
            .Where(staged => staged.AccountId == _scope.AccountId && staged.ChangeRequestId == requestId)
            .Select(staged => staged.FileId)
            .Distinct()
            .ToListAsync(cancellationToken);
        if (stagedFileIds.Count == 0) return [];

        await _db.CaveChangeRequestStagedFiles
            .Where(staged => staged.AccountId == _scope.AccountId && staged.ChangeRequestId == requestId)
            .ExecuteDeleteAsync(cancellationToken);
        var filesToDelete = await _db.Files
            .Where(file => file.AccountId == _scope.AccountId && stagedFileIds.Contains(file.Id) &&
                           file.CaveId == null &&
                           !_db.CaveChangeRequestStagedFiles.Any(staged =>
                               staged.AccountId == _scope.AccountId && staged.FileId == file.Id))
            .ToListAsync(cancellationToken);

        var cleanupTargets = await QueueUnpublishedFileObjectCleanupAsync(
            filesToDelete, cancellationToken);

        _db.Files.RemoveRange(filesToDelete);
        return cleanupTargets;
    }

    private async Task<IReadOnlyList<StorageCleanupTarget>> QueueUnpublishedFileObjectCleanupAsync(
        IEnumerable<Planarian.Model.Database.Entities.RidgeWalker.File> files,
        CancellationToken cancellationToken)
    {
        var candidates = new List<(string Partition, string Key)>();
        foreach (var file in files)
        {
            if (file.AccountId != _scope.AccountId || file.CaveId is not null)
                throw new InvalidOperationException("The staged File is not an unpublished File for this account.");
            if (string.IsNullOrWhiteSpace(file.BlobKey) && string.IsNullOrWhiteSpace(file.BlobContainer)) continue;
            if (string.IsNullOrWhiteSpace(file.BlobKey) || string.IsNullOrWhiteSpace(file.BlobContainer))
                throw new InvalidOperationException("A staged File has an incomplete object-storage address.");
            candidates.Add((file.BlobContainer, file.BlobKey));
        }

        var addresses = candidates.Distinct().ToList();
        if (addresses.Count == 0) return [];

        var existingByAddress = _db.StorageCleanupTargets.Local
            .Where(row => row.AccountId == _scope.AccountId &&
                          row.Purpose == StorageCleanupPurpose.UnpublishedFileRemoval && !row.IsContainer)
            .ToDictionary(row => (row.StoragePartition, row.StorageKey));

        foreach (var partition in addresses.GroupBy(address => address.Partition))
        {
            foreach (var keyChunk in partition.Select(address => address.Key)
                         .Distinct().Chunk(CleanupLookupBatchSize))
            {
                var keys = keyChunk.ToArray();
                var existing = await _db.StorageCleanupTargets
                    .Where(row => row.AccountId == _scope.AccountId &&
                                  row.Purpose == StorageCleanupPurpose.UnpublishedFileRemoval &&
                                  !row.IsContainer && row.StoragePartition == partition.Key &&
                                  keys.Contains(row.StorageKey))
                    .ToListAsync(cancellationToken);
                foreach (var target in existing)
                    existingByAddress.TryAdd((target.StoragePartition, target.StorageKey), target);
            }
        }

        var targets = new List<StorageCleanupTarget>(addresses.Count);
        foreach (var address in addresses)
        {
            if (existingByAddress.TryGetValue(address, out var existing))
            {
                targets.Add(existing);
                continue;
            }

            var target = new StorageCleanupTarget
            {
                AccountId = _scope.AccountId,
                StoragePartition = address.Partition,
                StorageKey = address.Key,
                Purpose = StorageCleanupPurpose.UnpublishedFileRemoval
            };
            _db.StorageCleanupTargets.Add(target);
            existingByAddress.Add(address, target);
            targets.Add(target);
        }

        return targets;
    }

    private async Task ClaimAuthoringStagedFilesAsync(CaveChangeRequest request, IEnumerable<string> fileIds,
        CancellationToken cancellationToken)
    {
        var requestedIds = fileIds.Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        if (requestedIds.Count == 0) return;

        var existingIds = (await _db.CaveChangeRequestStagedFiles.IgnoreQueryFilters()
            .Where(staged => staged.AccountId == _scope.AccountId && staged.ChangeRequestId == request.Id &&
                             requestedIds.Contains(staged.FileId))
            .Select(staged => staged.FileId).ToListAsync(cancellationToken)).ToHashSet(StringComparer.Ordinal);

        var now = DateTime.UtcNow;
        foreach (var fileId in requestedIds.Where(id => !existingIds.Contains(id)))
        {
            var file = await _db.Files.FromSqlInterpolated(
                    $"SELECT * FROM \"Files\" WHERE \"AccountId\" = {_scope.AccountId} AND \"Id\" = {fileId} FOR UPDATE")
                .IgnoreQueryFilters().SingleOrDefaultAsync(cancellationToken);
            if (file is null || file.CaveId is not null || file.ExpiresOn is null || file.ExpiresOn <= now ||
                file.CreatedByUserId != _user.Id)
                throw ApiExceptionDictionary.NotFound("Staged file");

            var alreadyBound = await _db.CaveChangeRequestStagedFiles.IgnoreQueryFilters().AnyAsync(staged =>
                staged.AccountId == _scope.AccountId && staged.FileId == fileId, cancellationToken);
            if (alreadyBound)
                throw ApiExceptionDictionary.BadRequest("The staged file is already bound to another change request.");

            _db.CaveChangeRequestStagedFiles.Add(new CaveChangeRequestStagedFile
            {
                AccountId = _scope.AccountId,
                ChangeRequestId = request.Id,
                FileId = fileId
            });
        }
    }

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
        _db.CaveChangeRequestReviewEvents.Add(new CaveChangeRequestReviewEvent
        {
            AccountId = request.AccountId,
            ChangeRequestId = request.Id,
            ProposalVersionId = proposalVersionId,
            Action = action,
            ActorUserId = _user.Id!,
            Message = string.IsNullOrWhiteSpace(message) ? null : message.Trim()
        });
    }

    private async Task<(bool Visible, bool Reviewer)> GetOpenRequestAccessAsync(
        CaveChangeRequest request, CancellationToken cancellationToken)
    {
        if (request.BaseRevisionId is null)
        {
            var reviewer = await CanManageProposedLocationAsync(request, cancellationToken);
            var submitterVisible = request.CreatedByUserId == _user.Id &&
                                   await HasCurrentViewCapabilityAsync(cancellationToken);
            return (submitterVisible || reviewer, reviewer);
        }

        var visible = await _db.UserCavePermissionView.AnyAsync(permission =>
            permission.AccountId == _scope.AccountId && permission.UserId == _user.Id &&
            permission.CaveId == request.CaveId, cancellationToken);
        var existingReviewer = visible && await _db.UserCavePermissionView.AnyAsync(permission =>
            permission.AccountId == _scope.AccountId && permission.UserId == _user.Id &&
            permission.CaveId == request.CaveId &&
            CaveManagerSatisfyingPermissionKeys.Contains(permission.PermissionKey),
            cancellationToken);
        return (visible, existingReviewer);
    }

    private async Task<bool> HasCurrentViewCapabilityAsync(CancellationToken cancellationToken)
    {
        if (await _db.UserPermissions.AnyAsync(permission =>
                permission.UserId == _user.Id && permission.AccountId == _scope.AccountId &&
                permission.Permission!.PermissionType == PermissionType.User &&
                UserViewSatisfyingPermissionKeys.Contains(permission.Permission.Key), cancellationToken))
            return true;

        return await _db.CavePermissions.AnyAsync(permission =>
            permission.UserId == _user.Id && permission.AccountId == _scope.AccountId &&
            permission.Permission!.PermissionType == PermissionType.Cave &&
            CaveViewSatisfyingPermissionKeys.Contains(permission.Permission.Key), cancellationToken);
    }

    private async Task<bool> CanManageProposedLocationAsync(CaveChangeRequest request,
        CancellationToken cancellationToken)
    {
        if (await _db.UserPermissions.AnyAsync(permission =>
                permission.UserId == _user.Id && permission.AccountId == _scope.AccountId &&
                permission.Permission!.PermissionType == PermissionType.User &&
                UserManagerSatisfyingPermissionKeys.Contains(permission.Permission.Key), cancellationToken))
            return true;

        return await _db.CavePermissions.AnyAsync(permission =>
            permission.UserId == _user.Id && permission.AccountId == _scope.AccountId &&
            permission.Permission!.PermissionType == PermissionType.Cave &&
            CaveManagerSatisfyingPermissionKeys.Contains(permission.Permission.Key) &&
            permission.CaveId == null &&
            ((permission.StateId == null && permission.CountyId == null) ||
             permission.CountyId == request.ProposedCountyId ||
             (permission.CountyId == null && permission.StateId == request.ProposedStateId)),
            cancellationToken);
    }

    private async Task<CaveChangeRequest> LockedAsync(string requestId, CancellationToken cancellationToken) =>
        await _db.CaveChangeRequests.FromSqlInterpolated(
                $"SELECT *, xmin FROM \"CaveChangeRequests\" WHERE \"AccountId\" = {_scope.AccountId} AND \"Id\" = {requestId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken) ?? throw ApiExceptionDictionary.NotFound("Change request");

    private IQueryable<CaveChangeRequestReadRow> Query(string? requestId = null, string? createdByUserId = null,
        CaveChangeRequestStatus? status = null) =>
        from request in _db.CaveChangeRequests.AsNoTracking()
        join proposal in _db.CaveProposalVersions.AsNoTracking()
            on new { request.AccountId, ChangeRequestId = request.Id, Id = request.CurrentProposalVersionId }
            equals new { proposal.AccountId, proposal.ChangeRequestId, Id = (string?)proposal.Id }
        join caveValue in _db.Caves.IgnoreQueryFilters().AsNoTracking()
                .Where(cave => cave.AccountId == _scope.AccountId)
            on new { request.AccountId, Id = request.CaveId }
            equals new { caveValue.AccountId, caveValue.Id } into caves
        from cave in caves.DefaultIfEmpty()
        join proposalBaseRevisionValue in _db.CaveRevisions.AsNoTracking()
            on new { proposal.AccountId, proposal.CaveId, Id = proposal.BaseRevisionId }
            equals new { proposalBaseRevisionValue.AccountId, proposalBaseRevisionValue.CaveId,
                Id = (string?)proposalBaseRevisionValue.Id } into proposalBaseRevisions
        from proposalBaseRevision in proposalBaseRevisions.DefaultIfEmpty()
        join currentRevisionValue in _db.CaveRevisions.AsNoTracking()
            on new { request.AccountId, Id = cave == null ? null : cave.CurrentRevisionId }
            equals new { currentRevisionValue.AccountId, Id = (string?)currentRevisionValue.Id } into currentRevisions
        from currentRevision in currentRevisions.DefaultIfEmpty()
        join submitterValue in _db.Users.AsNoTracking() on request.CreatedByUserId equals submitterValue.Id into submitters
        from submitter in submitters.DefaultIfEmpty()
        join reviewerValue in _db.Users.AsNoTracking() on request.ReviewerUserId equals reviewerValue.Id into reviewers
        from reviewer in reviewers.DefaultIfEmpty()
        let canViewCave = cave != null && _db.UserCavePermissionView.Any(permission =>
            permission.AccountId == _scope.AccountId && permission.UserId == _user.Id &&
            permission.CaveId == request.CaveId)
        let canManageExistingCave = cave != null && _db.UserCavePermissionView.Any(permission =>
            permission.AccountId == _scope.AccountId && permission.UserId == _user.Id &&
            permission.CaveId == request.CaveId &&
            CaveManagerSatisfyingPermissionKeys.Contains(permission.PermissionKey))
        let hasCurrentViewCapability =
            _db.UserPermissions.Any(permission =>
                permission.UserId == _user.Id && permission.AccountId == _scope.AccountId &&
                permission.Permission!.PermissionType == PermissionType.User &&
                UserViewSatisfyingPermissionKeys.Contains(permission.Permission.Key)) ||
            _db.CavePermissions.Any(permission =>
                permission.UserId == _user.Id && permission.AccountId == _scope.AccountId &&
                permission.Permission!.PermissionType == PermissionType.Cave &&
                CaveViewSatisfyingPermissionKeys.Contains(permission.Permission.Key))
        let canManageProposedLocation =
            _db.UserPermissions.Any(permission =>
                permission.UserId == _user.Id && permission.AccountId == _scope.AccountId &&
                permission.Permission!.PermissionType == PermissionType.User &&
                UserManagerSatisfyingPermissionKeys.Contains(permission.Permission.Key)) ||
            _db.CavePermissions.Any(permission =>
                permission.UserId == _user.Id && permission.AccountId == _scope.AccountId &&
                permission.Permission!.PermissionType == PermissionType.Cave &&
                CaveManagerSatisfyingPermissionKeys.Contains(permission.Permission.Key) &&
                permission.CaveId == null &&
                ((permission.StateId == null && permission.CountyId == null) ||
                 permission.CountyId == request.ProposedCountyId ||
                 (permission.CountyId == null && permission.StateId == request.ProposedStateId)))
        let isCreate = request.BaseRevisionId == null
        where request.AccountId == _scope.AccountId &&
              ((!isCreate && canViewCave) ||
               (isCreate && (cave == null
                   ? request.ApprovedRevisionId == null &&
                     ((request.CreatedByUserId == _user.Id && hasCurrentViewCapability) || canManageProposedLocation)
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
        var request = await _db.CaveChangeRequests.AsNoTracking()
            .Where(row => row.AccountId == _scope.AccountId && row.Id == requestId)
            .Select(row => new { row.CaveId, row.CurrentProposalVersionId })
            .SingleOrDefaultAsync(cancellationToken);
        if (request is null) return null;
        if (request.CurrentProposalVersionId is null)
            throw new InvalidOperationException("The proposal version history is missing its current version.");
        var versions = await _db.CaveProposalVersions.AsNoTracking().Where(version =>
                version.AccountId == _scope.AccountId && version.ChangeRequestId == requestId)
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
