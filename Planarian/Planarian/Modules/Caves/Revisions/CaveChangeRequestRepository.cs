using Microsoft.EntityFrameworkCore;
using Planarian.Library.Exceptions;
using Planarian.Model.Database;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.Revisions;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Helpers;
using Planarian.Modules.Query.Constants;
using Planarian.Modules.Query.Extensions;
using Planarian.Modules.Tags;
using Planarian.Modules.Tags.Repositories;

namespace Planarian.Modules.Caves.Revisions;

public sealed class CaveChangeRequestReadRow
{
    public CaveChangeRequest Request { get; init; } = null!;
    public CaveProposalVersion ProposalVersion { get; init; } = null!;
    public CaveRevision ProposalBaseRevision { get; init; } = null!;
    public CaveRevision? CurrentRevision { get; init; }
    public string? LiveCaveName { get; init; }
    public bool CaveExists { get; init; }
    public string? SubmitterName { get; init; }
    public string? ReviewerName { get; init; }
    public bool CurrentCanReview { get; init; }
}

public sealed record CaveProposalVersionReadRow(CaveProposalVersion Version, string? ActorName);
public sealed record CaveProposalVersionDetailReadRow(CaveProposalVersion Version, CaveRevision BaseRevision,
    CaveProposalVersion? PreviousVersion, CaveRevision? PreviousBaseRevision);

public sealed class CaveChangeRequestRepository
{
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

    public Task<string?> GetCurrentRevisionIdAsync(string caveId, CancellationToken cancellationToken) =>
        _db.Caves.Where(cave => cave.AccountId == _scope.AccountId && cave.Id == caveId)
            .Select(cave => cave.CurrentRevisionId).SingleOrDefaultAsync(cancellationToken);

