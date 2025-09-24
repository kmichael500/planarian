using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using CsvHelper;
using Microsoft.EntityFrameworkCore.Storage;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using Newtonsoft.Json.Linq;
using Planarian.Library.Exceptions;
using Planarian.Library.Extensions.DateTime;
using Planarian.Library.Extensions.String;
using Planarian.Library.Options;
using Planarian.Model.Database.Entities;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;
using Planarian.Modules.Caves.Models;
using Planarian.Modules.Caves.Repositories;
using Planarian.Modules.FeatureSettings.Repositories;
using Planarian.Modules.Files.Repositories;
using Planarian.Modules.Files.Services;
using Planarian.Modules.Query.Extensions;
using Planarian.Modules.Query.Models;
using Planarian.Modules.Tags.Repositories;
using Planarian.Shared.Base;
using File = Planarian.Model.Database.Entities.RidgeWalker.File;

namespace Planarian.Modules.Caves.Services;

public class CaveService : ServiceBase<CaveRepository>
{
    private readonly FileService _fileService;
    private readonly FileRepository _fileRepository;
    private readonly TagRepository _tagRepository;
    private readonly FeatureSettingRepository _featureSettingRepository;
    private readonly ServerOptions _serverOptions;

    public CaveService(CaveRepository repository, RequestUser requestUser, FileService fileService,
        FileRepository fileRepository, TagRepository tagRepository,
        FeatureSettingRepository featureSettingRepository, ServerOptions serverOptions) : base(
        repository, requestUser)
    {
        _fileService = fileService;
        _fileRepository = fileRepository;
        _tagRepository = tagRepository;
        _featureSettingRepository = featureSettingRepository;
        _serverOptions = serverOptions;
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
                descriptionStringBuilder.AppendLine($"{_serverOptions.ClientBaseUrl}/caves/{cave.Id}");

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
        using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
        csv.Context.RegisterClassMap(new CaveEntranceCsvModelMap(featureDict, exportFieldSet));
        csv.WriteRecords(csvRecords);
        writer.Flush();
        return memoryStream.ToArray();
    }

    public async Task<string> AddCave(AddCaveVm values, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(RequestUser.AccountId)) throw ApiExceptionDictionary.NoAccount;

        await RequestUser.HasCavePermission(PermissionPolicyKey.Manager, values.Id, values.CountyId);
        var isNew = string.IsNullOrWhiteSpace(values.Id);

        await RequestUser.HasCavePermission(PermissionPolicyKey.Manager, values.Id, values.CountyId);


        #region Data Validation

        // must be at least one entrance
        if (values.Entrances == null || !values.Entrances.Any())
            throw ApiExceptionDictionary.EntranceRequired("At least 1 entrance is required!");

        if (values.NumberOfPits < 0)
            throw ApiExceptionDictionary.BadRequest("Number of pits must be greater than or equal to 1!");

        if (values.LengthFeet < 0)
            throw ApiExceptionDictionary.BadRequest("Length must be greater than or equal to 0!");

        if (values.DepthFeet < 0)
            throw ApiExceptionDictionary.BadRequest("Depth must be greater than or equal to 0!");

        if (values.MaxPitDepthFeet < 0)
            throw ApiExceptionDictionary.BadRequest("Max pit depth must be greater than or equal to 0!");

        if (values.Entrances.Any(e => e.Latitude > 90 || e.Latitude < -90))
            throw ApiExceptionDictionary.BadRequest("Latitude must be between -90 and 90!");

        if (values.Entrances.Any(e => e.Longitude > 180 || e.Longitude < -180))
            throw ApiExceptionDictionary.BadRequest("Longitude must be between -180 and 180!");

        if (values.Entrances.Any(e => e.ElevationFeet < 0))
            throw ApiExceptionDictionary.BadRequest("Elevation must be greater than or equal to 0!");

        if (values.Entrances.Any(e => e.PitFeet < 0))
            throw ApiExceptionDictionary.BadRequest("Pit depth must be greater than or equal to 0!");

        var numberOfPrimaryEntrances = values.Entrances.Count(e => e.IsPrimary);

