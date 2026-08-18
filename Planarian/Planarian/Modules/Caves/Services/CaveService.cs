using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using Planarian.Library.Exceptions;
using Planarian.Library.Extensions.DateTime;
using Planarian.Library.Extensions.String;
using Planarian.Library.Helpers;
using Planarian.Model.Database.Entities;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.Revisions;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Base;
using Planarian.Model.Shared.Helpers;
using Planarian.Modules.Account.Repositories;
using Planarian.Modules.Caves.Models;
using Planarian.Modules.Caves.Repositories;
using Planarian.Modules.Caves.Revisions;
using Planarian.Modules.Files.Repositories;
using Planarian.Modules.Files.Services;
using Planarian.Modules.Query.Extensions;
using Planarian.Modules.Query.Models;
using Planarian.Modules.Tags;
using Planarian.Modules.Tags.Repositories;
using Planarian.Shared.Base;
using Planarian.Shared.Services;
using File = Planarian.Model.Database.Entities.RidgeWalker.File;

namespace Planarian.Modules.Caves.Services;

public class CaveService : ServiceBase<CaveRepository>
{
    private readonly FileService _fileService;
    private readonly TagRepository _tagRepository;
    private readonly FeatureSettingRepository _featureSettingRepository;
    private readonly ClientUrlBuilder _clientUrlBuilder;
    private readonly CaveMutationCoordinator _caveMutationCoordinator;
    private readonly TagReferenceLockRepository _tagReferenceLocks;
    private readonly CountyReferenceLockRepository _countyReferenceLocks;

    public CaveService(CaveRepository repository, RequestUser requestUser, FileService fileService,
        TagRepository tagRepository,
        FeatureSettingRepository featureSettingRepository, ClientUrlBuilder clientUrlBuilder,
        CaveMutationCoordinator caveMutationCoordinator, TagReferenceLockRepository tagReferenceLocks,
        CountyReferenceLockRepository countyReferenceLocks) : base(
        repository, requestUser)
    {
        _fileService = fileService;
        _tagRepository = tagRepository;
        _featureSettingRepository = featureSettingRepository;
        _clientUrlBuilder = clientUrlBuilder;
        _caveMutationCoordinator = caveMutationCoordinator;
        _tagReferenceLocks = tagReferenceLocks;
        _countyReferenceLocks = countyReferenceLocks;
    }

    #region Caves

    public async Task<PagedResult<CaveSearchVm>> GetCaves(FilterQuery query)
    {
        return await Repository.GetCaves(query);
    }

    public async Task<PagedResult<CaveSearchVm>> GetCavesSearch(FilterQuery query, string? permissionKey = null)
    {
        return await Repository.GetCavesSearch(query, permissionKey);
    }

    public async Task<CaveEditAuthoringContextVm> GetEditAuthoringContextAsync(string caveId,
        CancellationToken cancellationToken)
    {
        var visible = await Repository.GetCave(caveId) ?? throw ApiExceptionDictionary.NotFound("Cave");
        await RequestUser.HasCavePermission(PermissionPolicyKey.Manager, caveId, visible.CountyId, visible.StateId);
        await _caveMutationCoordinator.EnsureBaselineAsync(caveId, cancellationToken);
        var cave = await Repository.GetCave(caveId) ?? throw ApiExceptionDictionary.NotFound("Cave");
        var linePlots = await Repository.GetCaveLinePlotsAsync(caveId, cancellationToken);
        return new CaveEditAuthoringContextVm(cave, linePlots);
    }

    public async Task<int> GetNextCountyNumber(string countyId, bool useFirstAvailableCountyNumber = false)
    {
        return await Repository.GetNewDisplayId(countyId, useFirstAvailableCountyNumber);
    }

    public async Task<bool> IsCountyNumberInUse(string countyId, int countyNumber, string? caveId = null)
    {
        return await Repository.IsCountyNumberInUse(countyId, countyNumber, caveId);
    }

