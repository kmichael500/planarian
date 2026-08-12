using Planarian.Library.Exceptions;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.Revisions;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Helpers;
using Planarian.Modules.Caves.Models;
using Planarian.Modules.Caves.Repositories;
using Planarian.Modules.Caves.Revisions;
using Planarian.Modules.Files.Controllers;
using Planarian.Modules.Files.Services;

namespace Planarian.Modules.Caves.Services;

public sealed class CaveChangeRequestService
{
    private readonly CaveChangeRequestRepository _requests;
    private readonly CaveRepository _caves;
    private readonly CaveService _caveService;
    private readonly CaveRevisionQueryRepository _revisions;
    private readonly CaveMutationCoordinator _mutations;
    private readonly FileService _files;
    private readonly RequestUser _user;
    private readonly CaveRevisionDiffService _diff = new();

    public CaveChangeRequestService(CaveChangeRequestRepository requests, CaveRepository caves,
        CaveService caveService, CaveRevisionQueryRepository revisions, CaveMutationCoordinator mutations,
        FileService files, RequestUser user)
    {
        _requests = requests;
        _caves = caves;
        _caveService = caveService;
        _revisions = revisions;
        _mutations = mutations;
        _files = files;
        _user = user;
    }

    public async Task<CaveProposalAuthoringContextVm> GetAuthoringContextAsync(string caveId,
        CancellationToken cancellationToken)
    {
        await _mutations.EnsureBaselineAsync(caveId, cancellationToken);
        var cave = await _caves.GetCave(caveId) ?? throw ApiExceptionDictionary.NotFound("Cave");
        return new CaveProposalAuthoringContextVm(cave,
            cave.CurrentRevisionId ?? throw new InvalidOperationException("Revision baseline initialization failed."));
    }

    public async Task<CaveChangePreviewVm> PreviewAsync(string caveId, AddCaveVm values,
        string expectedBaseRevisionId,
        CancellationToken cancellationToken)
    {
        if (await _caves.GetCave(caveId) is null) throw ApiExceptionDictionary.NotFound("Cave");
        await RequireCurrentRevisionAsync(caveId, expectedBaseRevisionId, cancellationToken);
        var revision = await _revisions.GetAsync(caveId, expectedBaseRevisionId, cancellationToken)
                       ?? throw ApiExceptionDictionary.NotFound("Cave revision");
        var current = Deserialize(revision.Revision);
        var proposal = await BuildProposalAsync(values, caveId, cancellationToken, expectedBaseRevisionId);
        var proposed = await PresentAsync(proposal, current,
            cancellationToken);
        await RequireCurrentRevisionAsync(caveId, expectedBaseRevisionId, cancellationToken);
        return new CaveChangePreviewVm(current, proposed, Map(_diff.Compare(current, proposed)),
            proposal.CountyNumberIntent, proposal.RequestedCountyNumber);
    }

    public async Task<CaveChangePreviewVm> PreviewVersionAsync(string requestId, AddCaveVm values,
        bool againstCurrent, string expectedBaseRevisionId, string expectedProposalVersionId,
        CancellationToken cancellationToken)
    {
        var row = await RequireReadableAsync(requestId, cancellationToken);
        if (values.Id != row.Request.CaveId)
            throw ApiExceptionDictionary.BadRequest("The proposed Cave does not match the request.");
        var canReview = await CanReviewAsync(row, false);
        if (row.Request.CreatedByUserId != _user.Id && !canReview)
            throw ApiExceptionDictionary.Forbidden("You cannot revise this request.");
        RequireExpectedEditorState(row, expectedBaseRevisionId, expectedProposalVersionId, againstCurrent);
        var baseRevision = againstCurrent ? row.CurrentRevision : row.ProposalBaseRevision;
        if (baseRevision is null) throw ApiExceptionDictionary.NotFound("Cave revision");
        if (row.CurrentRevision?.Id != baseRevision.Id)
            throw new CaveRevisionConflictException(row.Request.CaveId, baseRevision.Id, row.CurrentRevision?.Id);
        var baseSnapshot = Deserialize(baseRevision);
        var proposal = await BuildVersionProposalAsync(row, values, baseRevision.Id, cancellationToken);
        var proposed = await PresentAsync(proposal, baseSnapshot, cancellationToken);
        var latest = await RequireReadableAsync(requestId, cancellationToken);
        RequireExpectedEditorState(latest, expectedBaseRevisionId, expectedProposalVersionId, againstCurrent);
        return new CaveChangePreviewVm(baseSnapshot, proposed, Map(_diff.Compare(baseSnapshot, proposed)),
            proposal.CountyNumberIntent, proposal.RequestedCountyNumber);
    }