    public async Task<string> AddVersionAsync(string requestId, string baseRevisionId, string expectedProposalVersionId,
        CaveProposalSnapshotV1 proposal, bool reviewer, bool againstCurrent, CancellationToken cancellationToken)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var request = await _db.CaveChangeRequests.FromSqlInterpolated(
                $"SELECT *, xmin FROM \"CaveChangeRequests\" WHERE \"AccountId\" = {_scope.AccountId} AND \"Id\" = {requestId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken) ?? throw ApiExceptionDictionary.NotFound("Change request");
        if (request.Status != CaveChangeRequestStatus.Pending)
            throw ApiExceptionDictionary.BadRequest("Only pending requests can be revised.");
        var visible = await _db.UserCavePermissionView.AnyAsync(permission =>
            permission.AccountId == _scope.AccountId && permission.UserId == _user.Id &&
            permission.CaveId == request.CaveId, cancellationToken);
        if (!visible) throw ApiExceptionDictionary.NotFound("Change request");
        reviewer = await _db.UserCavePermissionView.AnyAsync(permission =>
            permission.AccountId == _scope.AccountId && permission.UserId == _user.Id &&
            permission.CaveId == request.CaveId && permission.PermissionKey == PermissionPolicyKey.Manager,
            cancellationToken);
        if (!reviewer && request.CreatedByUserId != _user.Id)
            throw ApiExceptionDictionary.Forbidden("You can only revise your own request.");
        if (request.CurrentProposalVersionId != expectedProposalVersionId)
            throw new CaveProposalVersionConflictException(expectedProposalVersionId,
                request.CurrentProposalVersionId);

        var currentProposal = await _db.CaveProposalVersions.SingleAsync(version =>
            version.AccountId == _scope.AccountId && version.CaveId == request.CaveId &&
            version.ChangeRequestId == requestId && version.Id == expectedProposalVersionId, cancellationToken);
        if (!againstCurrent && currentProposal.BaseRevisionId != baseRevisionId)
            throw new CaveRevisionConflictException(request.CaveId, baseRevisionId,
                currentProposal.BaseRevisionId);

        var currentRevisionId = await _db.Caves.Where(cave => cave.AccountId == _scope.AccountId &&
                cave.Id == request.CaveId).Select(cave => cave.CurrentRevisionId).SingleAsync(cancellationToken);
        if (currentRevisionId != baseRevisionId)
            throw new CaveRevisionConflictException(request.CaveId, baseRevisionId, currentRevisionId);

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

    internal async Task StageFileAsync(string requestId, string fileId, string fileTypeTagId, string? displayName,
        bool reviewer, CancellationToken cancellationToken)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var request = await LockedAsync(requestId, cancellationToken);
        if (request.Status != CaveChangeRequestStatus.Pending)
            throw ApiExceptionDictionary.BadRequest("Only pending requests can receive files.");
        var visible = await _db.UserCavePermissionView.AnyAsync(permission =>
            permission.AccountId == _scope.AccountId && permission.UserId == _user.Id &&
            permission.CaveId == request.CaveId, cancellationToken);
        if (!visible) throw ApiExceptionDictionary.NotFound("Change request");
        reviewer = await _db.UserCavePermissionView.AnyAsync(permission =>
            permission.AccountId == _scope.AccountId && permission.UserId == _user.Id &&
            permission.CaveId == request.CaveId && permission.PermissionKey == PermissionPolicyKey.Manager,
            cancellationToken);
        if (!reviewer && request.CreatedByUserId != _user.Id)
            throw ApiExceptionDictionary.Forbidden("You can only add files to your own request.");

        var fileSnapshot = await (from file in _db.Files.AsNoTracking()
                from tag in _db.TagTypes.AsNoTracking()
                where file.AccountId == _scope.AccountId && file.Id == fileId && file.CaveId == null &&
                      tag.Id == fileTypeTagId
                select new { file.FileName, file.DisplayName, FileTypeName = tag.Name })
            .SingleOrDefaultAsync(cancellationToken);
        if (fileSnapshot is null) throw ApiExceptionDictionary.NotFound("File");

        var current = await _db.CaveProposalVersions.SingleAsync(version =>
            version.AccountId == _scope.AccountId && version.ChangeRequestId == requestId &&
            version.Id == request.CurrentProposalVersionId, cancellationToken);
        var proposal = CaveProposalJson.Deserialize(current.ProposalJson, current.SchemaVersion);
        var files = proposal.Files.Where(file => file.FileId != fileId).Append(
            new ProposalFileIntent(fileId, ProposalFileDisposition.PublishStaged, fileTypeTagId, displayName,
                CaveFileNamePolicy.GetEffectiveFileName(fileSnapshot.FileName, fileSnapshot.DisplayName, displayName),
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
                    FileName = file.FileName,
                    DisplayName = file.DisplayName
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
        string requestId, IEnumerable<string> fileIds, CancellationToken cancellationToken)
    {
        var ids = fileIds.Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal).ToList();
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
                      staged.ChangeRequestId != requestId && request.Status == CaveChangeRequestStatus.Pending
                select staged.FileId)
            .FirstOrDefaultAsync(cancellationToken);
        if (sharedPendingFileId is not null)
            throw ApiExceptionDictionary.BadRequest(
                "A staged file shared with another pending request cannot be published.");

        return rows.Select(file =>
        {
            if (string.IsNullOrWhiteSpace(file.BlobKey) || string.IsNullOrWhiteSpace(file.BlobContainer))
                throw ApiExceptionDictionary.NotFound("Staged file");
            return new StagedCaveFilePublication(file.Id, file.BlobKey, file.BlobContainer);
        }).OrderBy(publication => publication.FileId, StringComparer.Ordinal).ToList();
    }

    public Task<PagedResult<CaveChangeRequestReadRow>> ListMineAsync(int pageNumber, int pageSize,
        CancellationToken cancellationToken) => PageAsync(Query(createdByUserId: _user.Id)
            .OrderByDescending(row => row.Request.CreatedOn).ThenByDescending(row => row.Request.Id),
            pageNumber, pageSize, cancellationToken);