    public async Task<byte[]> ExportCavesGpx(FilterQuery filterQuery, string? permissionKey,
        CancellationToken cancellationToken = default)
    {
        var featureSettings = await _featureSettingRepository.GetFeatureSettings(cancellationToken);
        var featureDict = featureSettings.ToDictionary(fs => fs.Key, fs => fs.IsEnabled);
        var exportFieldSet = filterQuery.ExportFields?.ToHashSet(StringComparer.OrdinalIgnoreCase);

        bool Include(FeatureKey key) =>
            featureDict.TryGetValue(key, out var enabled) && enabled &&
            (exportFieldSet == null || exportFieldSet.Contains(key.ToString()));

        var exportData = await Repository.GetCavesForExport(filterQuery, permissionKey);

        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine("<gpx version=\"1.1\" creator=\"Planarian\" xmlns=\"http://www.topografix.com/GPX/1/1\">");

        foreach (var cave in exportData)
        {
            foreach (var entrance in cave.Entrances)
            {
                // Build the waypoint name.
                var caveNameEnabled = Include(FeatureKey.EnabledFieldCaveName);
                var caveName = caveNameEnabled ? cave.Name : string.Empty;
                var entranceNameEnabled = Include(FeatureKey.EnabledFieldEntranceName);
                var entranceName = entranceNameEnabled ? entrance.Name : string.Empty;
                entranceName = System.Security.SecurityElement.Escape(entranceName);

                var showCaveId = Include(FeatureKey.EnabledFieldCaveId);
                var caveCountyId = showCaveId
                    ? $"{cave.CountyDisplayId}{cave.CountyIdDelimiter}{cave.CountyNumber}"
                    : string.Empty;
                var waypointName = string.Empty;

                if (!string.IsNullOrWhiteSpace(caveCountyId))
                {
                    waypointName = $"{caveCountyId}";
                }

                if (!string.IsNullOrWhiteSpace(caveName))
                {
                    waypointName = $"{waypointName} {caveName}";
                }

                if (!string.IsNullOrWhiteSpace(entranceName) && !string.Equals(entranceName, caveName,
                        StringComparison.CurrentCultureIgnoreCase))
                {
                    waypointName = $"{waypointName} ({entranceName})";
                }

                if (entrance.IsPrimary) // Append asterisk if this is the primary entrance.
                {
                    waypointName = $"{waypointName} *";
                }

                if (string.IsNullOrWhiteSpace(waypointName))
                {
                    waypointName = !string.IsNullOrWhiteSpace(cave.Name)
                        ? cave.Name
                        : cave.Id;
                }

                var latitude = entrance.Latitude;
                var longitude = entrance.Longitude;
                var elevation = entrance.Elevation;

                var descriptionStringBuilder = new StringBuilder();

                var caveInfoLines = new List<string>();

                if (showCaveId)
                {
                    if (!string.IsNullOrWhiteSpace(caveCountyId))
                        caveInfoLines.Add($"ID: {caveCountyId}");
                }

                if (Include(FeatureKey.EnabledFieldCaveName))
                {
                    var name = cave.Name;
                    if (!string.IsNullOrWhiteSpace(name))
                        caveInfoLines.Add($"Name: {name}");
                }

                if (Include(FeatureKey.EnabledFieldCaveAlternateNames))
                {
                    var altNames = cave.AlternateNames.ToCommaSeparatedString();
                    if (!string.IsNullOrWhiteSpace(altNames))
                        caveInfoLines.Add($"Alternate Names: {altNames}");
                }

                if (Include(FeatureKey.EnabledFieldCaveCounty))
                {
                    var county = cave.CountyName;
                    if (!string.IsNullOrWhiteSpace(county))
                        caveInfoLines.Add($"County: {county}");
                }

                if (Include(FeatureKey.EnabledFieldCaveState))
                {
                    var state = cave.StateName;
                    if (!string.IsNullOrWhiteSpace(state))
                        caveInfoLines.Add($"State: {state}");
                }

                if (Include(FeatureKey.EnabledFieldCaveLengthFeet))
                {
                    if (cave.LengthFeet.HasValue && cave.LengthFeet != 0)
                        caveInfoLines.Add($"Length (ft): {cave.LengthFeet}");
                }

                if (Include(FeatureKey.EnabledFieldCaveDepthFeet))
                {
                    if (cave.DepthFeet.HasValue && cave.DepthFeet != 0)
                        caveInfoLines.Add($"Depth (ft): {cave.DepthFeet}");
                }

                if (Include(FeatureKey.EnabledFieldCaveMaxPitDepthFeet))
                {
                    if (cave.MaxPitDepthFeet.HasValue && cave.MaxPitDepthFeet != 0)
                        caveInfoLines.Add($"Max Pit Depth (ft): {cave.MaxPitDepthFeet}");
                }

                if (Include(FeatureKey.EnabledFieldCaveNumberOfPits))
                {
                    if (cave.NumberOfPits.HasValue && cave.NumberOfPits != 0)
                        caveInfoLines.Add($"Number Of Pits: {cave.NumberOfPits}");
                }

                if (Include(FeatureKey.EnabledFieldCaveReportedOn))
                {
                    var reportedOn = cave.ReportedOn.HasValue
                        ? cave.ReportedOn.Value.ToShortDateString()
                        : string.Empty;
                    if (!string.IsNullOrWhiteSpace(reportedOn))
                        caveInfoLines.Add($"Reported On: {reportedOn}");
                }

                if (Include(FeatureKey.EnabledFieldCaveGeologyTags))
                {
                    var geology = cave.GeologyTags.ToCommaSeparatedString();
                    if (!string.IsNullOrWhiteSpace(geology))
                        caveInfoLines.Add($"Geology: {geology}");
                }

                if (Include(FeatureKey.EnabledFieldCaveGeologicAgeTags))
                {
                    var geoAge = cave.GeologicAgeTags.ToCommaSeparatedString();
                    if (!string.IsNullOrWhiteSpace(geoAge))
                        caveInfoLines.Add($"Geologic Age: {geoAge}");
                }

                if (Include(FeatureKey.EnabledFieldCavePhysiographicProvinceTags))
                {
                    var physio = cave.PhysiographicProvinceTags.ToCommaSeparatedString();
                    if (!string.IsNullOrWhiteSpace(physio))
                        caveInfoLines.Add($"Physiographic Province: {physio}");
                }

                if (Include(FeatureKey.EnabledFieldCaveBiologyTags))
                {
                    var biology = cave.BiologyTags.ToCommaSeparatedString();
                    if (!string.IsNullOrWhiteSpace(biology))
                        caveInfoLines.Add($"Biology: {biology}");
                }

                if (Include(FeatureKey.EnabledFieldCaveArcheologyTags))
                {
                    var archeology = cave.ArcheologyTags.ToCommaSeparatedString();
                    if (!string.IsNullOrWhiteSpace(archeology))
                        caveInfoLines.Add($"Archeology: {archeology}");
                }

                if (Include(FeatureKey.EnabledFieldCaveMapStatusTags))
                {
                    var mapStatus = cave.MapStatusTags.ToCommaSeparatedString();
                    if (!string.IsNullOrWhiteSpace(mapStatus))
                        caveInfoLines.Add($"Map Status: {mapStatus}");
                }

                if (Include(FeatureKey.EnabledFieldCaveCartographerNameTags))
                {
                    var cartographer = cave.CartographerNameTags.ToCommaSeparatedString();
                    if (!string.IsNullOrWhiteSpace(cartographer))
                        caveInfoLines.Add($"Cartographer Name: {cartographer}");
                }

                if (Include(FeatureKey.EnabledFieldCaveReportedByNameTags))
                {
                    var reportedBy = cave.CaveReportedByTags.ToCommaSeparatedString();
                    if (!string.IsNullOrWhiteSpace(reportedBy))
                        caveInfoLines.Add($"Cave Reported By: {reportedBy}");
                }

                if (Include(FeatureKey.EnabledFieldCaveOtherTags))
                {
                    var otherTags = cave.CaveOtherTags.ToCommaSeparatedString();
                    if (!string.IsNullOrWhiteSpace(otherTags))
                        caveInfoLines.Add($"Other: {otherTags}");
                }

                if (caveInfoLines.Count > 0)
                {
                    descriptionStringBuilder.AppendLine("Cave Information:");
                    foreach (var line in caveInfoLines)
                    {
                        descriptionStringBuilder.AppendLine(line);
                    }
                }
                else
                {
                    descriptionStringBuilder.AppendLine($"Cave Information: {"".DefaultIfNullOrWhiteSpace()}");
                }

                // Build Entrance Information only for non-null fields.
                var entranceInfoSb = new StringBuilder();

                descriptionStringBuilder.AppendLine();
                if (Include(FeatureKey.EnabledFieldEntranceName))
                {
                    var name = entrance.Name;
                    if (!string.IsNullOrWhiteSpace(name) &&
                        !string.Equals(name, cave.Name, StringComparison.CurrentCultureIgnoreCase))
                        entranceInfoSb.AppendLine($"Name: {name}");
                }

                if (Include(FeatureKey.EnabledFieldEntranceReportedOn))
                {
                    var reportedOn = entrance.ReportedOn.HasValue
                        ? entrance.ReportedOn.Value.ToShortDateString()
                        : string.Empty;
                    if (!string.IsNullOrWhiteSpace(reportedOn))
                        entranceInfoSb.AppendLine($"Reported On: {reportedOn}");
                }

                if (Include(FeatureKey.EnabledFieldEntrancePitDepth))
                {
                    if (entrance.PitDepthFeet.HasValue && entrance.PitDepthFeet != 0)
                        entranceInfoSb.AppendLine($"Pit Depth (ft): {entrance.PitDepthFeet}");
                }

                entranceInfoSb.AppendLine("Primary Entrance: " + (entrance.IsPrimary ? "Yes" : "No"));

                if (Include(FeatureKey.EnabledFieldEntranceLocationQuality))
                {
                    var locQual = entrance.LocationQuality;
                    if (!string.IsNullOrWhiteSpace(locQual))
                        entranceInfoSb.AppendLine($"Location Quality: {locQual}");
                }

                if (Include(FeatureKey.EnabledFieldEntranceStatusTags))
                {
                    var statusTags = entrance.EntranceStatusTags.ToCommaSeparatedString();
                    if (!string.IsNullOrWhiteSpace(statusTags))
                        entranceInfoSb.AppendLine($"Entrance Status: {statusTags}");
                }

                if (Include(FeatureKey.EnabledFieldEntranceFieldIndicationTags))
                {
                    var fieldIndTags = entrance.FieldIndicationTags.ToCommaSeparatedString();
                    if (!string.IsNullOrWhiteSpace(fieldIndTags))
                        entranceInfoSb.AppendLine($"Field Indication: {fieldIndTags}");
                }

                if (Include(FeatureKey.EnabledFieldEntranceHydrologyTags))
                {
                    var hydroTags = entrance.EntranceHydrologyTags.ToCommaSeparatedString();
                    if (!string.IsNullOrWhiteSpace(hydroTags))
                        entranceInfoSb.AppendLine($"Entrance Hydrology: {hydroTags}");
                }

                if (Include(FeatureKey.EnabledFieldEntranceReportedByNameTags))
                {
                    var reportedByTags = entrance.EntranceReportedByTags.ToCommaSeparatedString();
                    if (!string.IsNullOrWhiteSpace(reportedByTags))
                        entranceInfoSb.AppendLine($"Entrance Reported By: {reportedByTags}");
                }

                if (Include(FeatureKey.EnabledFieldEntranceDescription))
                {
                    var description = entrance.Description;
                    if (!string.IsNullOrWhiteSpace(description))
                    {
                        var safeDescription = System.Security.SecurityElement.Escape(description);
                        entranceInfoSb.AppendLine($"Description: {safeDescription}");
                    }
                }

                if (Include(FeatureKey.EnabledFieldCaveNarrative))
                {
                    var narrative = cave.Narrative;
                    if (!string.IsNullOrWhiteSpace(narrative))
                    {
                        entranceInfoSb.AppendLine();
                        entranceInfoSb.AppendLine("Narrative:");
                        entranceInfoSb.AppendLine(narrative);
                    }
                }

                if (entranceInfoSb.Length > 0)
                {
                    descriptionStringBuilder.AppendLine("Entrance Information:");
                    descriptionStringBuilder.Append(entranceInfoSb.ToString());
                }
                else
                {
                    descriptionStringBuilder.AppendLine($"Entrance Information: {"".DefaultIfNullOrWhiteSpace()}");
                }

                descriptionStringBuilder.AppendLine();
                descriptionStringBuilder.AppendLine(_clientUrlBuilder.BuildCaveUrl(cave.Id));

                // Build the GPX waypoint.
                sb.AppendLine($"  <wpt lat=\"{latitude}\" lon=\"{longitude}\">");
                if (elevation > 0)
                {
                    sb.AppendLine($"    <ele>{elevation}</ele>");
                }

                var safeWaypointName = System.Security.SecurityElement.Escape(waypointName);
                sb.AppendLine($"    <name>{safeWaypointName}</name>");

                var safeCmt = System.Security.SecurityElement.Escape(descriptionStringBuilder.ToString());
                sb.AppendLine($"    <cmt><![CDATA[{safeCmt}]]></cmt>");
                sb.AppendLine("  </wpt>");
            }
        }

        sb.AppendLine("</gpx>");
        var gpx = sb.ToString();
        return Encoding.UTF8.GetBytes(gpx);
    }