    public async Task<string> CreateAsync(string caveId, AddCaveVm values, string expectedBaseRevisionId,
        CancellationToken cancellationToken)
    {
        if (values.Id != caveId) throw ApiExceptionDictionary.BadRequest("The proposed Cave does not match the route.");
        return await _requests.CreateAsync(caveId, expectedBaseRevisionId,
            token => BuildValidatedProposalAsync(values, caveId, expectedBaseRevisionId, null, token),
            cancellationToken);
    }

    public async Task<string> AddVersionAsync(string requestId, AddCaveVm values, bool againstCurrent,
        string expectedBaseRevisionId, string expectedProposalVersionId, CancellationToken cancellationToken)
    {
        var row = await RequireReadableAsync(requestId, cancellationToken);
        if (values.Id != row.Request.CaveId)
            throw ApiExceptionDictionary.BadRequest("The proposed Cave does not match the request.");
        var reviewer = await CanReviewAsync(row, false);
        RequireExpectedEditorState(row, expectedBaseRevisionId, expectedProposalVersionId, againstCurrent);
        var stagedIds = await _requests.GetStagedFileIdsAsync(row.Request.Id, cancellationToken);
        var proposal = await BuildValidatedProposalAsync(values, row.Request.CaveId, expectedBaseRevisionId,
            stagedIds, cancellationToken);
        return await _requests.AddVersionAsync(requestId, expectedBaseRevisionId, expectedProposalVersionId,
            proposal, reviewer, againstCurrent, cancellationToken);
    }

    public async Task<FileVm> StageFileAsync(string requestId, Stream stream, string fileName, string? uuid,
        CancellationToken cancellationToken)
    {
        var row = await RequireReadableAsync(requestId, cancellationToken);
        var reviewer = await CanReviewAsync(row, false);
        if (row.Request.CreatedByUserId != _user.Id && !reviewer)
            throw ApiExceptionDictionary.Forbidden("You cannot add files to this proposal.");
        if (row.Request.Status != CaveChangeRequestStatus.Pending)
            throw ApiExceptionDictionary.BadRequest("Only pending requests can receive files.");

        var file = await _files.StageCaveChangeRequestFile(stream, fileName, cancellationToken, uuid);
        try
        {
            await _requests.StageFileAsync(requestId, file.Id, file.FileTypeTagId, file.DisplayName, reviewer,
                cancellationToken);
            return file;
        }
        catch
        {
            await _files.DeleteUnpublishedFileAsync(file.Id, CancellationToken.None);
            throw;
        }
    }

    public async Task<(Stream Stream, string FileName)> OpenStagedFileAsync(string requestId, string fileId,
        CancellationToken cancellationToken)
    {
        await RequireReadableAsync(requestId, cancellationToken);
        if (!await _requests.IsStagedFileAsync(requestId, fileId, cancellationToken))
            throw ApiExceptionDictionary.NotFound("Staged file");
        return await _files.OpenUnpublishedFileAsync(fileId, cancellationToken);
    }

    public async Task<IReadOnlyList<CaveChangeRequestSummaryVm>> ListMineAsync(CancellationToken cancellationToken)
    {
        var result = new List<CaveChangeRequestSummaryVm>();
        foreach (var row in await _requests.ListMineAsync(cancellationToken))
            result.Add(MapSummary(row, await CanReviewAsync(row, false)));
        return result;
    }

    public async Task<IReadOnlyList<CaveChangeRequestSummaryVm>> ListForReviewAsync(CancellationToken cancellationToken)
    {
        var rows = await _requests.ListForReviewAsync(cancellationToken);
        var visible = new List<CaveChangeRequestSummaryVm>();
        foreach (var row in rows)
            if (await CanReviewAsync(row, false)) visible.Add(MapSummary(row, true));
        return visible;
    }

    public async Task<CaveChangeRequestDetailVm> GetAsync(string requestId, CancellationToken cancellationToken)
    {
        var row = await RequireReadableAsync(requestId, cancellationToken);
        var canReview = await CanReviewAsync(row, false);
        var baseSnapshot = Deserialize(row.ProposalBaseRevision);
        var currentSnapshot = row.CurrentRevision is null ? baseSnapshot : Deserialize(row.CurrentRevision);
        var proposal = CaveProposalJson.Deserialize(row.ProposalVersion.ProposalJson, row.ProposalVersion.SchemaVersion);
        var proposedSnapshot = await PresentAsync(proposal, baseSnapshot, cancellationToken);
        var isStale = IsStale(row);
        var activeStagedFiles = proposedSnapshot.Files.Where(file => proposal.Files.Any(intent =>
            intent.FileId == file.Id && intent.Disposition == ProposalFileDisposition.PublishStaged)).ToList();
        var versions = await _requests.ListVersionsAsync(requestId, cancellationToken);
        return new CaveChangeRequestDetailVm(MapSummary(row, canReview), baseSnapshot, proposedSnapshot, currentSnapshot,
            Map(_diff.Compare(baseSnapshot, proposedSnapshot)),
            isStale ? Map(_diff.Compare(baseSnapshot, currentSnapshot)) : null,
            versions.Select(version => new CaveProposalVersionVm(version.Version.Id,
                version.Version.PreviousProposalVersionId, version.Version.BaseRevisionId,
                version.Version.CreatedByUserId, version.ActorName,
                version.Version.CreatedOn, version.Version.Id == row.Request.CurrentProposalVersionId)).ToList(),
            proposal.CountyNumberIntent, proposal.RequestedCountyNumber, activeStagedFiles);
    }