        if (numberOfPrimaryEntrances == 0)
            throw ApiExceptionDictionary.BadRequest("One entrance must be marked as primary!");

        if (numberOfPrimaryEntrances != 1)
            throw ApiExceptionDictionary.BadRequest("Only one entrance can be marked as primary!");

        #endregion


        await using var transaction = await Repository.BeginTransactionAsync(cancellationToken);
        try
        {
            var entity = isNew ? new Cave() : await Repository.GetAsync(values.Id);

            if (entity == null) throw ApiExceptionDictionary.NotFound(nameof(entity.Id));

            var isNewCounty = entity.CountyId != values.CountyId;

            entity.Name = values.Name.Trim();
            entity.SetAlternateNamesList(values.AlternateNames.Select(e => e.Trim()));

            entity.CountyId = values.CountyId.Trim();
            entity.StateId = values.StateId.Trim();
            entity.LengthFeet = values.LengthFeet;
            entity.DepthFeet = values.DepthFeet;
            entity.MaxPitDepthFeet = values.MaxPitDepthFeet;
            entity.NumberOfPits = values.NumberOfPits;
            entity.Narrative = values.Narrative?.Trim();
            entity.ReportedOn = values.ReportedOn?.ToUtcKind();
            entity.AccountId = RequestUser.AccountId;

            entity.GeologyTags.Clear();
            foreach (var tagId in values.GeologyTagIds)
            {
                var tag = new GeologyTag()
                {
                    TagTypeId = tagId
                };
                entity.GeologyTags.Add(tag);
            }

            entity.ArcheologyTags.Clear();
            foreach (var tagId in values.ArcheologyTagIds)
            {
                var tag = new ArcheologyTag()
                {
                    TagTypeId = tagId
                };
                entity.ArcheologyTags.Add(tag);
            }

            entity.BiologyTags.Clear();
            foreach (var tagId in values.BiologyTagIds)
            {
                var tag = new BiologyTag()
                {
                    TagTypeId = tagId
                };
                entity.BiologyTags.Add(tag);
            }

            entity.CartographerNameTags.Clear();
            foreach (var personTagTypeId in values.CartographerNameTagIds)
            {

                var tagType = await _tagRepository.GetTag(personTagTypeId);
                tagType ??= new TagType
                {
                    Name = personTagTypeId.Trim(),
                    AccountId = RequestUser.AccountId,
                    Key = TagTypeKeyConstant.People,
                };
                var tag = new CartographerNameTag
                {
                    TagType = tagType
                };

                entity.CartographerNameTags.Add(tag);
            }

            entity.MapStatusTags.Clear();
            foreach (var tagId in values.MapStatusTagIds)
            {
                var tag = new MapStatusTag()
                {
                    TagTypeId = tagId
                };
                entity.MapStatusTags.Add(tag);
            }

            entity.GeologicAgeTags.Clear();
            foreach (var tagId in values.GeologicAgeTagIds)
            {
                var tag = new GeologicAgeTag()
                {
                    TagTypeId = tagId
                };
                entity.GeologicAgeTags.Add(tag);
            }

            entity.PhysiographicProvinceTags.Clear();
            foreach (var tagId in values.PhysiographicProvinceTagIds)
            {
                var tag = new PhysiographicProvinceTag()
                {
                    TagTypeId = tagId
                };
                entity.PhysiographicProvinceTags.Add(tag);
            }

            entity.CaveOtherTags.Clear();
            foreach (var tagId in values.OtherTagIds)
            {
                var tag = new CaveOtherTag()
                {
                    TagTypeId = tagId
                };
                entity.CaveOtherTags.Add(tag);
            }

            entity.CaveReportedByNameTags.Clear();
            foreach (var personTagTypeId in values.ReportedByNameTagIds)
            {
                var tagType = await _tagRepository.GetTag(personTagTypeId);
                tagType ??= new TagType
                {
                    Name = personTagTypeId.Trim(),
                    AccountId = RequestUser.AccountId,
                    Key = TagTypeKeyConstant.People,
                };
                var tag = new CaveReportedByNameTag
                {
                    TagType = tagType
                };

                entity.CaveReportedByNameTags.Add(tag);
            }

            if (isNewCounty) entity.CountyNumber = await Repository.GetNewDisplayId(entity.CountyId);

            if (!isNew)
                // remove entrances
                foreach (var entrance in entity.Entrances.ToList())
                {
                    var entranceValue = values.Entrances.FirstOrDefault(e => e.Id == entrance.Id);

                    if (entranceValue != null) continue;
                    entity.Entrances.Remove(entrance);
                    Repository.Delete(entrance);
                }

            foreach (var entranceValue in values.Entrances)
            {
                var isNewEntrance = string.IsNullOrWhiteSpace(entranceValue.Id);

                var entrance = isNewEntrance
                    ? new Entrance()
                    : entity.Entrances.FirstOrDefault(e => e.Id == entranceValue.Id);

                if (entrance == null) throw ApiExceptionDictionary.NotFound(nameof(entranceValue.Id));

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

                entrance.EntranceStatusTags.Clear();
                foreach (var tagId in entranceValue.EntranceStatusTagIds)
                {
                    var tag = new EntranceStatusTag()
                    {
                        TagTypeId = tagId
                    };
                    entrance.EntranceStatusTags.Add(tag);
                }

                entrance.FieldIndicationTags.Clear();
                foreach (var tagId in entranceValue.FieldIndicationTagIds)
                {
                    var tag = new FieldIndicationTag()
                    {
                        TagTypeId = tagId
                    };
                    entrance.FieldIndicationTags.Add(tag);
                }

                entrance.EntranceOtherTags.Clear();
                foreach (var tagId in entranceValue.EntranceOtherTagIds)
                {
                    var tag = new EntranceOtherTag
                    {
                        TagTypeId = tagId
                    };
                    entrance.EntranceOtherTags.Add(tag);
                }

                entrance.EntranceHydrologyTags.Clear();
                foreach (var tagId in entranceValue.EntranceHydrologyTagIds)
                {
                    var tag = new EntranceHydrologyTag()
                    {
                        TagTypeId = tagId
                    };
                    entrance.EntranceHydrologyTags.Add(tag);
                }

                entrance.EntranceReportedByNameTags.Clear();
                foreach (var personTagTypeId in entranceValue.ReportedByNameTagIds)
                {
                    var peopleTag = await _tagRepository.GetTag(personTagTypeId);
                    peopleTag ??= new TagType
                    {
                        Name = personTagTypeId.Trim(),
                        AccountId = RequestUser.AccountId,
                        Key = TagTypeKeyConstant.People,
                    };
                    var tag = new EntranceReportedByNameTag()
                    {
                        TagType = peopleTag
                    };
                    entrance.EntranceReportedByNameTags.Add(tag);

                }

                entrance.IsPrimary = entranceValue.IsPrimary;

                if (isNewEntrance) entity.Entrances.Add(entrance);
            }

            var blobsToDelete = new List<File>();
            if (values.Files != null)
            {
                foreach (var file in values.Files)
                {
                    var fileEntity = entity.Files.FirstOrDefault(f => f.Id == file.Id);

                    if (fileEntity == null) throw ApiExceptionDictionary.NotFound("File");

                    if (!string.IsNullOrWhiteSpace(file.DisplayName) &&
                        !string.Equals(file.DisplayName, fileEntity.DisplayName))
                    {
                        fileEntity.DisplayName = file.DisplayName;
                        fileEntity.FileName = $"{file.DisplayName}{Path.GetExtension(fileEntity.FileName)}";
                    }

                    if (!string.IsNullOrWhiteSpace(file.FileTypeTagId) &&
                        !string.Equals(file.FileTypeTagId, fileEntity.FileTypeTagId))
                    {
                        var tagType = await _tagRepository.GetTag(file.FileTypeTagId);
                        if (tagType == null) throw ApiExceptionDictionary.NotFound("Tag");

                        fileEntity.FileTypeTagId = tagType.Id;
                    }
                }

                // check if any ids are missing from the request compared to the db and delete the ones that are missing
                var missingIds = entity.Files.Select(e => e.Id).Except(values.Files.Select(f => f.Id)).ToList();
                foreach (var missingId in missingIds)
                {
                    var fileEntity = entity.Files.FirstOrDefault(f => f.Id == missingId);
                    if (fileEntity == null) continue;

                    blobsToDelete.Add(fileEntity);
                    Repository.Delete(fileEntity);
                }
            }

            if (isNew) Repository.Add(entity);

            await Repository.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            foreach (var blobProperties in blobsToDelete)
                await _fileService.DeleteFile(blobProperties.BlobKey, blobProperties.BlobContainer);

            return entity.Id;


        }
        catch (Exception e)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

    }

    public async Task<CaveVm?> GetCave(string caveId)
    {
        var cave = await Repository.GetCave(caveId);

        if (cave == null) throw ApiExceptionDictionary.NotFound("Cave");

        foreach (var file in cave.Files)
        {
            var fileProperties = await _fileRepository.GetFileBlobProperties(file.Id);
            if (fileProperties == null || string.IsNullOrWhiteSpace(fileProperties.BlobKey) ||
                string.IsNullOrWhiteSpace(fileProperties.ContainerName)) continue;

            file.EmbedUrl =
                await _fileService.GetLink(fileProperties.BlobKey, fileProperties.ContainerName, file.FileName);
            file.DownloadUrl = await _fileService.GetLink(fileProperties.BlobKey, fileProperties.ContainerName,
                file.FileName, true);
        }

        return cave;
    }

    public async Task DeleteCave(string caveId, CancellationToken cancellationToken,
        IDbContextTransaction? transaction = null)
    {
        var outsideTransaction = transaction != null;
        transaction ??= await Repository.BeginTransactionAsync(cancellationToken);

        var files = new List<File>();
        var isSuccessful = false;
        try
        {
            var entity = await Repository.GetAsync(caveId);

            if (entity == null) throw ApiExceptionDictionary.NotFound(nameof(entity.Id));
            await RequestUser.HasCavePermission(PermissionPolicyKey.Manager, caveId, entity.CountyId);

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

            files = entity.Files.ToList();

            Repository.Delete(entity);
            await Repository.SaveChangesAsync(cancellationToken);

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
            foreach (var file in files) await _fileService.DeleteFile(file.BlobKey, file.BlobContainer);
        }
    }

    public async Task ArchiveCave(string caveId)
    {

        var entity = await Repository.GetAsync(caveId);
        if (entity == null) throw ApiExceptionDictionary.NotFound(nameof(entity.Id));

        await RequestUser.HasCavePermission(PermissionPolicyKey.Manager, caveId, entity.CountyId);


        entity.IsArchived = true;
        await Repository.SaveChangesAsync();
    }

    public async Task UnarchiveCave(string caveId)
    {
        var entity = await Repository.GetAsync(caveId);

        if (entity == null) throw ApiExceptionDictionary.NotFound(nameof(entity.Id));
        await RequestUser.HasCavePermission(PermissionPolicyKey.Manager, caveId, entity.CountyId);

        entity.IsArchived = false;
        await Repository.SaveChangesAsync();
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

    #region GeoJson

    public async Task UploadCaveGeoJson(string caveId, IEnumerable<GeoJsonUploadVm> geoJsonUploads,
        CancellationToken cancellationToken = default)
    {
        var cave = await Repository.GetCaveWithLinePlots(caveId);
        if (cave == null)
            throw ApiExceptionDictionary.NotFound("Cave");

        await RequestUser.HasCavePermission(PermissionPolicyKey.Manager, caveId, cave.CountyId);

        foreach (var oldGeoJson in cave.GeoJsons.ToList())
        {
            Repository.RemoveCaveGeoJson(oldGeoJson);
        }

        await using var transaction = await Repository.BeginTransactionAsync(cancellationToken);
        try
        {
            foreach (var uploadVm in geoJsonUploads)
            {
                var parsedToken = JToken.Parse(uploadVm.GeoJson);
                IList<JToken> featureCollections;
                // If the token is an array, treat each element as a FeatureCollection.
                if (parsedToken is JArray jArray)
                {
                    featureCollections = jArray.Children().ToList();
                }
                // If it’s a single FeatureCollection object, wrap it in a list.
                else if (parsedToken is JObject)
                {
                    featureCollections = new List<JToken> { parsedToken };
                }
                else
                {
                    throw ApiExceptionDictionary.BadRequest("Invalid GeoJSON format.");
                }

                // Process each FeatureCollection individually.
                foreach (var featureCollectionToken in featureCollections)
                {
                    // Get the features array from the current FeatureCollection.
                    var featuresArray = featureCollectionToken["features"] as JArray;
                    if (featuresArray == null)
                    {
                        throw ApiExceptionDictionary.BadRequest(
                            "GeoJSON feature collection does not contain any features.");
                    }

                    var geoJsonEntity = new CaveGeoJson
                    {
                        CaveId = caveId,
                        Name = uploadVm.Name,
                        GeoJson = featureCollectionToken.ToString(),
                    };

                    Repository.AddCaveGeoJson(geoJsonEntity);
                    cave.GeoJsons.Add(geoJsonEntity);
                }
            }

            await Repository.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    #region Example Geometry Code
    // public async Task UploadCaveGeoJson(string caveId, IEnumerable<GeoJsonUploadVm> geoJsonUploads,
    //     CancellationToken cancellationToken = default)
    // {
    //     // Retrieve the cave entity (your GetCaveWithLinePlots method already does permission filtering)
    //     var cave = await Repository.GetCaveWithLinePlots(caveId);
    //     if (cave == null)
    //         throw ApiExceptionDictionary.NotFound("Cave");

    //     // Check that the current user has the Manager-level permission for the cave
    //     await RequestUser.HasCavePermission(PermissionPolicyKey.Manager, caveId, cave.CountyId);

    //     // Clear out any old geojson entries for the cave.
    //     foreach (var oldGeoJson in cave.GeoJsons.ToList())
    //     {
    //         Repository.RemoveCaveGeoJson(oldGeoJson);
    //     }

    //     // Begin a transaction
    //     await using var transaction = await Repository.BeginTransactionAsync(cancellationToken);
    //     try
    //     {
    //         var reader = new NetTopologySuite.IO.GeoJsonReader();
    //         // Iterate over each upload.
    //         foreach (var uploadVm in geoJsonUploads)
    //         {
    //             // Parse the uploaded GeoJSON string.
    //             var parsedToken = JToken.Parse(uploadVm.GeoJson);
    //             IList<JToken> featureCollections;
    //             // If the token is an array, treat each element as a FeatureCollection.
    //             if (parsedToken is JArray jArray)
    //             {
    //                 featureCollections = jArray.Children().ToList();
    //             }
    //             // If it’s a single FeatureCollection object, wrap it in a list.
    //             else if (parsedToken is JObject)
    //             {
    //                 featureCollections = new List<JToken> { parsedToken };
    //             }
    //             else
    //             {
    //                 throw ApiExceptionDictionary.BadRequest("Invalid GeoJSON format.");
    //             }

    //             // Process each FeatureCollection individually.
    //             foreach (var featureCollectionToken in featureCollections)
    //             {
    //                 // Get the features array from the current FeatureCollection.
    //                 var featuresArray = featureCollectionToken["features"] as JArray;
    //                 if (featuresArray == null)
    //                 {
    //                     throw ApiExceptionDictionary.BadRequest(
    //                         "GeoJSON feature collection does not contain any features.");
    //                 }

    //                 var collectedGeometries = new List<Geometry>();

    //                 // Process each feature individually.
    //                 foreach (var featureToken in featuresArray)
    //                 {
    //                     try
    //                     {
    //                         // Get the geometry token.
    //                         var geometryToken = featureToken["geometry"];
    //                         if (geometryToken != null)
    //                         {
    //                             // Force the rings closed for Polygon or MultiPolygon.
    //                             featureToken["geometry"] = ForceCloseRings(geometryToken);
    //                         }

    //                         // Convert the feature token to JSON and parse it.
    //                         var featureJson = featureToken.ToString();
    //                         var feature = reader.Read<Feature>(featureJson);
    //                         if (feature != null && feature.Geometry != null)
    //                         {
    //                             collectedGeometries.Add(feature.Geometry);
    //                         }
    //                     }
    //                     catch (Exception ex)
    //                     {
    //                         // Log the error and skip any invalid feature.
    //                         Console.WriteLine($"Skipping invalid feature: {ex.Message}");
    //                     }
    //                 }

    //                 // If we collected any valid geometries, combine them in one GeometryCollection.
    //                 if (collectedGeometries.Any())
    //                 {
    //                     Geometry combinedGeometry = new GeometryCollection(collectedGeometries.ToArray());
    //                     // Create one database record for this feature collection.
    //                     var geoJsonEntity = new CaveGeoJson
    //                     {
    //                         CaveId = caveId,
    //                         Geometry = combinedGeometry,
    //                         // Optionally, store the entire feature collection JSON. Remove if not needed.
    //                         OriginalGeoJson = featureCollectionToken.ToString(),
    //                         Attributes = "{}"
    //                     };

    //                     Repository.AddCaveGeoJson(geoJsonEntity);
    //                     cave.GeoJsons.Add(geoJsonEntity);
    //                 }
    //             }
    //         }

    //         await Repository.SaveChangesAsync(cancellationToken);
    //         await transaction.CommitAsync(cancellationToken);
    //     }
    //     catch (Exception)
    //     {
    //         await transaction.RollbackAsync(cancellationToken);
    //         throw;
    //     }
    // }

    // /// <summary>
    // /// Ensures that for all Polygon or MultiPolygon geometries, each coordinate ring is explicitly closed
    // /// and has at least four points. Rings that do not meet the criteria are removed.
    // /// </summary>
    // /// <param name="geometryToken">The JSON token representing the geometry.</param>
    // /// <returns>The modified JSON token with only valid rings.</returns>
    // private JToken? ForceCloseRings(JToken? geometryToken)
    // {
    //     if (geometryToken == null) return null;

    //     var type = geometryToken["type"]?.Value<string>();
    //     if (string.IsNullOrWhiteSpace(type))
    //         return geometryToken;

    //     // Process Polygon geometry
    //     if (type == "Polygon")
    //     {
    //         var rings = geometryToken["coordinates"] as JArray;
    //         if (rings != null)
    //         {
    //             var validRings = new JArray();
    //             foreach (var ring in rings)
    //             {
    //                 var ringArray = ring as JArray;
    //                 if (ringArray == null || ringArray.Count == 0)
    //                     continue;

    //                 // If the first coordinate is not the same as the last, append a copy of the first.
    //                 if (!JToken.DeepEquals(ringArray.First, ringArray.Last))
    //                 {
    //                     ringArray.Add(ringArray.First.DeepClone());
    //                 }

    //                 // Only include this ring if it has at least 4 coordinates.
    //                 if (ringArray.Count >= 4)
    //                 {
    //                     validRings.Add(ringArray);
    //                 }
    //             }

    //             geometryToken["coordinates"] = validRings;
    //         }
    //     }
    //     // Process MultiPolygon geometry
    //     else if (type == "MultiPolygon")
    //     {
    //         var polygons = geometryToken["coordinates"] as JArray;
    //         if (polygons != null)
    //         {
    //             var validPolygons = new JArray();
    //             foreach (var polygon in polygons)
    //             {
    //                 var rings = polygon as JArray;
    //                 if (rings != null)
    //                 {
    //                     var validRings = new JArray();
    //                     foreach (var ring in rings)
    //                     {
    //                         var ringArray = ring as JArray;
    //                         if (ringArray == null || ringArray.Count == 0)
    //                             continue;

    //                         if (!JToken.DeepEquals(ringArray.First, ringArray.Last))
    //                         {
    //                             ringArray.Add(ringArray.First.DeepClone());
    //                         }

    //                         if (ringArray.Count >= 4)
    //                         {
    //                             validRings.Add(ringArray);
    //                         }
    //                     }

    //                     // Only add the polygon if at least one ring is valid.
    //                     if (validRings.Count > 0)
    //                     {
    //                         validPolygons.Add(validRings);
    //                     }
    //                 }
    //             }

    //             geometryToken["coordinates"] = validPolygons;
    //         }
    //     }

    //     return geometryToken;
    // }

    #endregion


    #endregion
}