     public async Task<byte[]> ExportCavesCsv(FilterQuery filterQuery, string? permissionKey,
        CancellationToken cancellationToken = default)
    {
        var featureSettings = await _featureSettingRepository.GetFeatureSettings(cancellationToken);
        var featureDict = featureSettings.ToDictionary(fs => fs.Key, fs => fs.IsEnabled);
        var exportFieldSet = filterQuery.ExportFields?.ToHashSet(StringComparer.OrdinalIgnoreCase);

        bool Include(FeatureKey key) =>
            featureDict.TryGetValue(key, out var enabled) && enabled &&
            (exportFieldSet == null || exportFieldSet.Contains(key.ToString()));

        var caves = await Repository.GetCavesForExport(filterQuery, permissionKey);

        var csvRecords = new List<CaveEntranceCsvModel>();

        foreach (var cave in caves)
        {
            foreach (var entrance in cave.Entrances)
            {
                var record = new CaveEntranceCsvModel
                {
                    CaveId = cave.Id,
                    CaveName = Include(FeatureKey.EnabledFieldCaveName) ? cave.Name : null,
                    CaveAlternateNames = Include(FeatureKey.EnabledFieldCaveAlternateNames) ? cave.AlternateNames.ToCommaSeparatedString() : null,
                    CaveCounty = Include(FeatureKey.EnabledFieldCaveCounty) ? cave.CountyName : null,
                    CaveCountyDisplayId = cave.CountyDisplayId,
                    CaveState = Include(FeatureKey.EnabledFieldCaveState) ? cave.StateName : null,
                    CaveCountyNumber = cave.CountyNumber,
                    CaveLengthFeet = Include(FeatureKey.EnabledFieldCaveLengthFeet) ? cave.LengthFeet : null,
                    CaveDepthFeet = Include(FeatureKey.EnabledFieldCaveDepthFeet) ? cave.DepthFeet : null,
                    CaveMaxPitDepthFeet = Include(FeatureKey.EnabledFieldCaveMaxPitDepthFeet) ? cave.MaxPitDepthFeet : null,
                    CaveNumberOfPits = Include(FeatureKey.EnabledFieldCaveNumberOfPits) ? cave.NumberOfPits : null,
                    CaveNarrative = Include(FeatureKey.EnabledFieldCaveNarrative) ? cave.Narrative : null,
                    CaveReportedOn = Include(FeatureKey.EnabledFieldCaveReportedOn) ? cave.ReportedOn : null,
                    CaveIsArchived = cave.IsArchived,
                    CaveGeologyTags = Include(FeatureKey.EnabledFieldCaveGeologyTags) ? cave.GeologyTags.ToCommaSeparatedString() : null,
                    CaveMapStatusTags = Include(FeatureKey.EnabledFieldCaveMapStatusTags) ? cave.MapStatusTags.ToCommaSeparatedString() : null,
                    CaveGeologicAgeTags = Include(FeatureKey.EnabledFieldCaveGeologicAgeTags) ? cave.GeologicAgeTags.ToCommaSeparatedString() : null,
                    CavePhysiographicProvinceTags = Include(FeatureKey.EnabledFieldCavePhysiographicProvinceTags) ? cave.PhysiographicProvinceTags.ToCommaSeparatedString() : null,
                    CaveBiologyTags = Include(FeatureKey.EnabledFieldCaveBiologyTags) ? cave.BiologyTags.ToCommaSeparatedString() : null,
                    CaveArcheologyTags = Include(FeatureKey.EnabledFieldCaveArcheologyTags) ? cave.ArcheologyTags.ToCommaSeparatedString() : null,
                    CaveCartographerNameTags = Include(FeatureKey.EnabledFieldCaveCartographerNameTags) ? cave.CartographerNameTags.ToCommaSeparatedString() : null,
                    CaveReportedByTags = Include(FeatureKey.EnabledFieldCaveReportedByNameTags) ? cave.CaveReportedByTags.ToCommaSeparatedString() : null,
                    CaveOtherTags = Include(FeatureKey.EnabledFieldCaveOtherTags) ? cave.CaveOtherTags.ToCommaSeparatedString() : null,
                    EntranceName = Include(FeatureKey.EnabledFieldEntranceName) ? entrance.Name : null,
                    EntranceDescription = Include(FeatureKey.EnabledFieldEntranceDescription) ? entrance.Description : null,
                    EntranceIsPrimary = entrance.IsPrimary,
                    EntranceReportedOn = Include(FeatureKey.EnabledFieldEntranceReportedOn) ? entrance.ReportedOn : null,
                    EntrancePitDepthFeet = Include(FeatureKey.EnabledFieldEntrancePitDepth) ? entrance.PitDepthFeet : null,
                    EntranceLatitude = entrance.Latitude,
                    EntranceLongitude = entrance.Longitude,
                    EntranceElevation = entrance.Elevation,
                    EntranceLocationQuality = Include(FeatureKey.EnabledFieldEntranceLocationQuality) ? entrance.LocationQuality : null,
                    EntranceStatusTags = Include(FeatureKey.EnabledFieldEntranceStatusTags) ? entrance.EntranceStatusTags.ToCommaSeparatedString() : null,
                    EntranceFieldIndicationTags = Include(FeatureKey.EnabledFieldEntranceFieldIndicationTags) ? entrance.FieldIndicationTags.ToCommaSeparatedString() : null,
                    EntranceHydrologyTags = Include(FeatureKey.EnabledFieldEntranceHydrologyTags) ? entrance.EntranceHydrologyTags.ToCommaSeparatedString() : null,
                    EntranceReportedByTags = Include(FeatureKey.EnabledFieldEntranceReportedByNameTags) ? entrance.EntranceReportedByTags.ToCommaSeparatedString() : null,
                    EntranceOtherTags = Include(FeatureKey.EnabledFieldEntranceOtherTags) ? entrance.EntranceOtherTags.ToCommaSeparatedString() : null
                };

                csvRecords.Add(record);
            }
        }

        using var memoryStream = new MemoryStream();
        using var writer = new StreamWriter(memoryStream);
        using var csv = CsvExportPolicy.CreateWriter(writer);
        csv.Context.RegisterClassMap(new CaveEntranceCsvModelMap(featureDict, exportFieldSet));
        csv.WriteRecords(csvRecords);
        writer.Flush();
        return memoryStream.ToArray();
    }

    public Task<string> AddCave(AddCaveVm values, CancellationToken cancellationToken) =>
        SaveCaveAsync(values, CaveRevisionSource.ManagerEdit, null, values.ExpectedRevisionId, null, null, null, null, null,
            cancellationToken);

    internal Task<string> ApproveChangeRequestAsync(AddCaveVm values, string baseRevisionId, string changeRequestId,
        IReadOnlyList<StagedCaveFilePublication> stagedFilePublications,
        IReadOnlySet<string> authorizedNewEntranceIds,
        IReadOnlySet<string> authorizedNewLinePlotIds,
        CavePeoplePublicationContext peoplePublicationContext,
        Func<CaveMutationResult, CancellationToken, Task> beforeCommit, CancellationToken cancellationToken) =>
        SaveCaveAsync(values, CaveRevisionSource.UserSubmission, changeRequestId, baseRevisionId,
            stagedFilePublications, authorizedNewEntranceIds, authorizedNewLinePlotIds, peoplePublicationContext,
            beforeCommit, cancellationToken);