    public async Task<CaveProposalVersionDetailVm> GetVersionAsync(string requestId, string versionId,
        CancellationToken cancellationToken)
    {
        await RequireReadableAsync(requestId, cancellationToken);
        var row = await _requests.GetVersionAsync(requestId, versionId, cancellationToken)
                  ?? throw ApiExceptionDictionary.NotFound("Proposal version");
        var baseSnapshot = Deserialize(row.BaseRevision);
        var proposal = CaveProposalJson.Deserialize(row.Version.ProposalJson, row.Version.SchemaVersion);
        var presentation = await PresentAsync(proposal, baseSnapshot, cancellationToken,
            preserveUnavailableStagedFiles: true);
        CavePublishedSnapshotV1? previousProposed = null;
        CaveRevisionDiffVm? diffFromPreviousVersion = null;
        CaveProposalCountyNumberChangeVm? countyNumberChange = null;
        var unavailableStagedFileIds = presentation.UnavailableStagedFileIds.ToHashSet(StringComparer.Ordinal);
        if (row.PreviousVersion is not null && row.PreviousBaseRevision is not null)
        {
            var previousBase = Deserialize(row.PreviousBaseRevision);
            var previousProposal = CaveProposalJson.Deserialize(row.PreviousVersion.ProposalJson,
                row.PreviousVersion.SchemaVersion);
            var previousPresentation = await PresentAsync(previousProposal, previousBase, cancellationToken,
                preserveUnavailableStagedFiles: true);
            previousProposed = previousPresentation.Snapshot;
            diffFromPreviousVersion = Map(_diff.Compare(previousProposed, presentation.Snapshot));
            var previousCountyNumber = CaveProposalCountyNumberPresentation.Resolve(previousProposal, previousBase);
            var currentCountyNumber = CaveProposalCountyNumberPresentation.Resolve(proposal, baseSnapshot);
            if (previousCountyNumber != currentCountyNumber)
                countyNumberChange = new CaveProposalCountyNumberChangeVm(previousCountyNumber,
                    currentCountyNumber);
            unavailableStagedFileIds.UnionWith(previousPresentation.UnavailableStagedFileIds);
        }
        return new CaveProposalVersionDetailVm(baseSnapshot, presentation.Snapshot,
            Map(_diff.Compare(baseSnapshot, presentation.Snapshot)), previousProposed, diffFromPreviousVersion,
            countyNumberChange,
            row.PreviousVersion is not null && row.PreviousVersion.BaseRevisionId != row.Version.BaseRevisionId,
            row.PreviousVersion?.BaseRevisionId, row.Version.BaseRevisionId, proposal.CountyNumberIntent,
            proposal.RequestedCountyNumber, unavailableStagedFileIds.ToList());
    }

    public async Task<CaveChangeRequestDecisionVm> ApproveAsync(string requestId,
        string expectedProposalVersionId, string? notes, CancellationToken cancellationToken)
    {
        var row = await _requests.GetAsync(requestId, cancellationToken)
                  ?? throw ApiExceptionDictionary.NotFound("Change request");
        await CanReviewAsync(row, true);
        if (row.Request.Status != CaveChangeRequestStatus.Pending)
            throw ApiExceptionDictionary.BadRequest("This request has already been reviewed.");
        if (row.Request.CurrentProposalVersionId != expectedProposalVersionId)
            throw new CaveProposalVersionConflictException(expectedProposalVersionId,
                row.Request.CurrentProposalVersionId);
        if (row.CurrentRevision?.Id != row.ProposalVersion.BaseRevisionId)
            throw new CaveRevisionConflictException(row.Request.CaveId, row.ProposalVersion.BaseRevisionId,
                row.CurrentRevision?.Id);

        var proposal = CaveProposalJson.Deserialize(row.ProposalVersion.ProposalJson, row.ProposalVersion.SchemaVersion);
        var values = ToAddCave(proposal, Deserialize(row.ProposalBaseRevision));
        var stagedFileIds = proposal.Files.Where(file => file.Disposition == ProposalFileDisposition.PublishStaged)
            .Select(file => file.FileId).ToList();
        await _caveService.ApproveChangeRequestAsync(values, row.ProposalVersion.BaseRevisionId, requestId,
            stagedFileIds,
            (mutation, token) => _requests.MarkApprovedAsync(requestId, expectedProposalVersionId,
                mutation, notes, token), cancellationToken);
        var approved = await _requests.GetAsync(requestId, cancellationToken);
        return new CaveChangeRequestDecisionVm(CaveChangeRequestDecisionResult.Approved, requestId,
            approved?.Request.ApprovedRevisionId, approved?.CurrentRevision?.Id);
    }