    public Task<PagedResult<CaveChangeRequestReadRow>> ListForReviewAsync(int pageNumber, int pageSize,
        CancellationToken cancellationToken) => PageAsync(Query(status: CaveChangeRequestStatus.Pending)
            .Where(row => row.CurrentCanReview).OrderBy(row => row.Request.CreatedOn).ThenBy(row => row.Request.Id),
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
        if (!baseRevisions.TryGetValue(currentVersion.BaseRevisionId, out var currentBaseRevision))
            throw new InvalidOperationException("The proposal version has a missing base revision.");
        CaveRevision? previousBaseRevision = null;
        if (previousVersion is not null &&
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

    public async Task<IReadOnlyList<StagedFileObjectDeleteTarget>> RejectAsync(string requestId,
        string expectedProposalVersionId, string? notes,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var request = await LockedAsync(requestId, cancellationToken);
        if (request.Status != CaveChangeRequestStatus.Pending)
            throw ApiExceptionDictionary.BadRequest("This request has already been reviewed.");
        if (request.CurrentProposalVersionId != expectedProposalVersionId)
            throw new CaveProposalVersionConflictException(expectedProposalVersionId,
                request.CurrentProposalVersionId);
        request.Status = CaveChangeRequestStatus.Rejected;
        request.ReviewerUserId = _user.Id;
        request.ReviewerNotes = notes?.Trim();
        request.ReviewedOn = DateTime.UtcNow;
        var blobs = await RemoveRemainingStagedFilesAsync(requestId, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return blobs;
    }

    public async Task<IReadOnlyList<StagedFileObjectDeleteTarget>> MarkApprovedAsync(string requestId,
        string expectedProposalVersionId,
        CaveMutationResult mutation, string? notes, CancellationToken cancellationToken)
    {
        if (!mutation.CreatedRevision || mutation.RevisionId is null)
            throw ApiExceptionDictionary.BadRequest("The proposal does not contain a publishable change.");
        var request = await LockedAsync(requestId, cancellationToken);
        if (request.Status != CaveChangeRequestStatus.Pending)
            throw ApiExceptionDictionary.BadRequest("This request has already been reviewed.");
        if (request.CurrentProposalVersionId != expectedProposalVersionId)
            throw new CaveProposalVersionConflictException(expectedProposalVersionId,
                request.CurrentProposalVersionId);
        request.Status = CaveChangeRequestStatus.Approved;
        request.ApprovedRevisionId = mutation.RevisionId;
        request.ReviewerUserId = _user.Id;
        request.ReviewerNotes = notes?.Trim();
        request.ReviewedOn = DateTime.UtcNow;
        var blobs = await RemoveRemainingStagedFilesAsync(requestId, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return blobs;
    }

    private async Task<IReadOnlyList<StagedFileObjectDeleteTarget>> RemoveRemainingStagedFilesAsync(
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
        var blobs = filesToDelete
            .Where(file => !string.IsNullOrWhiteSpace(file.BlobKey) &&
                           !string.IsNullOrWhiteSpace(file.BlobContainer))
            .Select(file => new StagedFileObjectDeleteTarget(file.BlobKey!, file.BlobContainer!))
            .ToList();
        _db.Files.RemoveRange(filesToDelete);
        return blobs;
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
        join caveValue in _db.Caves.AsNoTracking() on new { request.AccountId, Id = request.CaveId }
            equals new { caveValue.AccountId, caveValue.Id } into caves
        from cave in caves.DefaultIfEmpty()
        join proposalBaseRevision in _db.CaveRevisions.AsNoTracking()
            on new { proposal.AccountId, proposal.CaveId, Id = proposal.BaseRevisionId }
            equals new { proposalBaseRevision.AccountId, proposalBaseRevision.CaveId, Id = proposalBaseRevision.Id }
        join currentRevisionValue in _db.CaveRevisions.AsNoTracking()
            on new { request.AccountId, Id = cave == null ? null : cave.CurrentRevisionId }
            equals new { currentRevisionValue.AccountId, Id = (string?)currentRevisionValue.Id } into currentRevisions
        from currentRevision in currentRevisions.DefaultIfEmpty()
        join submitterValue in _db.Users.AsNoTracking() on request.CreatedByUserId equals submitterValue.Id into submitters
        from submitter in submitters.DefaultIfEmpty()
        join reviewerValue in _db.Users.AsNoTracking() on request.ReviewerUserId equals reviewerValue.Id into reviewers
        from reviewer in reviewers.DefaultIfEmpty()
        where request.AccountId == _scope.AccountId &&
              cave != null && _db.UserCavePermissionView.Any(permission =>
                  permission.AccountId == _scope.AccountId && permission.UserId == _user.Id &&
                  permission.CaveId == request.CaveId) &&
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
            CurrentCanReview = _db.UserCavePermissionView.Any(permission => permission.AccountId == _scope.AccountId &&
                permission.UserId == _user.Id && permission.CaveId == request.CaveId &&
                permission.PermissionKey == PermissionPolicyKey.Manager)
        };

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

    private static CaveProposalVersion NewVersion(CaveChangeRequest request, string baseRevisionId, string? previousId,
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