    public Task<string> ApproveChangeRequestAsync(AddCaveVm values, string baseRevisionId, string changeRequestId,
        IReadOnlyList<StagedCaveFilePublication> stagedFilePublications,
        Func<CaveMutationResult, CancellationToken, Task> beforeCommit, CancellationToken cancellationToken) =>
        SaveCaveAsync(values, CaveRevisionSource.UserSubmission, changeRequestId, baseRevisionId,
            stagedFilePublications, new HashSet<string>(StringComparer.Ordinal),
            new HashSet<string>(StringComparer.Ordinal), null, beforeCommit, cancellationToken);

    private async Task<string> SaveCaveAsync(AddCaveVm values, CaveRevisionSource revisionSource,
        string? changeRequestId, string? expectedRevisionId,
        IReadOnlyList<StagedCaveFilePublication>? stagedFilePublications,
        IReadOnlySet<string>? authorizedNewEntranceIds,
        IReadOnlySet<string>? authorizedNewLinePlotIds,
        CavePeoplePublicationContext? peoplePublicationContext,
        Func<CaveMutationResult, CancellationToken, Task>? beforeCommit, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(RequestUser.AccountId)) throw ApiExceptionDictionary.NoAccount;

        if (revisionSource == CaveRevisionSource.UserSubmission)
            await RequestUser.HasCavePermission(PermissionPolicyKey.Manager, values.Id, null, null);
        else
            await RequestUser.HasCavePermission(PermissionPolicyKey.Manager, values.Id, values.CountyId, values.StateId);
        var isNew = string.IsNullOrWhiteSpace(values.Id);
        if (!isNew && revisionSource == CaveRevisionSource.ManagerEdit &&
            string.IsNullOrWhiteSpace(expectedRevisionId))
            throw ApiExceptionDictionary.BadRequest("ExpectedRevisionId is required when editing an existing Cave.");
        if (isNew && !string.IsNullOrWhiteSpace(expectedRevisionId))
            throw ApiExceptionDictionary.BadRequest("ExpectedRevisionId must be empty when creating a Cave.");
        CaveMutationValidation.NormalizeAndValidate(values);

        // Object storage is outside the relational transaction. Verify generic authoring uploads
        // before opening that transaction, then revalidate and lock their relational rows when attaching them.
        if (changeRequestId is null)
        {
            var desiredFileIds = (values.Files ?? []).Select(file => file.Id)
                .ToHashSet(StringComparer.Ordinal);
            if (desiredFileIds.Count > 0)
            {
                var publishedFileIds = isNew
                    ? new HashSet<string>(StringComparer.Ordinal)
                    : await Repository.GetPublishedFileIdsAsync(values.Id!, cancellationToken);
                var authoringStagedFileIds = desiredFileIds.Except(publishedFileIds, StringComparer.Ordinal).ToList();
                await _fileService.RequireAuthoringStagedObjectsAvailableAsync(authoringStagedFileIds,
                    cancellationToken);
            }
        }

