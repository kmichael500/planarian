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
    private readonly FileService _files;
    private readonly RequestUser _user;
    private readonly CaveRevisionDiffService _diff = new();

    public CaveChangeRequestService(CaveChangeRequestRepository requests, CaveRepository caves,
        CaveService caveService, CaveRevisionQueryRepository revisions, FileService files, RequestUser user)
    {
        _requests = requests;
        _caves = caves;
        _caveService = caveService;
        _revisions = revisions;
        _files = files;
        _user = user;
    }

    public async Task<CaveRevisionDiffVm> PreviewAsync(string caveId, AddCaveVm values,
        CancellationToken cancellationToken)
    {
        if (await _caves.GetCave(caveId) is null) throw ApiExceptionDictionary.NotFound("Cave");
        var currentId = await GetCurrentRevisionIdAsync(caveId, cancellationToken);
        var revision = await _revisions.GetAsync(caveId, currentId, cancellationToken)
                       ?? throw ApiExceptionDictionary.NotFound("Cave revision");
        var current = Deserialize(revision.Revision);
        var proposed = await PresentAsync(await BuildProposalAsync(values, caveId, cancellationToken), current,
            cancellationToken);
        return Map(_diff.Compare(current, proposed));
    }

    public async Task<string> CreateAsync(string caveId, AddCaveVm values, CancellationToken cancellationToken)
    {
        var cave = await _caves.GetCave(caveId) ?? throw ApiExceptionDictionary.NotFound("Cave");
        if (values.Id != caveId) throw ApiExceptionDictionary.BadRequest("The proposed Cave does not match the route.");
        var currentRevisionId = await GetCurrentRevisionIdAsync(caveId, cancellationToken);
        var proposal = await BuildProposalAsync(values, caveId, cancellationToken);
        return await _requests.CreateAsync(caveId, currentRevisionId, proposal, cancellationToken);
    }

    public async Task<string> AddVersionAsync(string requestId, AddCaveVm values, CancellationToken cancellationToken)
    {
        var row = await RequireReadableAsync(requestId, cancellationToken);
        if (values.Id != row.Request.CaveId)
            throw ApiExceptionDictionary.BadRequest("The proposed Cave does not match the request.");
        var reviewer = await CanReviewAsync(row, false);
        var proposal = await BuildProposalAsync(values, row.Request.CaveId, cancellationToken,
            row.Request.BaseRevisionId);
        return await _requests.AddVersionAsync(requestId, proposal, reviewer, cancellationToken);
    }

    public async Task<FileVm> StageFileAsync(string requestId, Stream stream, string fileName, string? uuid,
        CancellationToken cancellationToken)
    {
        var row = await RequireReadableAsync(requestId, cancellationToken);
        if (row.Request.CreatedByUserId != _user.Id)
            throw ApiExceptionDictionary.Forbidden("Only the submitter can add proposal files.");
        if (row.Request.Status != CaveChangeRequestStatus.Pending)
            throw ApiExceptionDictionary.BadRequest("Only pending requests can receive files.");

        var file = await _files.StageCaveChangeRequestFile(stream, fileName, cancellationToken, uuid);
        try
        {
            await _requests.StageFileAsync(requestId, file.Id, file.FileTypeTagId, file.DisplayName,
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

    public async Task<IReadOnlyList<CaveChangeRequestSummaryVm>> ListMineAsync(CancellationToken cancellationToken) =>
        (await _requests.ListMineAsync(cancellationToken)).Select(MapSummary).ToList();

    public async Task<IReadOnlyList<CaveChangeRequestSummaryVm>> ListForReviewAsync(CancellationToken cancellationToken)
    {
        var rows = await _requests.ListForReviewAsync(cancellationToken);
        var visible = new List<CaveChangeRequestSummaryVm>();
        foreach (var row in rows)
            if (await CanReviewAsync(row, false)) visible.Add(MapSummary(row));
        return visible;
    }

    public async Task<CaveChangeRequestDetailVm> GetAsync(string requestId, CancellationToken cancellationToken)
    {
        var row = await RequireReadableAsync(requestId, cancellationToken);
        var baseSnapshot = Deserialize(row.BaseRevision);
        var currentSnapshot = row.CurrentRevision is null ? baseSnapshot : Deserialize(row.CurrentRevision);
        var proposal = CaveProposalJson.Deserialize(row.ProposalVersion.ProposalJson, row.ProposalVersion.SchemaVersion);
        var proposedSnapshot = await PresentAsync(proposal, baseSnapshot, cancellationToken);
        var versions = await _requests.ListVersionsAsync(requestId, cancellationToken);
        return new CaveChangeRequestDetailVm(MapSummary(row), baseSnapshot, proposedSnapshot, currentSnapshot,
            Map(_diff.Compare(baseSnapshot, proposedSnapshot)),
            row.Request.BaseRevisionId == row.CurrentRevision?.Id ? null : Map(_diff.Compare(baseSnapshot, currentSnapshot)),
            versions.Select(version => new CaveProposalVersionVm(version.Version.Id,
                version.Version.PreviousProposalVersionId, version.Version.CreatedByUserId, version.ActorName,
                version.Version.CreatedOn, version.Version.Id == row.Request.CurrentProposalVersionId)).ToList());
    }

    public async Task<CaveChangeRequestDecisionVm> ApproveAsync(string requestId, string? notes,
        CancellationToken cancellationToken)
    {
        var row = await _requests.GetAsync(requestId, cancellationToken)
                  ?? throw ApiExceptionDictionary.NotFound("Change request");
        await CanReviewAsync(row, true);
        if (row.Request.Status != CaveChangeRequestStatus.Pending)
            throw ApiExceptionDictionary.BadRequest("This request has already been reviewed.");
        if (row.CurrentRevision?.Id != row.Request.BaseRevisionId)
            return new CaveChangeRequestDecisionVm(CaveChangeRequestDecisionResult.Conflict, requestId, null,
                row.CurrentRevision?.Id);

        var proposal = CaveProposalJson.Deserialize(row.ProposalVersion.ProposalJson, row.ProposalVersion.SchemaVersion);
        var values = ToAddCave(proposal);
        var stagedFileIds = proposal.Files.Where(file => file.Disposition == ProposalFileDisposition.PublishStaged)
            .Select(file => file.FileId).ToList();
        try
        {
            await _caveService.ApproveChangeRequestAsync(values, row.Request.BaseRevisionId!, requestId,
                stagedFileIds,
                (mutation, token) => _requests.MarkApprovedAsync(requestId, mutation, notes, token), cancellationToken);
        }
        catch (CaveRevisionConflictException conflict)
        {
            return new CaveChangeRequestDecisionVm(CaveChangeRequestDecisionResult.Conflict, requestId, null,
                conflict.ActualRevisionId);
        }
        var approved = await _requests.GetAsync(requestId, cancellationToken);
        return new CaveChangeRequestDecisionVm(CaveChangeRequestDecisionResult.Approved, requestId,
            approved?.Request.ApprovedRevisionId, approved?.CurrentRevision?.Id);
    }

    public async Task<CaveChangeRequestDecisionVm> RejectAsync(string requestId, string? notes,
        CancellationToken cancellationToken)
    {
        var row = await _requests.GetAsync(requestId, cancellationToken)
                  ?? throw ApiExceptionDictionary.NotFound("Change request");
        await CanReviewAsync(row, true);
        await _requests.RejectAsync(requestId, notes, cancellationToken);
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

    private async Task<CaveProposalSnapshotV1> BuildProposalAsync(AddCaveVm values, string caveId,
        CancellationToken cancellationToken, string? sourceRevisionId = null)
    {
        var currentRevisionId = sourceRevisionId ?? await GetCurrentRevisionIdAsync(caveId, cancellationToken);
        var currentRevision = await _revisions.GetAsync(caveId, currentRevisionId, cancellationToken)
                              ?? throw ApiExceptionDictionary.NotFound("Cave revision");
        var current = Deserialize(currentRevision.Revision);
        var ids = CaveTagGroups(values).SelectMany(group => group.Ids)
            .Concat(values.Entrances.SelectMany(EntranceTagGroups).SelectMany(group => group.Ids))
            .Concat(values.Entrances.Select(entrance => entrance.LocationQualityTagId));
        var names = await _requests.GetTagNamesAsync(ids, cancellationToken);
        string Name(string id) => names.GetValueOrDefault(id, id);

        return new CaveProposalSnapshotV1
        {
            CaveId = caveId,
            AccountId = _user.AccountId!,
            Name = values.Name.Trim(),
            AlternateNames = values.AlternateNames.Select(name => name.Trim()).ToList(),
            StateId = values.StateId,
            CountyId = values.CountyId,
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
                Latitude = entrance.Latitude,
                Longitude = entrance.Longitude,
                Elevation = entrance.ElevationFeet,
                LocationQualityTagId = entrance.LocationQualityTagId,
                ReportedOn = entrance.ReportedOn,
                PitDepthFeet = entrance.PitFeet,
                Tags = EntranceTagGroups(entrance).SelectMany(group => group.Ids.Select(id =>
                    new SnapshotTagReference(group.Role, id, Name(id)))).ToList()
            }).ToList(),
            Files = values.Files?.Select(file => new ProposalFileIntent(file.Id,
                ProposalFileDisposition.RetainPublished, file.FileTypeTagId, file.DisplayName)).ToList() ?? []
        };
    }

    private async Task<CavePublishedSnapshotV1> PresentAsync(CaveProposalSnapshotV1 proposal,
        CavePublishedSnapshotV1 @base, CancellationToken cancellationToken)
    {
        var labels = await _requests.GetLocationLabelsAsync(proposal.StateId, proposal.CountyId, cancellationToken);
        var tagNames = await _requests.GetTagNamesAsync(proposal.Entrances.Select(entrance => entrance.LocationQualityTagId), cancellationToken);
        var baseFiles = @base.Files.ToDictionary(file => file.Id);
        var stagedFiles = await _requests.GetFileSnapshotsAsync(proposal.Files
            .Where(file => file.Disposition == ProposalFileDisposition.PublishStaged)
            .Select(file => file.FileId), cancellationToken);
        return new CavePublishedSnapshotV1
        {
            CaveId = proposal.CaveId,
            AccountId = proposal.AccountId,
            Name = proposal.Name,
            AlternateNames = proposal.AlternateNames,
            State = new SnapshotReference(proposal.StateId, labels.StateName, null, labels.StateAbbreviation),
            County = new SnapshotReference(proposal.CountyId, labels.CountyName, labels.CountyDisplayId),
            CountyNumber = proposal.RequestedCountyNumber ?? (@base.County.Id == proposal.CountyId ? @base.CountyNumber : 0),
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
                Description = entrance.Description, Latitude = entrance.Latitude, Longitude = entrance.Longitude,
                Elevation = entrance.Elevation, Srid = entrance.Srid,
                LocationQualityTagId = entrance.LocationQualityTagId,
                LocationQualityNameAtRevision = tagNames.GetValueOrDefault(entrance.LocationQualityTagId,
                    entrance.LocationQualityTagId), ReportedOn = entrance.ReportedOn,
                PitDepthFeet = entrance.PitDepthFeet, Tags = entrance.Tags
            }).ToList(),
            Files = proposal.Files.Select(file => file.Disposition switch
                {
                    ProposalFileDisposition.RetainPublished when baseFiles.TryGetValue(file.FileId, out var published) =>
                        published with
                        {
                            FileTypeTagId = file.FileTypeTagId ?? published.FileTypeTagId,
                            DisplayName = file.DisplayName ?? published.DisplayName
                        },
                    ProposalFileDisposition.PublishStaged when stagedFiles.TryGetValue(file.FileId, out var staged) =>
                        staged with
                        {
                            FileTypeTagId = file.FileTypeTagId ?? staged.FileTypeTagId,
                            DisplayName = file.DisplayName ?? staged.DisplayName
                        },
                    _ => null
                }).Where(file => file is not null).Cast<CaveFileSnapshotV1>().ToList()
        };
    }

    private static AddCaveVm ToAddCave(CaveProposalSnapshotV1 proposal) => new()
    {
        Id = proposal.CaveId, Name = proposal.Name, AlternateNames = proposal.AlternateNames,
        StateId = proposal.StateId, CountyId = proposal.CountyId,
        IsCountyNumberManuallySet = proposal.CountyNumberIntent == CountyNumberIntent.Manual,
        UseFirstAvailableCountyNumber = proposal.CountyNumberIntent == CountyNumberIntent.FirstAvailable,
        CountyNumber = proposal.RequestedCountyNumber, LengthFeet = proposal.LengthFeet ?? 0,
        DepthFeet = proposal.DepthFeet ?? 0, MaxPitDepthFeet = proposal.MaxPitDepthFeet ?? 0,
        NumberOfPits = proposal.NumberOfPits ?? 0, Narrative = proposal.Narrative, ReportedOn = proposal.ReportedOn,
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
            Id = entrance.EntranceId, Name = entrance.Name, IsPrimary = entrance.IsPrimary,
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

    private CaveChangeRequestSummaryVm MapSummary(CaveChangeRequestReadRow row) => new(
        row.Request.Id, row.Request.CaveId, row.CaveName, row.Request.Status,
        row.Request.CreatedByUserId!, row.SubmitterName, row.Request.CreatedOn, row.Request.ModifiedOn,
        row.Request.ReviewedOn, row.Request.ReviewerUserId, row.ReviewerName, row.Request.ReviewerNotes,
        row.Request.BaseRevisionId!, row.CurrentRevision?.Id, row.Request.CurrentProposalVersionId!,
        row.Request.BaseRevisionId != row.CurrentRevision?.Id, row.Request.ApprovedRevisionId,
        row.Request.CreatedByUserId == _user.Id && row.Request.Status == CaveChangeRequestStatus.Pending);

    private static CaveRevisionDiffVm Map(CaveRevisionDiff diff) => new(
        diff.Scalars.Select(change => new CaveScalarChangeVm(change.Key, change.Value.Previous, change.Value.Current)).ToList(),
        diff.AddedTags, diff.RemovedTags, diff.AddedEntrances, diff.RemovedEntrances, diff.ChangedEntrances,
        diff.AddedFiles, diff.RemovedFiles, diff.ChangedFiles, diff.ReferenceMetadataChanges);
}