    public async Task<CaveChangeRequestDecisionVm> RejectAsync(string requestId,
        string expectedProposalVersionId, string? notes, CancellationToken cancellationToken)
    {
        var row = await _requests.GetAsync(requestId, cancellationToken)
                  ?? throw ApiExceptionDictionary.NotFound("Change request");
        await CanReviewAsync(row, true);
        if (row.Request.CurrentProposalVersionId != expectedProposalVersionId)
            throw new CaveProposalVersionConflictException(expectedProposalVersionId,
                row.Request.CurrentProposalVersionId);
        await _requests.RejectAsync(requestId, expectedProposalVersionId, notes, cancellationToken);
        return new CaveChangeRequestDecisionVm(CaveChangeRequestDecisionResult.Rejected, requestId, null,
            row.CurrentRevision?.Id);
    }

    private async Task<CaveChangeRequestReadRow> RequireReadableAsync(string requestId,
        CancellationToken cancellationToken)
    {
        var row = await _requests.GetAsync(requestId, cancellationToken)
                  ?? throw ApiExceptionDictionary.NotFound("Change request");
        if (row.Request.CreatedByUserId != _user.Id && !await CanReviewAsync(row, false))
            throw ApiExceptionDictionary.NotFound("Change request");
        return row;
    }

    private Task<bool> CanReviewAsync(CaveChangeRequestReadRow row, bool throwIfDenied) =>
        _user.HasCavePermission(PermissionPolicyKey.Manager, row.Request.CaveId,
            row.Request.BaseCountyId, row.Request.BaseStateId, throwIfDenied);

    private async Task<string> GetCurrentRevisionIdAsync(string caveId, CancellationToken cancellationToken)
    {
        return await _requests.GetCurrentRevisionIdAsync(caveId, cancellationToken)
               ?? throw ApiExceptionDictionary.BadRequest("The Cave has no published revision.");
    }

    private async Task RequireCurrentRevisionAsync(string caveId, string expectedRevisionId,
        CancellationToken cancellationToken)
    {
        var actual = await GetCurrentRevisionIdAsync(caveId, cancellationToken);
        if (actual != expectedRevisionId)
            throw new CaveRevisionConflictException(caveId, expectedRevisionId, actual);
    }

    private static void RequireExpectedEditorState(CaveChangeRequestReadRow row, string expectedBaseRevisionId,
        string expectedProposalVersionId, bool againstCurrent)
    {
        if (row.Request.Status != CaveChangeRequestStatus.Pending)
            throw ApiExceptionDictionary.BadRequest("Only pending requests can be revised.");
        if (row.Request.CurrentProposalVersionId != expectedProposalVersionId)
            throw new CaveProposalVersionConflictException(expectedProposalVersionId,
                row.Request.CurrentProposalVersionId);
        if (!againstCurrent && row.ProposalVersion.BaseRevisionId != expectedBaseRevisionId)
            throw new CaveRevisionConflictException(row.Request.CaveId, expectedBaseRevisionId,
                row.ProposalVersion.BaseRevisionId);
        if (row.CurrentRevision?.Id != expectedBaseRevisionId)
            throw new CaveRevisionConflictException(row.Request.CaveId, expectedBaseRevisionId,
                row.CurrentRevision?.Id);
    }

    private async Task<CaveProposalSnapshotV1> BuildVersionProposalAsync(CaveChangeRequestReadRow row,
        AddCaveVm values, string baseRevisionId, CancellationToken cancellationToken)
    {
        var stagedIds = await _requests.GetStagedFileIdsAsync(row.Request.Id, cancellationToken);
        return await BuildProposalAsync(values, row.Request.CaveId, cancellationToken, baseRevisionId, stagedIds);
    }

    private async Task<CaveProposalSnapshotV1> BuildValidatedProposalAsync(AddCaveVm values, string caveId,
        string baseRevisionId, IReadOnlySet<string>? stagedFileIds, CancellationToken cancellationToken)
    {
        var proposal = await BuildProposalAsync(values, caveId, cancellationToken, baseRevisionId, stagedFileIds);
        var revision = await _revisions.GetAsync(caveId, baseRevisionId, cancellationToken)
                       ?? throw ApiExceptionDictionary.NotFound("Cave revision");
        var baseSnapshot = Deserialize(revision.Revision);
        var proposedSnapshot = await PresentAsync(proposal, baseSnapshot, cancellationToken);
        if (_diff.IsSemanticEqual(baseSnapshot, proposedSnapshot))
            throw ApiExceptionDictionary.BadRequest("The proposal does not contain any changes.");
        return proposal;
    }