        await using var transaction = await Repository.BeginTransactionAsync(cancellationToken);
        string savedCaveId;
        try
        {
            var entity = isNew ? new Cave { Id = IdGenerator.Generate() } : await Repository.GetAsync(values.Id);

            if (entity == null) throw ApiExceptionDictionary.NotFound(nameof(entity.Id));

            var isNewCounty = entity.CountyId != values.CountyId;
            if (values.IsCountyNumberManuallySet || isNewCounty)
            {
                var lockedCounties = await _countyReferenceLocks.LockForMutationAsync([values.CountyId], cancellationToken);
                if (lockedCounties.Count != 1 || lockedCounties[0].StateId != values.StateId)
                    throw ApiExceptionDictionary.BadRequest(
                        "The selected County does not belong to the selected State.");
            }
            else
            {
                await Repository.ValidateStateCountyPairAsync(values.StateId, values.CountyId, cancellationToken);
            }
            var resolvedTags = await ValidateAndNormalizeTagReferencesAsync(values,
                peoplePublicationContext, cancellationToken);
            var existingTagIds = CollectExistingStableTagTypeIds(entity)
                .Concat(resolvedTags.TargetExistingIds)
                .Distinct(StringComparer.Ordinal).ToList();
            var lockedTags = await _tagReferenceLocks.LockForReferenceAsync(existingTagIds, cancellationToken);
            if (lockedTags.Count != existingTagIds.Count)
                throw ApiExceptionDictionary.BadRequest(
                    "One or more selected tag references changed or disappeared. Retry with current data.");
            var peopleTagsBySelection = resolvedTags.PeopleBindings;

            CaveMutationPreparation? revisionPreparation = null;
            if (!isNew)
            {
                revisionPreparation = await _caveMutationCoordinator.PrepareExistingAsync(
                    entity.Id, expectedRevisionId, cancellationToken);
            }

            int? countyNumber = null;

            if (values.IsCountyNumberManuallySet)
            {
                countyNumber = values.CountyNumber;

                var countyNumberInUse =
                    await Repository.IsCountyNumberInUse(values.CountyId, countyNumber.Value, values.Id);
                if (countyNumberInUse)
                    throw ApiExceptionDictionary.BadRequest("That county number is already in use for the selected county.");
            }

            entity.Name = values.Name.Trim();
            entity.SetAlternateNamesList(CaveAlternateNameNormalizer.Normalize(values.AlternateNames));

            entity.CountyId = values.CountyId.Trim();
            entity.StateId = values.StateId.Trim();
            entity.LengthFeet = values.LengthFeet;
            entity.DepthFeet = values.DepthFeet;
            entity.MaxPitDepthFeet = values.MaxPitDepthFeet;
            entity.NumberOfPits = values.NumberOfPits;
            entity.Narrative = values.Narrative?.Trim();
            entity.ReportedOn = values.ReportedOn?.ToUtcKind();
            entity.AccountId = RequestUser.AccountId;

            var removedTagAssociations = new List<EntityBase>();

            SyncTags(entity.GeologyTags, values.GeologyTagIds, tag => tag.TagTypeId,
                tagId => new GeologyTag { TagTypeId = tagId }, deferDelete: true);
            SyncTags(entity.ArcheologyTags, values.ArcheologyTagIds, tag => tag.TagTypeId,
                tagId => new ArcheologyTag { TagTypeId = tagId }, deferDelete: true);
            SyncTags(entity.BiologyTags, values.BiologyTagIds, tag => tag.TagTypeId,
                tagId => new BiologyTag { TagTypeId = tagId }, deferDelete: true);

            RemoveUnselected(entity.CartographerNameTags, values.CartographerNameTagIds,
                tag => tag.TagTypeId, deferDelete: true);
            foreach (var personTagTypeId in values.CartographerNameTagIds)
            {
                if (entity.CartographerNameTags.Any(tag => tag.TagTypeId == personTagTypeId)) continue;

                var binding = peopleTagsBySelection[personTagTypeId];
                var tag = new CartographerNameTag
                {
                    TagTypeId = binding.Id,
                    TagType = binding.NewTag
                };

                entity.CartographerNameTags.Add(tag);
            }

            SyncTags(entity.MapStatusTags, values.MapStatusTagIds, tag => tag.TagTypeId,
                tagId => new MapStatusTag { TagTypeId = tagId }, deferDelete: true);
            SyncTags(entity.GeologicAgeTags, values.GeologicAgeTagIds, tag => tag.TagTypeId,
                tagId => new GeologicAgeTag { TagTypeId = tagId }, deferDelete: true);
            SyncTags(entity.PhysiographicProvinceTags, values.PhysiographicProvinceTagIds,
                tag => tag.TagTypeId, tagId => new PhysiographicProvinceTag { TagTypeId = tagId }, deferDelete: true);
            SyncTags(entity.CaveOtherTags, values.OtherTagIds, tag => tag.TagTypeId,
                tagId => new CaveOtherTag { TagTypeId = tagId }, deferDelete: true);

            RemoveUnselected(entity.CaveReportedByNameTags, values.ReportedByNameTagIds,
                tag => tag.TagTypeId, deferDelete: true);
            foreach (var personTagTypeId in values.ReportedByNameTagIds)
            {
                if (entity.CaveReportedByNameTags.Any(tag => tag.TagTypeId == personTagTypeId)) continue;
                var binding = peopleTagsBySelection[personTagTypeId];
                var tag = new CaveReportedByNameTag
                {
                    TagTypeId = binding.Id,
                    TagType = binding.NewTag
                };

                entity.CaveReportedByNameTags.Add(tag);
            }

            void RemoveUnselected<T>(ICollection<T> current, IEnumerable<string> selectedIds,
                Func<T, string> getTagTypeId, bool deferDelete = true) where T : EntityBase
            {
                var selected = selectedIds.ToHashSet(StringComparer.Ordinal);
                foreach (var removed in current.Where(tag => !selected.Contains(getTagTypeId(tag))).ToList())
                {
                    current.Remove(removed);
                    if (deferDelete) removedTagAssociations.Add(removed);
                    else Repository.Delete(removed);
                }
            }

            void SyncTags<T>(ICollection<T> current, IEnumerable<string> selectedIds,
                Func<T, string> getTagTypeId, Func<string, T> create, bool deferDelete = true) where T : EntityBase
            {
                var selected = selectedIds.ToHashSet(StringComparer.Ordinal);
                RemoveUnselected(current, selected, getTagTypeId, deferDelete);
                var existing = current.Select(getTagTypeId).ToHashSet(StringComparer.Ordinal);
                foreach (var tagId in selected.Where(tagId => !existing.Contains(tagId)))
                    current.Add(create(tagId));
            }

            if (values.IsCountyNumberManuallySet && countyNumber.HasValue)
            {
                entity.CountyNumber = countyNumber.Value;
            }
            else if (isNewCounty)
            {
                entity.CountyNumber = await Repository.GetNewDisplayId(entity.CountyId,
                    values.UseFirstAvailableCountyNumber);
            }

            if (!isNew)
                // remove entrances
                foreach (var entrance in entity.Entrances.ToList())
                {
                    var entranceValue = values.Entrances.FirstOrDefault(e => e.Id == entrance.Id);

                    if (entranceValue != null) continue;

                    // Entrance tag rows use identifying composite keys that include EntranceId.
                    // Mark dependents deleted while their principal relationship is still intact;
                    // severing/removing the Entrance first makes EF try to null/change those key FKs.
                    Repository.DeleteRange(entrance.EntranceStatusTags);
                    Repository.DeleteRange(entrance.FieldIndicationTags);
                    Repository.DeleteRange(entrance.EntranceOtherTags);
                    Repository.DeleteRange(entrance.EntranceHydrologyTags);
                    Repository.DeleteRange(entrance.EntranceReportedByNameTags);
                    Repository.Delete(entrance);
                }

            foreach (var entranceValue in values.Entrances)
            {
                var isAuthorizedProposalEntrance = !string.IsNullOrWhiteSpace(entranceValue.Id) &&
                    authorizedNewEntranceIds?.Contains(entranceValue.Id) == true;
                var isNewEntrance = string.IsNullOrWhiteSpace(entranceValue.Id) || isAuthorizedProposalEntrance;

                Entrance? entrance;
                if (isNewEntrance)
                {
                    // Allocate the final Entrance ID before its composite-key tag rows are tracked.
                    // Otherwise EF can try to propagate a later principal-ID change into an identifying
                    // EntranceId foreign key when the same tag value moves between entrances.
                    entrance = new Entrance
                    {
                        Id = isAuthorizedProposalEntrance ? entranceValue.Id! : IdGenerator.Generate()
                    };
                }
                else
                {
                    entrance = entity.Entrances.FirstOrDefault(e => e.Id == entranceValue.Id);
                }

                if (entrance == null)
                    throw ApiExceptionDictionary.BadRequest("The selected Entrance does not belong to this Cave.");

                entrance.Name = entranceValue.Name;
                entrance.LocationQualityTagId = entranceValue.LocationQualityTagId;
                entrance.Description = entranceValue.Description;
                entrance.ReportedOn = entranceValue.ReportedOn?.ToUtcKind();
                entrance.PitDepthFeet = entranceValue.PitFeet;

                entrance.ReportedOn = entrance.ReportedOn?.ToUtcKind();

                entrance.Location =
                    new Point(entranceValue.Longitude, entranceValue.Latitude, entranceValue.ElevationFeet)
                        { SRID = 4326 };

                // this can only be called if the entrance already exists otherwise SaveChanges throws a
                // Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException because it's expecting to update something
                if (!isNewEntrance)
                {
                    // if the z value was not provided during import, ef core doesn't realize that the z value was added later.
                    Repository.SetPropertiesModified(entrance, e => e.Location);
                }

                SyncTags(entrance.EntranceStatusTags, entranceValue.EntranceStatusTagIds,
                    tag => tag.TagTypeId,
                    tagId => new EntranceStatusTag { EntranceId = entrance.Id, TagTypeId = tagId });
                SyncTags(entrance.FieldIndicationTags, entranceValue.FieldIndicationTagIds,
                    tag => tag.TagTypeId,
                    tagId => new FieldIndicationTag { EntranceId = entrance.Id, TagTypeId = tagId });
                SyncTags(entrance.EntranceOtherTags, entranceValue.EntranceOtherTagIds,
                    tag => tag.TagTypeId,
                    tagId => new EntranceOtherTag { EntranceId = entrance.Id, TagTypeId = tagId });
                SyncTags(entrance.EntranceHydrologyTags, entranceValue.EntranceHydrologyTagIds,
                    tag => tag.TagTypeId,
                    tagId => new EntranceHydrologyTag { EntranceId = entrance.Id, TagTypeId = tagId });

                RemoveUnselected(entrance.EntranceReportedByNameTags, entranceValue.ReportedByNameTagIds,
                    tag => tag.TagTypeId);
                foreach (var personTagTypeId in entranceValue.ReportedByNameTagIds)
                {
                    if (entrance.EntranceReportedByNameTags.Any(tag => tag.TagTypeId == personTagTypeId)) continue;
                    var binding = peopleTagsBySelection[personTagTypeId];
                    var tag = new EntranceReportedByNameTag()
                    {
                        EntranceId = entrance.Id,
                        TagTypeId = binding.Id,
                        TagType = binding.NewTag
                    };
                    entrance.EntranceReportedByNameTags.Add(tag);

                }

                entrance.IsPrimary = entranceValue.IsPrimary;

                if (isNewEntrance) entity.Entrances.Add(entrance);
            }

            var desiredLinePlots = (values.LinePlots ?? []).ToList();
            var desiredExistingLinePlotIds = desiredLinePlots
                .Where(linePlot => !string.IsNullOrWhiteSpace(linePlot.Id))
                .Select(linePlot => linePlot.Id!)
                .ToHashSet(StringComparer.Ordinal);
            var currentLinePlotsById = entity.GeoJsons.ToDictionary(linePlot => linePlot.Id, StringComparer.Ordinal);
            foreach (var linePlotValue in desiredLinePlots)
            {
                CaveGeoJson linePlot;
                if (string.IsNullOrWhiteSpace(linePlotValue.Id))
                {
                    linePlot = new CaveGeoJson { CaveId = entity.Id };
                    entity.GeoJsons.Add(linePlot);
                }
                else if (currentLinePlotsById.TryGetValue(linePlotValue.Id, out var currentLinePlot))
                {
                    linePlot = currentLinePlot;
                }
                else if (authorizedNewLinePlotIds?.Contains(linePlotValue.Id) == true)
                {
                    linePlot = new CaveGeoJson { Id = linePlotValue.Id, CaveId = entity.Id };
                    entity.GeoJsons.Add(linePlot);
                }
                else
                {
                    throw ApiExceptionDictionary.BadRequest(
                        "The selected line plot does not belong to this Cave.");
                }

                if (!string.Equals(linePlot.Name, linePlotValue.Name, StringComparison.Ordinal))
                    linePlot.Name = linePlotValue.Name;
                var currentNormalizedGeoJson = string.IsNullOrWhiteSpace(linePlot.GeoJson)
                    ? null
                    : CaveJsonContent.Normalize(linePlot.GeoJson);
                if (!string.Equals(currentNormalizedGeoJson, linePlotValue.GeoJson, StringComparison.Ordinal))
                    linePlot.GeoJson = linePlotValue.GeoJson;
            }

            foreach (var currentLinePlot in entity.GeoJsons
                         .Where(linePlot => currentLinePlotsById.ContainsKey(linePlot.Id) &&
                                            !desiredExistingLinePlotIds.Contains(linePlot.Id))
                         .ToList())
            {
                entity.GeoJsons.Remove(currentLinePlot);
                Repository.RemoveCaveGeoJson(currentLinePlot);
            }

            if (isNew) Repository.Add(entity);

            var desiredFiles = (values.Files ?? []).ToList();
            var desiredFileIds = desiredFiles.Select(file => file.Id).ToHashSet(StringComparer.Ordinal);
            var missingDesiredFileIds = desiredFileIds
                .Except(entity.Files.Select(file => file.Id), StringComparer.Ordinal)
                .Order(StringComparer.Ordinal).ToList();

            if (missingDesiredFileIds.Count > 0)
            {
                IReadOnlyList<File> stagedFiles;
                if (changeRequestId is not null)
                {
                    var publications = (stagedFilePublications ?? [])
                        .Where(publication => missingDesiredFileIds.Contains(publication.FileId, StringComparer.Ordinal))
                        .ToList();
                    if (publications.Count != missingDesiredFileIds.Count ||
                        publications.Select(publication => publication.FileId)
                            .Distinct(StringComparer.Ordinal).Count() != missingDesiredFileIds.Count)
                        throw ApiExceptionDictionary.BadRequest(
                            "The proposal staged-file set does not match the desired Cave files.");
                    stagedFiles = await Repository.AttachStagedFilesAsync(changeRequestId, entity.Id,
                        publications, cancellationToken);
                }
                else
                {
                    stagedFiles = await Repository.AttachAuthoringStagedFilesAsync(entity.Id,
                        missingDesiredFileIds, cancellationToken);
                }

                foreach (var stagedFile in stagedFiles)
                    if (entity.Files.All(file => file.Id != stagedFile.Id)) entity.Files.Add(stagedFile);
            }

            foreach (var file in desiredFiles)
            {
                var fileEntity = entity.Files.FirstOrDefault(candidate => candidate.Id == file.Id);
                if (fileEntity == null) throw ApiExceptionDictionary.NotFound("File");

                if (!string.IsNullOrWhiteSpace(file.Name) &&
                    !string.Equals(file.Name, fileEntity.Name, StringComparison.Ordinal))
                {
                    try
                    {
                        FileNamePolicy.ComposeEditableName(fileEntity.Name, file.Name, fileEntity.Extension);
                    }
                    catch (ArgumentException exception)
                    {
                        throw ApiExceptionDictionary.BadRequest(exception.Message);
                    }
                    fileEntity.Name = file.Name;
                }

                if (!string.IsNullOrWhiteSpace(file.FileTypeTagId) &&
                    !string.Equals(file.FileTypeTagId, fileEntity.FileTypeTagId, StringComparison.Ordinal))
                    fileEntity.FileTypeTagId = file.FileTypeTagId;
            }

            foreach (var fileEntity in entity.Files.Where(file => !desiredFileIds.Contains(file.Id)).ToList())
            {
                await Repository.RetainPublishedFileObjectAsync(fileEntity, entity.Id, cancellationToken);
                entity.Files.Remove(fileEntity);
                Repository.Delete(fileEntity);
            }

            // Tag association rows use identifying composite keys and can be resurrected by EF relationship fixup.
            // Delete removed join rows explicitly inside this transaction, then detach their stale tracked instances.
            await Repository.DeleteTagAssociationsAsync(entity.Id, removedTagAssociations, cancellationToken);

            try
            {
                await Repository.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException exception) when (changeRequestId is not null &&
                exception.Entries.Any(entry => entry.Entity is Cave))
            {
                throw new CaveRevisionConflictException(entity.Id, expectedRevisionId, null);
            }

            CaveMutationResult mutationResult;
            if (isNew)
            {
                mutationResult = await _caveMutationCoordinator.PublishPersistedNewAsync(
                    entity.Id, revisionSource, CaveRevisionOperation.Create, changeRequestId,
                    cancellationToken: cancellationToken);
            }
            else
            {
                mutationResult = await _caveMutationCoordinator.PublishPreparedAsync(
                    revisionPreparation!, revisionSource, CaveRevisionOperation.Update, changeRequestId,
                    cancellationToken: cancellationToken);
            }

            if (beforeCommit is not null) await beforeCommit(mutationResult, cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            savedCaveId = entity.Id;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return savedCaveId;
    }

    private sealed record PeopleTagBinding(string Id, TagType? NewTag);
    private sealed record ResolvedTagMutationBoundary(
        IReadOnlyDictionary<string, PeopleTagBinding> PeopleBindings,
        IReadOnlyList<string> TargetExistingIds);

    private async Task<ResolvedTagMutationBoundary> ValidateAndNormalizeTagReferencesAsync(AddCaveVm values,
        CavePeoplePublicationContext? publicationContext, CancellationToken cancellationToken)
    {
        var caveGroups = CaveTagReferencePolicy.CaveGroups(values).ToList();
        var entranceGroups = values.Entrances.Select(entrance =>
            (Entrance: entrance, Groups: CaveTagReferencePolicy.EntranceGroups(entrance).ToList())).ToList();
        var allIds = caveGroups.SelectMany(group => group.Values)
            .Concat(entranceGroups.SelectMany(item => item.Groups).SelectMany(group => group.Values))
            .Concat(values.Entrances.Select(entrance => entrance.LocationQualityTagId))
            .Concat((values.Files ?? []).Select(file => file.FileTypeTagId!)).ToList();
        var idCandidates = await Repository.GetTagCandidatesByIdsAsync(allIds, cancellationToken);
        var byId = idCandidates.ToDictionary(candidate => candidate.Id, StringComparer.Ordinal);
        var unresolvedPeopleNames = publicationContext is null
            ? caveGroups.Concat(entranceGroups.SelectMany(item => item.Groups))
                .Where(group => CaveTagReferencePolicy.AllowsNewPeopleIntent(group.Role))
                .SelectMany(group => group.Values)
                .Where(value => !string.IsNullOrWhiteSpace(value) && !byId.ContainsKey(value.Trim()))
            : publicationContext.Cave.NewPeople.Select(intent => intent.Name)
                .Concat(publicationContext.Entrances.Values.SelectMany(scope => scope.NewPeople)
                    .Select(intent => intent.Name));
        var distinctUnresolvedPeopleNames = unresolvedPeopleNames
            .Select(name => name.Trim())
            .Distinct(TagNameMatchPolicy.IdentityComparer)
            .ToList();
        IReadOnlyList<TagNameCandidate> peopleNameCandidates = distinctUnresolvedPeopleNames.Count == 0
            ? []
            : await Repository.GetEligiblePeopleCandidatesAsync(cancellationToken);
        ResolvedCaveTagReferences ResolveDirect(
            IEnumerable<(SnapshotTagRole Role, IEnumerable<string> Values)> groups) =>
            CaveTagReferencePolicy.Resolve(groups, idCandidates, peopleNameCandidates, RequestUser.AccountId!);
        var caveResolved = publicationContext is null
            ? ResolveDirect(caveGroups)
            : ResolvePublishedPeople(caveGroups, publicationContext.Cave,
                SnapshotTagRole.Cartographer, SnapshotTagRole.CaveReportedBy);
        var entranceResolved = entranceGroups.Select(item => (item.Entrance,
            Resolved: publicationContext is null
                ? ResolveDirect(item.Groups)
                : ResolvePublishedPeople(item.Groups,
                    publicationContext.Entrances.GetValueOrDefault(item.Entrance.Id ?? string.Empty) ??
                    throw ApiExceptionDictionary.BadRequest(
                        "The proposal People bindings do not match its Entrances."),
                    SnapshotTagRole.EntranceReportedBy))).ToList();
        if (publicationContext is not null &&
            publicationContext.Entrances.Keys.Except(values.Entrances.Select(entrance => entrance.Id ?? string.Empty),
                StringComparer.Ordinal).Any())
            throw ApiExceptionDictionary.BadRequest("The proposal People bindings do not match its Entrances.");
        if (publicationContext is not null && values.Entrances.Select(entrance => entrance.Id ?? string.Empty)
            .Except(publicationContext.Entrances.Keys,
                StringComparer.Ordinal).Any())
            throw ApiExceptionDictionary.BadRequest("The proposal People bindings do not match its Entrances.");

        foreach (var entrance in values.Entrances)
            CaveTagReferencePolicy.RequireTypedReference(entrance.LocationQualityTagId,
                TagTypeKeyConstant.LocationQuality, byId, RequestUser.AccountId!, "Location Quality");
        foreach (var file in values.Files ?? [])
            CaveTagReferencePolicy.RequireTypedReference(file.FileTypeTagId!, TagTypeKeyConstant.File,
                byId, RequestUser.AccountId!, "file type");

        values.GeologyTagIds = Existing(caveResolved, SnapshotTagRole.Geology);
        values.GeologicAgeTagIds = Existing(caveResolved, SnapshotTagRole.GeologicAge);
        values.MapStatusTagIds = Existing(caveResolved, SnapshotTagRole.MapStatus);
        values.PhysiographicProvinceTagIds = Existing(caveResolved, SnapshotTagRole.PhysiographicProvince);
        values.ArcheologyTagIds = Existing(caveResolved, SnapshotTagRole.Archeology);
        values.BiologyTagIds = Existing(caveResolved, SnapshotTagRole.Biology);
        values.OtherTagIds = Existing(caveResolved, SnapshotTagRole.CaveOther);
        foreach (var item in entranceResolved)
        {
            item.Entrance.EntranceStatusTagIds = Existing(item.Resolved, SnapshotTagRole.EntranceStatus);
            item.Entrance.EntranceHydrologyTagIds = Existing(item.Resolved, SnapshotTagRole.EntranceHydrology);
            item.Entrance.FieldIndicationTagIds = Existing(item.Resolved, SnapshotTagRole.FieldIndication);
            item.Entrance.EntranceOtherTagIds = Existing(item.Resolved, SnapshotTagRole.EntranceOther);
        }

        var existingPeopleIds = caveResolved.Existing.Concat(entranceResolved.SelectMany(item => item.Resolved.Existing))
            .Where(reference => CaveTagReferencePolicy.AllowsNewPeopleIntent(reference.Role))
            .Select(reference => reference.TagTypeId).Distinct(StringComparer.Ordinal).ToList();
        var newIntents = caveResolved.NewPeople.Concat(entranceResolved.SelectMany(item => item.Resolved.NewPeople))
            .ToList();
        var peopleByName = new Dictionary<string, PeopleTagBinding>(TagNameMatchPolicy.IdentityComparer);
        foreach (var name in newIntents.Select(intent => intent.Name)
                     .Distinct(TagNameMatchPolicy.IdentityComparer))
        {
            var match = TagNameMatchPolicy.Select(name, RequestUser.AccountId!, peopleNameCandidates);
            if (match is not null)
            {
                peopleByName[name] = new PeopleTagBinding(match.Id, null);
                continue;
            }

            var created = new TagType
            {
                Name = name, AccountId = RequestUser.AccountId, Key = TagTypeKeyConstant.People
            };
            peopleByName[name] = new PeopleTagBinding(created.Id, created);
        }
        var candidateIds = peopleNameCandidates.Select(candidate => candidate.Id).ToHashSet(StringComparer.Ordinal);
        var selectedExistingIds = caveResolved.Existing
            .Concat(entranceResolved.SelectMany(item => item.Resolved.Existing))
            .Select(reference => reference.TagTypeId)
            .Concat(values.Entrances.Select(entrance => entrance.LocationQualityTagId))
            .Concat((values.Files ?? []).Select(file => file.FileTypeTagId!))
            .Concat(peopleByName.Values.Where(binding => candidateIds.Contains(binding.Id)).Select(binding => binding.Id))
            .Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToList();
        var result = existingPeopleIds.ToDictionary(id => id, id => new PeopleTagBinding(id, null),
            StringComparer.Ordinal);
        foreach (var binding in peopleByName.Values)
            result.TryAdd(binding.Id, binding);

        values.CartographerNameTagIds = People(caveResolved, SnapshotTagRole.Cartographer);
        values.ReportedByNameTagIds = People(caveResolved, SnapshotTagRole.CaveReportedBy);
        foreach (var item in entranceResolved)
            item.Entrance.ReportedByNameTagIds = People(item.Resolved, SnapshotTagRole.EntranceReportedBy);
        return new ResolvedTagMutationBoundary(result, selectedExistingIds);

        static IReadOnlyList<string> Existing(ResolvedCaveTagReferences resolved, SnapshotTagRole role) =>
            resolved.Existing.Where(tag => tag.Role == role).Select(tag => tag.TagTypeId).ToList();
        IReadOnlyList<string> People(ResolvedCaveTagReferences resolved, SnapshotTagRole role) =>
            Existing(resolved, role).Concat(resolved.NewPeople.Where(tag => tag.Role == role)
                .Select(tag => peopleByName[tag.Name].Id)).Distinct(StringComparer.Ordinal).ToList();

        ResolvedCaveTagReferences ResolvePublishedPeople(
            IEnumerable<(SnapshotTagRole Role, IEnumerable<string> Values)> groups,
            CavePeoplePublicationScope people, params SnapshotTagRole[] allowedRoles)
        {
            var allowed = allowedRoles.ToHashSet();
            if (people.Existing.Any(reference => !allowed.Contains(reference.Role)) ||
                people.NewPeople.Any(intent => !allowed.Contains(intent.Role)))
                throw ApiExceptionDictionary.BadRequest("The proposal People bindings contain an invalid role.");
            var nonPeople = ResolveDirect(groups.Where(group => !CaveTagReferencePolicy
                .AllowsNewPeopleIntent(group.Role)));
            var existing = new List<SnapshotTagReference>(nonPeople.Existing);
            foreach (var reference in people.Existing)
            {
                if (!byId.TryGetValue(reference.TagTypeId, out var candidate) ||
                    candidate.Key != TagTypeKeyConstant.People ||
                    (!candidate.IsDefault && candidate.AccountId != RequestUser.AccountId))
                    throw ApiExceptionDictionary.BadRequest(
                        $"The recorded People tag is no longer valid for {reference.Role}.");
                existing.Add(new SnapshotTagReference(reference.Role, candidate.Id, candidate.Name));
            }
            var newPeople = people.NewPeople.Select(intent =>
            {
                CaveTagReferencePolicy.ValidateNewPeopleName(intent.Name);
                return intent with { Name = intent.Name.Trim() };
            }).ToList();
            return new ResolvedCaveTagReferences(existing, newPeople);
        }
    }

    private static IReadOnlyList<string> CollectExistingStableTagTypeIds(Cave cave)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        static void Add<T>(HashSet<string> target, IEnumerable<T> tags, Func<T, string> id)
        {
            foreach (var tag in tags) target.Add(id(tag));
        }

        Add(ids, cave.GeologyTags, tag => tag.TagTypeId);
        Add(ids, cave.GeologicAgeTags, tag => tag.TagTypeId);
        Add(ids, cave.MapStatusTags, tag => tag.TagTypeId);
        Add(ids, cave.PhysiographicProvinceTags, tag => tag.TagTypeId);
        Add(ids, cave.ArcheologyTags, tag => tag.TagTypeId);
        Add(ids, cave.BiologyTags, tag => tag.TagTypeId);
        Add(ids, cave.CaveOtherTags, tag => tag.TagTypeId);
        Add(ids, cave.CartographerNameTags, tag => tag.TagTypeId);
        Add(ids, cave.CaveReportedByNameTags, tag => tag.TagTypeId);
        foreach (var entrance in cave.Entrances)
        {
            Add(ids, entrance.EntranceStatusTags, tag => tag.TagTypeId);
            Add(ids, entrance.EntranceHydrologyTags, tag => tag.TagTypeId);
            Add(ids, entrance.FieldIndicationTags, tag => tag.TagTypeId);
            Add(ids, entrance.EntranceOtherTags, tag => tag.TagTypeId);
            Add(ids, entrance.EntranceReportedByNameTags, tag => tag.TagTypeId);
            ids.Add(entrance.LocationQualityTagId);
        }
        foreach (var file in cave.Files) ids.Add(file.FileTypeTagId);
        return ids.Where(id => !string.IsNullOrWhiteSpace(id)).Order(StringComparer.Ordinal).ToList();
    }

