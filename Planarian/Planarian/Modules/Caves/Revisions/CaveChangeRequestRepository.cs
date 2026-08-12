using Microsoft.EntityFrameworkCore;
using Planarian.Library.Exceptions;
using Planarian.Model.Database;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.Revisions;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Helpers;

namespace Planarian.Modules.Caves.Revisions;

public sealed record CaveChangeRequestReadRow(
    CaveChangeRequest Request,
    CaveProposalVersion ProposalVersion,
    CaveRevision ProposalBaseRevision,
    CaveRevision? CurrentRevision,
    string? LiveCaveName,
    bool CaveExists,
    string? SubmitterName,
    string? ReviewerName);

public sealed record CaveProposalVersionReadRow(CaveProposalVersion Version, string? ActorName);
public sealed record CaveProposalVersionDetailReadRow(CaveProposalVersion Version, CaveRevision BaseRevision);

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

    public async Task StageFileAsync(string requestId, string fileId, string fileTypeTagId, string? displayName,
        bool reviewer, CancellationToken cancellationToken)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var request = await LockedAsync(requestId, cancellationToken);
        if (request.Status != CaveChangeRequestStatus.Pending)
            throw ApiExceptionDictionary.BadRequest("Only pending requests can receive files.");
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

    public async Task<List<CaveChangeRequestReadRow>> ListMineAsync(CancellationToken cancellationToken) =>
        (await Query(createdByUserId: _user.Id).ToListAsync(cancellationToken))
        .OrderByDescending(row => row.Request.CreatedOn).ToList();

    public async Task<List<CaveChangeRequestReadRow>> ListForReviewAsync(CancellationToken cancellationToken) =>
        (await Query(status: CaveChangeRequestStatus.Pending).ToListAsync(cancellationToken))
        .OrderBy(row => row.Request.CreatedOn).ToList();

    public Task<CaveChangeRequestReadRow?> GetAsync(string requestId, CancellationToken cancellationToken) =>
        Query(requestId: requestId).SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<CaveProposalVersionReadRow>> ListVersionsAsync(string requestId,
        CancellationToken cancellationToken) =>
        await (from version in _db.CaveProposalVersions.AsNoTracking()
                join actor in _db.Users.AsNoTracking() on version.CreatedByUserId equals actor.Id into actors
                from actor in actors.DefaultIfEmpty()
                where version.AccountId == _scope.AccountId && version.ChangeRequestId == requestId
                orderby version.CreatedOn
                select new CaveProposalVersionReadRow(version,
                    actor == null ? null : actor.FirstName + " " + actor.LastName))
            .ToListAsync(cancellationToken);

    public Task<CaveProposalVersionDetailReadRow?> GetVersionAsync(string requestId, string versionId,
        CancellationToken cancellationToken) =>
        (from version in _db.CaveProposalVersions.AsNoTracking()
            join revision in _db.CaveRevisions.AsNoTracking()
                on new { version.AccountId, version.CaveId, Id = version.BaseRevisionId }
                equals new { revision.AccountId, revision.CaveId, revision.Id }
            where version.AccountId == _scope.AccountId && version.ChangeRequestId == requestId &&
                  version.Id == versionId
            select new CaveProposalVersionDetailReadRow(version, revision))
        .SingleOrDefaultAsync(cancellationToken);

    public async Task<Dictionary<string, string>> GetTagNamesAsync(IEnumerable<string> ids,
        CancellationToken cancellationToken)
    {
        var distinct = ids.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList();
        return await _db.TagTypes.AsNoTracking().Where(tag => distinct.Contains(tag.Id))
            .ToDictionaryAsync(tag => tag.Id, tag => tag.Name, cancellationToken);
    }

    public async Task<(string StateName, string? StateAbbreviation, string CountyName, string CountyDisplayId)>
        GetLocationLabelsAsync(string stateId, string countyId, CancellationToken cancellationToken)
    {
        var state = await _db.States.AsNoTracking().Where(row => row.Id == stateId)
            .Select(row => new { row.Name, row.Abbreviation }).SingleAsync(cancellationToken);
        var county = await _db.Counties.AsNoTracking()
            .Where(row => row.AccountId == _scope.AccountId && row.Id == countyId)
            .Select(row => new { row.Name, row.DisplayId }).SingleAsync(cancellationToken);
        return (state.Name, state.Abbreviation, county.Name, county.DisplayId);
    }

    public async Task RejectAsync(string requestId, string expectedProposalVersionId, string? notes,
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
        await _db.CaveChangeRequestStagedFiles
            .Where(staged => staged.AccountId == _scope.AccountId && staged.ChangeRequestId == requestId)
            .ExecuteDeleteAsync(cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task MarkApprovedAsync(string requestId, string expectedProposalVersionId,
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
        await _db.CaveChangeRequestStagedFiles
            .Where(staged => staged.AccountId == _scope.AccountId && staged.ChangeRequestId == requestId)
            .ExecuteDeleteAsync(cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
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
              (requestId == null || request.Id == requestId) &&
              (createdByUserId == null || request.CreatedByUserId == createdByUserId) &&
              (status == null || request.Status == status)
        select new CaveChangeRequestReadRow(request, proposal, proposalBaseRevision, currentRevision,
            cave == null ? null : cave.Name, cave != null,
            submitter == null ? null : submitter.FirstName + " " + submitter.LastName,
            reviewer == null ? null : reviewer.FirstName + " " + reviewer.LastName);

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