    private async Task<CaveProposalSnapshotV1> BuildProposalAsync(AddCaveVm values, string caveId,
        CancellationToken cancellationToken, string? sourceRevisionId = null,
        IReadOnlySet<string>? stagedFileIds = null)
    {
        var currentRevisionId = sourceRevisionId ?? await GetCurrentRevisionIdAsync(caveId, cancellationToken);
        var currentRevision = await _revisions.GetAsync(caveId, currentRevisionId, cancellationToken)
                              ?? throw ApiExceptionDictionary.NotFound("Cave revision");
        var current = Deserialize(currentRevision.Revision);
        var currentEntrances = current.Entrances.ToDictionary(entrance => entrance.Id);
        var ids = CaveTagGroups(values).SelectMany(group => group.Ids)
            .Concat(values.Entrances.SelectMany(EntranceTagGroups).SelectMany(group => group.Ids))
            .Concat(values.Entrances.Select(entrance => entrance.LocationQualityTagId))
            .Concat((values.Files ?? []).Select(file => file.FileTypeTagId));
        var names = await _requests.GetTagNamesAsync(ids, cancellationToken);
        var locationLabels = await _requests.GetLocationLabelsAsync(values.StateId, values.CountyId,
            cancellationToken);
        string Name(string id) => names.GetValueOrDefault(id, id);

        var selectedFiles = (values.Files ?? []).ToDictionary(file => file.Id, StringComparer.Ordinal);
        var baseFileIds = current.Files.Select(file => file.Id).ToHashSet(StringComparer.Ordinal);
        var stagedSnapshots = await _requests.GetFileSnapshotsAsync(selectedFiles.Keys
            .Where(fileId => !baseFileIds.Contains(fileId)), cancellationToken);
        var fileIntents = current.Files.Select(file => selectedFiles.TryGetValue(file.Id, out var selected)
                ? new ProposalFileIntent(file.Id, ProposalFileDisposition.RetainPublished,
                    selected.FileTypeTagId, selected.DisplayName,
                    CaveFileNamePolicy.GetEffectiveFileName(file.FileName, file.DisplayName, selected.DisplayName),
                    Name(selected.FileTypeTagId))
                : new ProposalFileIntent(file.Id, ProposalFileDisposition.RemovePublished, null, null))
            .ToList();
        foreach (var selected in selectedFiles.Values.Where(file => !baseFileIds.Contains(file.Id)))
        {
            if (stagedFileIds?.Contains(selected.Id) != true)
                throw ApiExceptionDictionary.BadRequest("The proposal contains a file that is neither published nor staged for this request.");
            if (!stagedSnapshots.TryGetValue(selected.Id, out var staged))
                throw ApiExceptionDictionary.NotFound("Staged file");
            fileIntents.Add(new ProposalFileIntent(selected.Id, ProposalFileDisposition.PublishStaged,
                selected.FileTypeTagId, selected.DisplayName,
                CaveFileNamePolicy.GetEffectiveFileName(staged.FileName, staged.DisplayName, selected.DisplayName),
                Name(selected.FileTypeTagId)));
        }

        return new CaveProposalSnapshotV1
        {
            CaveId = caveId,
            AccountId = _user.AccountId!,
            Name = values.Name.Trim(),
            AlternateNames = values.AlternateNames.Select(name => name.Trim()).ToList(),
            StateId = values.StateId,
            StateNameAtRevision = locationLabels.StateName,
            StateAbbreviationAtRevision = locationLabels.StateAbbreviation,
            CountyId = values.CountyId,
            CountyNameAtRevision = locationLabels.CountyName,
            CountyDisplayIdAtRevision = locationLabels.CountyDisplayId,
            CountyNumberIntent = values.IsCountyNumberManuallySet ? CountyNumberIntent.Manual
                : values.UseFirstAvailableCountyNumber ? CountyNumberIntent.FirstAvailable : CountyNumberIntent.AutomaticNext,
            RequestedCountyNumber = values.IsCountyNumberManuallySet ? values.CountyNumber : null,
            LengthFeet = values.LengthFeet,
            DepthFeet = values.DepthFeet,
            MaxPitDepthFeet = values.MaxPitDepthFeet,
            NumberOfPits = values.NumberOfPits,
            Narrative = values.Narrative?.Trim(),
            ReportedOn = values.ReportedOn,
            IsArchived = current.IsArchived,
            Tags = CaveTagGroups(values).SelectMany(group => group.Ids.Select(id =>
                new SnapshotTagReference(group.Role, id, Name(id)))).ToList(),
            Entrances = values.Entrances.Select(entrance => new CaveProposalEntranceV1
            {
                EntranceId = string.IsNullOrWhiteSpace(entrance.Id) ? IdGenerator.Generate() : entrance.Id,
                Name = entrance.Name,
                IsPrimary = entrance.IsPrimary,
                Description = entrance.Description,
                ReportedByUserId = !string.IsNullOrWhiteSpace(entrance.Id) &&
                                   currentEntrances.TryGetValue(entrance.Id, out var currentEntrance)
                    ? currentEntrance.ReportedByUserId : null,
                Latitude = entrance.Latitude,
                Longitude = entrance.Longitude,
                Elevation = entrance.ElevationFeet,
                LocationQualityTagId = entrance.LocationQualityTagId,
                LocationQualityNameAtRevision = Name(entrance.LocationQualityTagId),
                ReportedOn = entrance.ReportedOn,
                PitDepthFeet = entrance.PitFeet,
                Tags = EntranceTagGroups(entrance).SelectMany(group => group.Ids.Select(id =>
                    new SnapshotTagReference(group.Role, id, Name(id)))).ToList()
            }).ToList(),
            Files = fileIntents
        };
    }