    public async Task<CaveVm?> GetCave(string caveId)
    {
        var cave = await Repository.GetCave(caveId);

        if (cave == null) throw ApiExceptionDictionary.NotFound("Cave");

        return cave;
    }

    public async Task DeleteCave(string caveId, CancellationToken cancellationToken,
        IDbContextTransaction? transaction = null, List<File>? deferredFileDeletes = null,
        List<StorageObjectAddress>? deferredObjectDeletes = null)
    {
        var outsideTransaction = transaction != null;
        transaction ??= await Repository.BeginTransactionAsync(cancellationToken);

        var files = new List<File>();
        var retainedObjects = new List<StorageObjectAddress>();
        var isSuccessful = false;
        try
        {
            await Repository.LockForHardDeleteAsync(caveId, cancellationToken);
            var entity = await Repository.GetAsync(caveId);

            if (entity == null) throw ApiExceptionDictionary.NotFound(nameof(entity.Id));
            await RequestUser.HasCavePermission(PermissionPolicyKey.Manager, caveId, entity.CountyId, entity.StateId);
            if (await Repository.HasPendingChangeRequestsAsync(caveId, cancellationToken))
                throw ApiExceptionDictionary.BadRequest(
                    "Pending proposed changes must be approved or rejected before this Cave can be deleted.");

            var revisionPreparation = await _caveMutationCoordinator.PrepareExistingAsync(
                entity.Id, entity.CurrentRevisionId, cancellationToken);

            var geoJsons = await Repository.GetCaveGeoJsonsAsync(caveId);
            foreach (var geoJson in geoJsons)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Repository.RemoveCaveGeoJson(geoJson);
            }

            await Repository.SaveChangesAsync(cancellationToken);

            foreach (var entrance in entity.Entrances)
            {
                cancellationToken.ThrowIfCancellationRequested();
                foreach (var tag in entrance.EntranceStatusTags)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Repository.Delete(tag);
                }

                await Repository.SaveChangesAsync(cancellationToken);

                foreach (var tag in entrance.EntranceHydrologyTags)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Repository.Delete(tag);
                }