    private sealed record ProposalPresentation(CavePublishedSnapshotV1 Snapshot,
        IReadOnlyList<string> UnavailableStagedFileIds);

    private async Task<CavePublishedSnapshotV1> PresentAsync(CaveProposalSnapshotV1 proposal,
        CavePublishedSnapshotV1 @base, CancellationToken cancellationToken) =>
        (await PresentAsync(proposal, @base, cancellationToken, preserveUnavailableStagedFiles: false)).Snapshot;

    private async Task<ProposalPresentation> PresentAsync(CaveProposalSnapshotV1 proposal,
        CavePublishedSnapshotV1 @base, CancellationToken cancellationToken, bool preserveUnavailableStagedFiles)
    {
        var needsLocationFallback = proposal.StateNameAtRevision is null ||
                                    proposal.CountyNameAtRevision is null;
        var labels = needsLocationFallback
            ? await _requests.GetLocationLabelsAsync(proposal.StateId, proposal.CountyId, cancellationToken)
            : (proposal.StateNameAtRevision!, proposal.StateAbbreviationAtRevision,
                proposal.CountyNameAtRevision!, proposal.CountyDisplayIdAtRevision ?? proposal.CountyId);
        var missingLocationQualityIds = proposal.Entrances
            .Where(entrance => entrance.LocationQualityNameAtRevision is null)
            .Select(entrance => entrance.LocationQualityTagId);
        var tagNames = await _requests.GetTagNamesAsync(missingLocationQualityIds, cancellationToken);
        var baseFiles = @base.Files.ToDictionary(file => file.Id);
        var baseEntrances = @base.Entrances.ToDictionary(entrance => entrance.Id);
        var stagedFiles = await _requests.GetFileSnapshotsAsync(proposal.Files
            .Where(file => file.Disposition == ProposalFileDisposition.PublishStaged)
            .Select(file => file.FileId), cancellationToken);
        var unavailableStagedFileIds = proposal.Files
            .Where(file => file.Disposition == ProposalFileDisposition.PublishStaged &&
                           !stagedFiles.ContainsKey(file.FileId))
            .Select(file => file.FileId).ToList();
        var snapshot = new CavePublishedSnapshotV1
        {
            CaveId = proposal.CaveId,
            AccountId = proposal.AccountId,
            Name = proposal.Name,
            AlternateNames = proposal.AlternateNames,
            State = new SnapshotReference(proposal.StateId, labels.Item1, null, labels.Item2),
            County = new SnapshotReference(proposal.CountyId, labels.Item3, labels.Item4),
            CountyNumber = proposal.RequestedCountyNumber ?? (@base.County.Id == proposal.CountyId ? @base.CountyNumber : 0),
            ReportedByUserId = @base.ReportedByUserId,
            LengthFeet = proposal.LengthFeet,
            DepthFeet = proposal.DepthFeet,
            MaxPitDepthFeet = proposal.MaxPitDepthFeet,
            NumberOfPits = proposal.NumberOfPits,
            Narrative = proposal.Narrative,
            ReportedOn = proposal.ReportedOn,
            IsArchived = proposal.IsArchived,
            Tags = proposal.Tags,
            Entrances = proposal.Entrances.Select(entrance => new CaveEntranceSnapshotV1
            {
                Id = entrance.EntranceId, Name = entrance.Name, IsPrimary = entrance.IsPrimary,
                Description = entrance.Description, ReportedByUserId = entrance.ReportedByUserId,
                ReportedByNameAtRevision = baseEntrances.GetValueOrDefault(entrance.EntranceId)?.ReportedByNameAtRevision,
                Latitude = entrance.Latitude, Longitude = entrance.Longitude,
                Elevation = entrance.Elevation, Srid = entrance.Srid,
                LocationQualityTagId = entrance.LocationQualityTagId,
                LocationQualityNameAtRevision = entrance.LocationQualityNameAtRevision ??
                    tagNames.GetValueOrDefault(entrance.LocationQualityTagId, entrance.LocationQualityTagId),
                ReportedOn = entrance.ReportedOn,
                PitDepthFeet = entrance.PitDepthFeet, Tags = entrance.Tags
            }).ToList(),
            Files = proposal.Files.Select(file => file.Disposition switch
                {
                    ProposalFileDisposition.RetainPublished when baseFiles.TryGetValue(file.FileId, out var published) =>
                        published with
                        {
                            FileTypeTagId = file.FileTypeTagId ?? published.FileTypeTagId,
                            FileTypeNameAtRevision = file.FileTypeName ?? published.FileTypeNameAtRevision,
                            FileName = file.FileName ?? published.FileName,
                            DisplayName = file.DisplayName ?? published.DisplayName
                        },
                    ProposalFileDisposition.PublishStaged when stagedFiles.TryGetValue(file.FileId, out var staged) =>
                        staged with
                        {
                            FileTypeTagId = file.FileTypeTagId ?? staged.FileTypeTagId,
                            FileTypeNameAtRevision = file.FileTypeName ?? staged.FileTypeNameAtRevision,
                            FileName = file.FileName ?? staged.FileName,
                            DisplayName = file.DisplayName ?? staged.DisplayName
                        },
                    ProposalFileDisposition.PublishStaged when preserveUnavailableStagedFiles =>
                        new CaveFileSnapshotV1
                        {
                            Id = file.FileId,
                            FileTypeTagId = file.FileTypeTagId ?? "unavailable",
                            FileTypeNameAtRevision = file.FileTypeName ?? "Unavailable staged file",
                            FileName = file.FileName ?? file.DisplayName ?? file.FileId,
                            DisplayName = file.DisplayName
                        },
                    _ => null
                }).Where(file => file is not null).Cast<CaveFileSnapshotV1>().ToList()
        };
        return new ProposalPresentation(snapshot, unavailableStagedFileIds);
    }

    private static AddCaveVm ToAddCave(CaveProposalSnapshotV1 proposal, CavePublishedSnapshotV1 proposalBase)
    {
        var publishedEntranceIds = proposalBase.Entrances.Select(entrance => entrance.Id)
            .ToHashSet(StringComparer.Ordinal);
        return new AddCaveVm
    {
        Id = proposal.CaveId, Name = proposal.Name, AlternateNames = proposal.AlternateNames,
        StateId = proposal.StateId, CountyId = proposal.CountyId,
        IsCountyNumberManuallySet = proposal.CountyNumberIntent == CountyNumberIntent.Manual,
        UseFirstAvailableCountyNumber = proposal.CountyNumberIntent == CountyNumberIntent.FirstAvailable,
        CountyNumber = proposal.RequestedCountyNumber, LengthFeet = proposal.LengthFeet,
        DepthFeet = proposal.DepthFeet, MaxPitDepthFeet = proposal.MaxPitDepthFeet,
        NumberOfPits = proposal.NumberOfPits, Narrative = proposal.Narrative, ReportedOn = proposal.ReportedOn,
        GeologyTagIds = Tags(proposal, SnapshotTagRole.Geology),
        GeologicAgeTagIds = Tags(proposal, SnapshotTagRole.GeologicAge),
        MapStatusTagIds = Tags(proposal, SnapshotTagRole.MapStatus),
        PhysiographicProvinceTagIds = Tags(proposal, SnapshotTagRole.PhysiographicProvince),
        ArcheologyTagIds = Tags(proposal, SnapshotTagRole.Archeology),
        BiologyTagIds = Tags(proposal, SnapshotTagRole.Biology),
        OtherTagIds = Tags(proposal, SnapshotTagRole.CaveOther),
        CartographerNameTagIds = Tags(proposal, SnapshotTagRole.Cartographer),
        ReportedByNameTagIds = Tags(proposal, SnapshotTagRole.CaveReportedBy),
        Entrances = proposal.Entrances.Select(entrance => new AddEntranceVm
        {
            // Proposal-only IDs keep immutable versions/diffs stable, but the canonical Cave mutation
            // boundary must allocate the persisted ID for an entrance that was not in this version's base.
            Id = publishedEntranceIds.Contains(entrance.EntranceId) ? entrance.EntranceId : null,
            Name = entrance.Name, IsPrimary = entrance.IsPrimary,
            Description = entrance.Description, Latitude = entrance.Latitude ?? 0,
            Longitude = entrance.Longitude ?? 0, ElevationFeet = entrance.Elevation ?? 0,
            LocationQualityTagId = entrance.LocationQualityTagId, ReportedOn = entrance.ReportedOn,
            PitFeet = entrance.PitDepthFeet,
            EntranceStatusTagIds = Tags(entrance, SnapshotTagRole.EntranceStatus),
            EntranceHydrologyTagIds = Tags(entrance, SnapshotTagRole.EntranceHydrology),
            FieldIndicationTagIds = Tags(entrance, SnapshotTagRole.FieldIndication),
            ReportedByNameTagIds = Tags(entrance, SnapshotTagRole.EntranceReportedBy),
            EntranceOtherTagIds = Tags(entrance, SnapshotTagRole.EntranceOther)
        }).ToList(),
        Files = proposal.Files.Where(file => file.Disposition is ProposalFileDisposition.RetainPublished or
                ProposalFileDisposition.PublishStaged)
            .Select(file => new EditFileMetadataVm { Id = file.FileId, FileTypeTagId = file.FileTypeTagId,
                DisplayName = file.DisplayName }).ToList()
    };
    }