                await Repository.SaveChangesAsync(cancellationToken);

                foreach (var tag in entrance.FieldIndicationTags)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Repository.Delete(tag);
                }

                await Repository.SaveChangesAsync(cancellationToken);

                foreach (var tag in entrance.EntranceReportedByNameTags)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Repository.Delete(tag);
                }

                await Repository.SaveChangesAsync(cancellationToken);

                foreach (var tag in entrance.EntranceOtherTags)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Repository.Delete(tag);
                }

                await Repository.SaveChangesAsync(cancellationToken);

                Repository.Delete(entrance);
            }

            await Repository.SaveChangesAsync(cancellationToken);


            foreach (var tag in entity.GeologyTags)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Repository.Delete(tag);
            }

            await Repository.SaveChangesAsync(cancellationToken);

            foreach (var tag in entity.MapStatusTags)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Repository.Delete(tag);
            }

            await Repository.SaveChangesAsync(cancellationToken);

            foreach (var tag in entity.GeologicAgeTags)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Repository.Delete(tag);
            }

            await Repository.SaveChangesAsync(cancellationToken);

            foreach (var tag in entity.PhysiographicProvinceTags)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Repository.Delete(tag);
            }

            await Repository.SaveChangesAsync(cancellationToken);

            foreach (var tag in entity.BiologyTags)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Repository.Delete(tag);
            }

            await Repository.SaveChangesAsync(cancellationToken);

            foreach (var tag in entity.ArcheologyTags)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Repository.Delete(tag);
            }

            await Repository.SaveChangesAsync(cancellationToken);

            foreach (var tag in entity.CartographerNameTags)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Repository.Delete(tag);
            }

            await Repository.SaveChangesAsync(cancellationToken);

            foreach (var tag in entity.CaveReportedByNameTags)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Repository.Delete(tag);
            }

            await Repository.SaveChangesAsync(cancellationToken);

            foreach (var tag in entity.CaveOtherTags)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Repository.Delete(tag);
            }

            await Repository.SaveChangesAsync(cancellationToken);

            foreach (var favorite in entity.Favorites)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Repository.Delete(favorite);
            }

            await Repository.SaveChangesAsync(cancellationToken);

            files = entity.Files.ToList();
            retainedObjects = (await Repository.RemoveRetainedFileObjectsForHardDeleteAsync(
                caveId, cancellationToken)).ToList();

            foreach (var permission in entity.CavePermissions)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Repository.Delete(permission);
            }

            await Repository.DeleteStagedFileReferencesAsync(files.Select(file => file.Id), cancellationToken);

            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Repository.Delete(file);
            }

            await Repository.SaveChangesAsync(cancellationToken);

            Repository.Delete(entity);
            await Repository.SaveChangesAsync(cancellationToken);
            await _caveMutationCoordinator.PublishPreparedDeleteAsync(
                revisionPreparation, CaveRevisionSource.ManagerEdit, cancellationToken: cancellationToken);

            if (!outsideTransaction)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            isSuccessful = true;
        }
        catch (Exception)
        {
            if (!outsideTransaction)
            {
                await transaction.RollbackAsync(cancellationToken);
            }

            throw;
        }

        if (isSuccessful)
        {
            var objectAddresses = files
                .Where(file => !string.IsNullOrWhiteSpace(file.BlobKey) &&
                               !string.IsNullOrWhiteSpace(file.BlobContainer))
                .Select(file => new StorageObjectAddress(file.BlobContainer!, file.BlobKey!))
                .Concat(retainedObjects)
                .Distinct()
                .ToList();

            if (outsideTransaction)
            {
                deferredFileDeletes?.AddRange(files);
                deferredObjectDeletes?.AddRange(objectAddresses);
                // The caller owns commit/rollback. External object deletion cannot be
                // made safe here before that decision; production hard delete uses the
                // internally-owned transaction path below.
            }
            else
            {
                foreach (var address in objectAddresses)
                    await _fileService.DeleteObjectBestEffortAsync(address);
            }
        }
    }

    public async Task ArchiveCave(string caveId)
    {

        var entity = await Repository.GetAsync(caveId);
        if (entity == null) throw ApiExceptionDictionary.NotFound(nameof(entity.Id));

        await RequestUser.HasCavePermission(PermissionPolicyKey.Manager, caveId, entity.CountyId, entity.StateId);


        await _caveMutationCoordinator.PublishExistingAsync(
            caveId, entity.CurrentRevisionId, CaveRevisionSource.ManagerEdit, CaveRevisionOperation.Archive,
            cave => cave.IsArchived = true);
    }

    public async Task UnarchiveCave(string caveId)
    {
        var entity = await Repository.GetAsync(caveId);

        if (entity == null) throw ApiExceptionDictionary.NotFound(nameof(entity.Id));
        await RequestUser.HasCavePermission(PermissionPolicyKey.Manager, caveId, entity.CountyId, entity.StateId);

        await _caveMutationCoordinator.PublishExistingAsync(
            caveId, entity.CurrentRevisionId, CaveRevisionSource.ManagerEdit, CaveRevisionOperation.Unarchive,
            cave => cave.IsArchived = false);
    }

    #endregion


    #region Favorite Cave

    public async Task<PagedResult<FavoriteVm>> GetFavoriteCaves(FilterQuery query)
    {
        var caves = await Repository.GetFavoriteCaves(query);
        return caves;
    }

    public async Task FavoriteCave(string caveId)
    {
        if (string.IsNullOrWhiteSpace(RequestUser.AccountId)) throw ApiExceptionDictionary.NoAccount;

        var entity = await Repository.GetAsync(caveId);
        if (entity == null) throw ApiExceptionDictionary.NotFound(nameof(entity.Id));

        var favorite = await Repository.GetFavoriteCave(caveId);

        var isNew = favorite == null;
        favorite ??= new Favorite();

        favorite.CaveId = caveId;
        favorite.UserId = RequestUser.Id;
        favorite.AccountId = RequestUser.AccountId;

        if (isNew)
        {
            Repository.Add(favorite);
        }

        await Repository.SaveChangesAsync();
    }

    public async Task UnfavoriteCave(string caveId)
    {
        if (string.IsNullOrWhiteSpace(RequestUser.AccountId)) throw ApiExceptionDictionary.NoAccount;

        var favorite = await Repository.GetFavoriteCave(caveId);
        if (favorite == null) throw ApiExceptionDictionary.NotFound(nameof(favorite.Id));

        Repository.Delete(favorite);
        await Repository.SaveChangesAsync();
    }

    public async Task<FavoriteVm?> GetFavoriteCave(string caveId)
    {
        if (string.IsNullOrWhiteSpace(RequestUser.AccountId)) throw ApiExceptionDictionary.NoAccount;

        var favorite = await Repository.GetFavoriteCaveVm(caveId);
        return favorite;
    }

    #endregion

}