    private static IEnumerable<string> Tags(CaveProposalSnapshotV1 proposal, SnapshotTagRole role) =>
        proposal.Tags.Where(tag => tag.Role == role).Select(tag => tag.TagTypeId);
    private static IEnumerable<string> Tags(CaveProposalEntranceV1 proposal, SnapshotTagRole role) =>
        proposal.Tags.Where(tag => tag.Role == role).Select(tag => tag.TagTypeId);

    private static IEnumerable<(SnapshotTagRole Role, IEnumerable<string> Ids)> CaveTagGroups(AddCaveVm cave)
    {
        yield return (SnapshotTagRole.Geology, cave.GeologyTagIds);
        yield return (SnapshotTagRole.GeologicAge, cave.GeologicAgeTagIds);
        yield return (SnapshotTagRole.MapStatus, cave.MapStatusTagIds);
        yield return (SnapshotTagRole.PhysiographicProvince, cave.PhysiographicProvinceTagIds);
        yield return (SnapshotTagRole.Archeology, cave.ArcheologyTagIds);
        yield return (SnapshotTagRole.Biology, cave.BiologyTagIds);
        yield return (SnapshotTagRole.CaveOther, cave.OtherTagIds);
        yield return (SnapshotTagRole.Cartographer, cave.CartographerNameTagIds);
        yield return (SnapshotTagRole.CaveReportedBy, cave.ReportedByNameTagIds);
    }

    private static IEnumerable<(SnapshotTagRole Role, IEnumerable<string> Ids)> EntranceTagGroups(AddEntranceVm entrance)
    {
        yield return (SnapshotTagRole.EntranceStatus, entrance.EntranceStatusTagIds);
        yield return (SnapshotTagRole.EntranceHydrology, entrance.EntranceHydrologyTagIds);
        yield return (SnapshotTagRole.FieldIndication, entrance.FieldIndicationTagIds);
        yield return (SnapshotTagRole.EntranceReportedBy, entrance.ReportedByNameTagIds);
        yield return (SnapshotTagRole.EntranceOther, entrance.EntranceOtherTagIds);
    }

    private static CavePublishedSnapshotV1 Deserialize(CaveRevision revision) =>
        CaveSnapshotJson.Deserialize(revision.SnapshotJson, revision.SnapshotSchemaVersion);

    private CaveChangeRequestSummaryVm MapSummary(CaveChangeRequestReadRow row, bool canReview) => new(
        row.Request.Id, row.Request.CaveId,
        row.LiveCaveName ?? Deserialize(row.ProposalBaseRevision).Name, row.CaveExists, row.Request.Status,
        row.Request.CreatedByUserId!, row.SubmitterName, row.Request.CreatedOn, row.Request.ModifiedOn,
        row.Request.ReviewedOn, row.Request.ReviewerUserId, row.ReviewerName, row.Request.ReviewerNotes,
        row.Request.BaseRevisionId!, row.ProposalVersion.BaseRevisionId, row.CurrentRevision?.Id,
        row.Request.CurrentProposalVersionId!, IsStale(row),
        row.Request.ApprovedRevisionId,
        row.Request.CreatedByUserId == _user.Id && row.Request.Status == CaveChangeRequestStatus.Pending,
        canReview && row.Request.Status == CaveChangeRequestStatus.Pending);

    private static bool IsStale(CaveChangeRequestReadRow row) =>
        row.Request.Status == CaveChangeRequestStatus.Pending &&
        row.ProposalVersion.BaseRevisionId != row.CurrentRevision?.Id;

    private static CaveRevisionDiffVm Map(CaveRevisionDiff diff) => new(
        diff.Scalars.Select(change => new CaveScalarChangeVm(change.Key, change.Value.Previous, change.Value.Current)).ToList(),
        diff.AddedTags, diff.RemovedTags, diff.AddedEntrances, diff.RemovedEntrances, diff.ChangedEntrances,
        diff.AddedFiles, diff.RemovedFiles, diff.ChangedFiles,
        diff.EntranceChanges.Select(change => new CaveEntranceChangeVm(change.EntranceId,
            change.Scalars.Select(pair => new CaveScalarChangeVm(pair.Key, pair.Value.Previous, pair.Value.Current)).ToList(),
            change.AddedTags, change.RemovedTags)).ToList(),
        diff.FileChanges.Select(change => new CaveFileChangeVm(change.FileId,
            change.Scalars.Select(pair => new CaveScalarChangeVm(pair.Key, pair.Value.Previous, pair.Value.Current)).ToList())).ToList(),
        diff.ReferenceMetadataChanges);
}
