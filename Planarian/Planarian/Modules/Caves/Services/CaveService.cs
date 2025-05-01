using System.Data;
using System.Text;
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
using Planarian.Model.Database.Entities.RidgeWalker.ViewModels;
using Planarian.Model.Shared;
using Planarian.Modules.Account.Repositories;
using Planarian.Modules.Caves.Models;
using Planarian.Modules.Caves.Repositories;
using Planarian.Modules.FeatureSettings.Repositories;
using Planarian.Modules.Files.Repositories;
using Planarian.Modules.Files.Services;
using Planarian.Modules.Query.Extensions;
using Planarian.Modules.Query.Models;
using Planarian.Modules.Settings.Repositories;
using Planarian.Modules.Tags.Repositories;
using Planarian.Shared.Base;
using File = Planarian.Model.Database.Entities.RidgeWalker.File;

namespace Planarian.Modules.Caves.Services;

public partial class CaveService : ServiceBase<CaveRepository>
{
    private readonly FileService _fileService;
    private readonly FileRepository _fileRepository;
    private readonly TagRepository _tagRepository;
    private readonly SettingsRepository _settingsRepository;
    private readonly FeatureSettingRepository _featureSettingRepository;
    private readonly CaveChangeRequestRepository _caveChangeRequestRepository;
    private readonly CaveChangeLogRepository _caveChangeLogRepository;
    private readonly ServerOptions _serverOptions;


    public CaveService(CaveRepository repository, RequestUser requestUser, FileService fileService,
        FileRepository fileRepository, TagRepository tagRepository,
        FeatureSettingRepository featureSettingRepository, ServerOptions serverOptions,
        CaveChangeRequestRepository caveChangeRequestRepository, SettingsRepository settingsRepository, CaveChangeLogRepository caveChangeLogRepository) : base(
        repository, requestUser)
    {
        _fileService = fileService;
        _fileRepository = fileRepository;
        _tagRepository = tagRepository;
        _featureSettingRepository = featureSettingRepository;
        _serverOptions = serverOptions;
        _caveChangeRequestRepository = caveChangeRequestRepository;
        _settingsRepository = settingsRepository;
        _caveChangeLogRepository = caveChangeLogRepository;
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

        var exportData = await Repository.GetCavesForExport(filterQuery, permissionKey);

        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine("<gpx version=\"1.1\" creator=\"Planarian\" xmlns=\"http://www.topografix.com/GPX/1/1\">");

        foreach (var cave in exportData)
        {
            foreach (var entrance in cave.Entrances)
            {
                // Build the waypoint name.
                var caveName = featureDict.TryGetValue(FeatureKey.EnabledFieldCaveName, out var caveNameEnabled) &&
                               caveNameEnabled
                    ? cave.Name
                    : string.Empty;
                var entranceName =
                    featureDict.TryGetValue(FeatureKey.EnabledFieldEntranceName, out var entranceNameEnabled) &&
                    entranceNameEnabled
                        ? entrance.Name
                        : string.Empty;
                entranceName = System.Security.SecurityElement.Escape(entranceName);

                var caveCountyId = featureDict.TryGetValue(FeatureKey.EnabledFieldCaveId, out var showCaveId) &&
                                   showCaveId
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

                if (featureDict.TryGetValue(FeatureKey.EnabledFieldCaveName, out var showCaveName) && showCaveName)
                {
                    var name = cave.Name;
                    if (!string.IsNullOrWhiteSpace(name))
                        caveInfoLines.Add($"Name: {name}");
                }

                if (featureDict.TryGetValue(FeatureKey.EnabledFieldCaveAlternateNames, out var showCaveAltNames) &&
                    showCaveAltNames)
                {
                    var altNames = cave.AlternateNames.ToCommaSeparatedString();
                    if (!string.IsNullOrWhiteSpace(altNames))
                        caveInfoLines.Add($"Alternate Names: {altNames}");
                }

                if (featureDict.TryGetValue(FeatureKey.EnabledFieldCaveCounty, out var showCaveCounty) &&
                    showCaveCounty)
                {
                    var county = cave.CountyName;
                    if (!string.IsNullOrWhiteSpace(county))
                        caveInfoLines.Add($"County: {county}");
                }

                if (featureDict.TryGetValue(FeatureKey.EnabledFieldCaveState, out var showCaveState) && showCaveState)
                {
                    var state = cave.StateName;
                    if (!string.IsNullOrWhiteSpace(state))
                        caveInfoLines.Add($"State: {state}");
                }

                if (featureDict.TryGetValue(FeatureKey.EnabledFieldCaveLengthFeet, out var showCaveLength) &&
                    showCaveLength)
                {
                    if (cave.LengthFeet.HasValue && cave.LengthFeet != 0)
                        caveInfoLines.Add($"Length (ft): {cave.LengthFeet}");
                }

                if (featureDict.TryGetValue(FeatureKey.EnabledFieldCaveDepthFeet, out var showCaveDepth) &&
                    showCaveDepth)
                {
                    if (cave.DepthFeet.HasValue && cave.DepthFeet != 0)
                        caveInfoLines.Add($"Depth (ft): {cave.DepthFeet}");
                }

                if (featureDict.TryGetValue(FeatureKey.EnabledFieldCaveMaxPitDepthFeet, out var showCaveMaxPit) &&
                    showCaveMaxPit)
                {
                    if (cave.MaxPitDepthFeet.HasValue && cave.MaxPitDepthFeet != 0)
                        caveInfoLines.Add($"Max Pit Depth (ft): {cave.MaxPitDepthFeet}");
                }

                if (featureDict.TryGetValue(FeatureKey.EnabledFieldCaveNumberOfPits, out var showCaveNumPits) &&
                    showCaveNumPits)
                {
                    if (cave.NumberOfPits.HasValue && cave.NumberOfPits != 0)
                        caveInfoLines.Add($"Number Of Pits: {cave.NumberOfPits}");
                }

                if (featureDict.TryGetValue(FeatureKey.EnabledFieldCaveReportedOn, out var showCaveReportedOn) &&
                    showCaveReportedOn)
                {
                    var reportedOn = cave.ReportedOn.HasValue
                        ? cave.ReportedOn.Value.ToShortDateString()
                        : string.Empty;
                    if (!string.IsNullOrWhiteSpace(reportedOn))
                        caveInfoLines.Add($"Reported On: {reportedOn}");
                }

                if (featureDict.TryGetValue(FeatureKey.EnabledFieldCaveGeologyTags, out var showCaveGeology) &&
                    showCaveGeology)
                {
                    var geology = cave.GeologyTags.ToCommaSeparatedString();
                    if (!string.IsNullOrWhiteSpace(geology))
                        caveInfoLines.Add($"Geology: {geology}");
                }

                if (featureDict.TryGetValue(FeatureKey.EnabledFieldCaveGeologicAgeTags, out var showCaveGeoAge) &&
                    showCaveGeoAge)
                {
                    var geoAge = cave.GeologicAgeTags.ToCommaSeparatedString();
                    if (!string.IsNullOrWhiteSpace(geoAge))
                        caveInfoLines.Add($"Geologic Age: {geoAge}");
                }

                if (featureDict.TryGetValue(FeatureKey.EnabledFieldCavePhysiographicProvinceTags,
                        out var showCavePhysio) && showCavePhysio)
                {
                    var physio = cave.PhysiographicProvinceTags.ToCommaSeparatedString();
                    if (!string.IsNullOrWhiteSpace(physio))
                        caveInfoLines.Add($"Physiographic Province: {physio}");
                }

                if (featureDict.TryGetValue(FeatureKey.EnabledFieldCaveBiologyTags, out var showCaveBiology) &&
                    showCaveBiology)
                {
                    var biology = cave.BiologyTags.ToCommaSeparatedString();
                    if (!string.IsNullOrWhiteSpace(biology))
                        caveInfoLines.Add($"Biology: {biology}");
                }

                if (featureDict.TryGetValue(FeatureKey.EnabledFieldCaveArcheologyTags, out var showCaveArcheology) &&
                    showCaveArcheology)
                {
                    var archeology = cave.ArcheologyTags.ToCommaSeparatedString();
                    if (!string.IsNullOrWhiteSpace(archeology))
                        caveInfoLines.Add($"Archeology: {archeology}");
                }

                if (featureDict.TryGetValue(FeatureKey.EnabledFieldCaveMapStatusTags, out var showCaveMapStatus) &&
                    showCaveMapStatus)
                {
                    var mapStatus = cave.MapStatusTags.ToCommaSeparatedString();
                    if (!string.IsNullOrWhiteSpace(mapStatus))
                        caveInfoLines.Add($"Map Status: {mapStatus}");
                }

                if (featureDict.TryGetValue(FeatureKey.EnabledFieldCaveCartographerNameTags,
                        out var showCaveCartographer) && showCaveCartographer)
                {
                    var cartographer = cave.CartographerNameTags.ToCommaSeparatedString();
                    if (!string.IsNullOrWhiteSpace(cartographer))
                        caveInfoLines.Add($"Cartographer Name: {cartographer}");
                }

                if (featureDict.TryGetValue(FeatureKey.EnabledFieldCaveReportedByNameTags,
                        out var showCaveReportedBy) && showCaveReportedBy)
                {
                    var reportedBy = cave.CaveReportedByTags.ToCommaSeparatedString();
                    if (!string.IsNullOrWhiteSpace(reportedBy))
                        caveInfoLines.Add($"Cave Reported By: {reportedBy}");
                }

                if (featureDict.TryGetValue(FeatureKey.EnabledFieldCaveOtherTags, out var showCaveOther) &&
                    showCaveOther)
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
                if (featureDict.TryGetValue(FeatureKey.EnabledFieldEntranceName, out var showEntranceName) &&
                    showEntranceName)
                {
                    var name = entrance.Name;
                    if (!string.IsNullOrWhiteSpace(name) &&
                        !string.Equals(name, cave.Name, StringComparison.CurrentCultureIgnoreCase))
                        entranceInfoSb.AppendLine($"Name: {name}");
                }

                if (featureDict.TryGetValue(FeatureKey.EnabledFieldEntranceReportedOn,
                        out var showEntranceReportedOn) && showEntranceReportedOn)
                {
                    var reportedOn = entrance.ReportedOn.HasValue
                        ? entrance.ReportedOn.Value.ToShortDateString()
                        : string.Empty;
                    if (!string.IsNullOrWhiteSpace(reportedOn))
                        entranceInfoSb.AppendLine($"Reported On: {reportedOn}");
                }

                if (featureDict.TryGetValue(FeatureKey.EnabledFieldEntrancePitDepth, out var showEntrancePitDepth) &&
                    showEntrancePitDepth)
                {
                    // Assuming a numeric value.
                    if (entrance.PitDepthFeet.HasValue && entrance.PitDepthFeet != 0)
                        entranceInfoSb.AppendLine($"Pit Depth (ft): {entrance.PitDepthFeet}");
                }

                entranceInfoSb.AppendLine("Primary Entrance: " + (entrance.IsPrimary ? "Yes" : "No"));

                if (featureDict.TryGetValue(FeatureKey.EnabledFieldEntranceLocationQuality,
                        out var showEntranceLocQual) && showEntranceLocQual)
                {
                    var locQual = entrance.LocationQuality;
                    if (!string.IsNullOrWhiteSpace(locQual))
                        entranceInfoSb.AppendLine($"Location Quality: {locQual}");
                }

                if (featureDict.TryGetValue(FeatureKey.EnabledFieldEntranceStatusTags, out var showEntranceStatus) &&
                    showEntranceStatus)
                {
                    var statusTags = entrance.EntranceStatusTags.ToCommaSeparatedString();
                    if (!string.IsNullOrWhiteSpace(statusTags))
                        entranceInfoSb.AppendLine($"Entrance Status: {statusTags}");
                }

                if (featureDict.TryGetValue(FeatureKey.EnabledFieldEntranceFieldIndicationTags,
                        out var showEntranceFieldInd) && showEntranceFieldInd)
                {
                    var fieldIndTags = entrance.FieldIndicationTags.ToCommaSeparatedString();
                    if (!string.IsNullOrWhiteSpace(fieldIndTags))
                        entranceInfoSb.AppendLine($"Field Indication: {fieldIndTags}");
                }

                if (featureDict.TryGetValue(FeatureKey.EnabledFieldEntranceHydrologyTags, out var showEntranceHydro) &&
                    showEntranceHydro)
                {
                    var hydroTags = entrance.EntranceHydrologyTags.ToCommaSeparatedString();
                    if (!string.IsNullOrWhiteSpace(hydroTags))
                        entranceInfoSb.AppendLine($"Entrance Hydrology: {hydroTags}");
                }

                if (featureDict.TryGetValue(FeatureKey.EnabledFieldEntranceReportedByNameTags,
                        out var showEntranceReportedBy) && showEntranceReportedBy)
                {
                    var reportedByTags = entrance.EntranceReportedByTags.ToCommaSeparatedString();
                    if (!string.IsNullOrWhiteSpace(reportedByTags))
                        entranceInfoSb.AppendLine($"Entrance Reported By: {reportedByTags}");
                }

                if (featureDict.TryGetValue(FeatureKey.EnabledFieldEntranceDescription, out var showEntranceDesc) &&
                    showEntranceDesc)
                {
                    var description = entrance.Description;
                    if (!string.IsNullOrWhiteSpace(description))
                    {
                        var safeDescription = System.Security.SecurityElement.Escape(description);
                        entranceInfoSb.AppendLine($"Description: {safeDescription}");
                    }
                }

                if (featureDict.TryGetValue(FeatureKey.EnabledFieldCaveNarrative, out var showCaveNarrative) &&
                    showCaveNarrative)
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

    #region Create / Update Cave

    public async Task<string> AddCave(AddCave values, CancellationToken cancellationToken,
        IDbContextTransaction? transaction = null)
    {
        if (string.IsNullOrWhiteSpace(RequestUser.AccountId)) throw ApiExceptionDictionary.NoAccount;
        await ValidateCave(values);

        var entity = await Repository.GetAsync(values.Id);
        var isNew = entity == null;

        if (values.Id.IsNullOrWhiteSpace() || !values.Id.IsValidId())
        {
            throw ApiExceptionDictionary.BadRequest("Cave Id must be defined ahead of time.");
        }

        entity ??= new Cave { Id = values.Id };


        var existingTransaction = transaction != null;
        transaction ??= await Repository.BeginTransactionAsync(cancellationToken);
        try
        {
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
                    
                    entrance.EntranceStatusTags.Clear();
                    entrance.FieldIndicationTags.Clear();
                    entrance.EntranceOtherTags.Clear();
                    entrance.EntranceHydrologyTags.Clear();
                    entrance.EntranceReportedByNameTags.Clear();
                    
                    entity.Entrances.Remove(entrance);
                    Repository.Delete(entrance);
                }

            foreach (var entranceValue in values.Entrances)
            {
                var entrance = entity.Entrances.FirstOrDefault(e => e.Id == entranceValue.Id);

                var isNewEntrance = entrance == null;
                if (isNewEntrance)
                {
                    if (entranceValue.Id.IsNullOrWhiteSpace() || !entranceValue.Id.IsValidId())
                    {
                        throw ApiExceptionDictionary.BadRequest("Entrance Id must be defined ahead of time.");
                    }

                    entrance = new Entrance
                    {
                        Id = entranceValue.Id,
                        CaveId = entity.Id
                    };
                }

                entrance!.Name = entranceValue.Name;
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

            if (!existingTransaction)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            foreach (var blobProperties in blobsToDelete)
                await _fileService.DeleteFile(blobProperties.BlobKey, blobProperties.BlobContainer);

            return entity.Id;
        }
        catch (Exception e)
        {
            if (!existingTransaction)
            {
                await transaction.RollbackAsync(cancellationToken);
            }

            throw;
        }

    }


    private async Task ValidateCave(AddCave values)
    {
        await RequestUser.HasCavePermission(PermissionPolicyKey.Manager, values.Id, values.CountyId);

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
    }
    
    #endregion

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

    public async Task<IEnumerable<CaveHistory>> GetCaveHistory(string caveId,
        CancellationToken cancellationToken)
    {
        var caveHistorySummaries = (await _caveChangeLogRepository.GetCaveHistory(caveId, cancellationToken)).ToList();

        var entireHistory =
            caveHistorySummaries
                .SelectMany(e => e.Records)
                .OrderByDescending(e => e.CreatedOn)
                .ToList();

        foreach (var historySummary in caveHistorySummaries)
        {
            var entranceHistorySummary = new List<EntranceHistorySummary>();
            var caveHistoryDetails = new List<HistoryDetail>();

            // there can be multiple add, delete, etc. For the same change history, we only want to process once
            var listsAlreadyProcessed = new List<string>();

            foreach (var record in historySummary.Records)
            {
                var previousRecord = GetPreviousRecord(entireHistory, record.PropertyName, record.EntranceId,
                    record.CreatedOn);
                var historyDetail = new HistoryDetail
                {
                    PropertyName = record.PropertyName,
                    ValueString = record.ValueString,
                    ValueInt = record.ValueInt,
                    ValueDouble = record.ValueDouble,
                    ValueBool = record.ValueBool,
                    ValueDateTime = record.ValueDateTime,
                    PreviousValueString = previousRecord?.ValueString,
                    PreviousValueInt = previousRecord?.ValueInt,
                    PreviousValueDouble = previousRecord?.ValueDouble,
                    PreviousValueBool = previousRecord?.ValueBool,
                    PreviousValueDateTime = previousRecord?.ValueDateTime
                };
                switch (record.PropertyName)
                {
                    case CaveLogPropertyNames.AlternateNames:
                    case CaveLogPropertyNames.GeologyTagName:
                    case CaveLogPropertyNames.MapStatusTagName:
                    case CaveLogPropertyNames.GeologicAgeTagName:
                    case CaveLogPropertyNames.PhysiographicProvinceTagName:
                    case CaveLogPropertyNames.BiologyTagName:
                    case CaveLogPropertyNames.ArcheologyTagName:
                    case CaveLogPropertyNames.CartographerNameTagName:
                    case CaveLogPropertyNames.ReportedByNameTagName:
                    case CaveLogPropertyNames.OtherTagName:
                    case CaveLogPropertyNames.EntranceHydrologyTagName:
                    case CaveLogPropertyNames.EntranceFieldIndicationTagName:
                    case CaveLogPropertyNames.EntranceReportedByNameTagName:
                        if (listsAlreadyProcessed.Contains(record.PropertyName)) continue;

                        var current = GetListAtDateTime(entireHistory, record.PropertyName, record.CreatedOn);
                        var previousAlternateNames = previousRecord != null
                            ? GetListAtDateTime(entireHistory, record.PropertyName, previousRecord.CreatedOn)
                            : [];
                        historyDetail.ValueStrings = current.ToList();
                        historyDetail.PreviousValueStrings = previousAlternateNames.ToList();

                        historyDetail.ValueString = null;
                        historyDetail.ValueInt = null;
                        historyDetail.ValueDouble = null;
                        historyDetail.ValueBool = null;
                        historyDetail.ValueDateTime = null;
                        historyDetail.PreviousValueString = null;
                        historyDetail.PreviousValueInt = null;
                        historyDetail.PreviousValueDouble = null;
                        historyDetail.PreviousValueBool = null;
                        historyDetail.PreviousValueDateTime = null;
                        listsAlreadyProcessed.Add(record.PropertyName);
                        break;
                }

                if (!string.IsNullOrWhiteSpace(record.EntranceId))
                {
                    var entrance =
                        entranceHistorySummary.FirstOrDefault(e => e.EntranceId == record.EntranceId);
                    var entranceInList = entrance != null;
                    entrance ??= new EntranceHistorySummary
                    {
                        EntranceName = GetEntranceName(record.EntranceId, entireHistory,
                            record.CreatedOn),
                        EntranceId = record.EntranceId,
                        ChangeType = ChangeType.Update,
                    };

                    if (record.PropertyName == CaveLogPropertyNames.Entrance)
                    {
                        entrance.ChangeType = record.ChangeType;
                    }

                    entrance.Details = entrance.Details.Append(historyDetail);

                    if (!entranceInList)
                    {
                        entranceHistorySummary.Add(entrance);
                    }

                    continue;
                }

                caveHistoryDetails.Add(historyDetail);
            }

            historySummary.CaveHistoryDetails = caveHistoryDetails;
            historySummary.EntranceHistorySummary = entranceHistorySummary;
        }

        return caveHistorySummaries;
    }


    private string GetEntranceName(string entranceId, IEnumerable<CaveHistoryRecord> changeLogs, DateTime dateTime)
    {
        // materialize once so we don't re‐enumerate
        var logs = changeLogs.ToList();

        // 1) Try to get an explicit name as of dateTime
        var entranceName = logs
            .Where(e =>
                e.EntranceId == entranceId
                && e.PropertyName == CaveLogPropertyNames.EntranceName
                && e.CreatedOn <= dateTime)
            .MaxBy(e => e.CreatedOn)
            ?.ValueString;

        if (!entranceName.IsNullOrWhiteSpace())
            return entranceName!;

        // 2) Build the set of all EntranceIds seen up to dateTime
        var allIds = logs
            .Where(e => e.EntranceId != null && e.CreatedOn <= dateTime)
            .Select(e => e.EntranceId!)
            .Distinct();

        // 3) Remove any that were deleted on or before dateTime
        var deletedIds = logs
            .Where(e =>
                e is
                {
                    PropertyName: CaveLogPropertyNames.Entrance, ChangeValueType: ChangeValueType.Entrance,
                    ChangeType: ChangeType.Delete, EntranceId: not null
                }
                && e.CreatedOn <= dateTime)
            .Select(e => e.EntranceId!)
            .Distinct();

        var presentIds = allIds.Except(deletedIds).ToList();

        // 4) For each remaining entrance, get its latest ReportedOn (may be null)
        var orderedIds = presentIds
            .Select(id =>
            {
                var lastReport = logs
                    .Where(e =>
                        e.EntranceId == id
                        && e.PropertyName == CaveLogPropertyNames.EntranceReportedOn
                        && e.CreatedOn <= dateTime)
                    .MaxBy(e => e.CreatedOn);

                return new { EntranceId = id, ReportedOn = lastReport?.ValueDateTime };
            })
            // Actual dates first; nulls sort to the end
            .OrderByDescending(x => x.ReportedOn)
            .Select(x => x.EntranceId)
            .ToList();

        // 5) Return the 1-based position (or empty if missing)
        var idx = orderedIds.IndexOf(entranceId);
        return idx >= 0
            ? $"Entrance {(idx + 1)}".ToString()
            : string.Empty;
    }

    private IEnumerable<string?> GetListAtDateTime(
        IEnumerable<CaveHistoryRecord> changeLogs,
        string? propertyName,
        DateTime dateTime)
    {
        var changeLogsAtDateTime = changeLogs.Where(e => e.PropertyName == propertyName && e.CreatedOn <= dateTime)
            .OrderBy(e => e.CreatedOn)
            .ThenBy(e => e.ChangeType switch
            {
                ChangeType.Add => 1,
                ChangeType.Update => 2,
                ChangeType.Delete => 3,
                _ => 4
            }).ToList();

        var values = new List<(string? Id, string? Value)>();
        foreach (var log in changeLogsAtDateTime)
        {

            var value = values.FirstOrDefault(e => e.Id == log.PropertyId);

            switch (log.ChangeType)
            {
                case ChangeType.Add:
                    values.Add((log.PropertyId, log.ValueString));
                    break;
                case ChangeType.Update:
                {
                    values.Remove(value);
                    values.Add((log.PropertyId, log.ValueString));
                    break;
                }
                case ChangeType.Delete:
                    values.Remove(value);
                    break;
            }
        }

        return values.Select(e => e.Value);
    }

    private CaveHistoryRecord? GetPreviousRecord(IEnumerable<CaveHistoryRecord> changeLogs,
        string propertyName,
        string? entranceId,
        DateTime dateTime)
    {
        var changeLogsAtDateTime = changeLogs.Where(e => e.PropertyName == propertyName && e.EntranceId == entranceId && e.CreatedOn < dateTime)
            .OrderByDescending(e => e.CreatedOn)
            .ToList();

        return changeLogsAtDateTime.FirstOrDefault();
    }

    public async Task DeleteCave(string caveId, CancellationToken cancellationToken,
        IDbContextTransaction? transaction = null)
    {
        var outsideTransaction = transaction != null;
        transaction ??= await Repository.BeginTransactionAsync(cancellationToken);

        List<File> files;
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

public static class CaveLogPropertyNames
{
    public const string CountyName                       = "CountyName";
    public const string StateName                        = "StateName";
    public const string Name                             = "Name";
    public const string AlternateNames                   = "AlternateNames";
    public const string LengthFeet                       = "LengthFeet";
    public const string DepthFeet                        = "DepthFeet";
    public const string MaxPitDepthFeet                  = "MaxPitDepthFeet";
    public const string NumberOfPits                     = "NumberOfPits";
    public const string Narrative                        = "Narrative";
    public const string ReportedOn                       = "ReportedOn";
    public const string GeologyTagName                   = "GeologyTagName";
    public const string MapStatusTagName                 = "MapStatusTagName";
    public const string GeologicAgeTagName               = "GeologicAgeTagName";
    public const string PhysiographicProvinceTagName     = "PhysiographicProvinceTagName";
    public const string BiologyTagName                   = "BiologyTagName";
    public const string ArcheologyTagName                = "ArcheologyTagName";
    public const string CartographerNameTagName          = "CartographerNameTagName";
    public const string ReportedByNameTagName            = "ReportedByNameTagName";
    public const string OtherTagName                     = "OtherTagName";
    
    public const string EntranceName                     = "EntranceName";
    public const string EntranceLatitude                  = "EntranceLatitude";
    public const string EntranceLongitude                 = "EntranceLongitude";
    public const string EntranceElevationFeet             = "EntranceElevationFeet";
    public const string EntranceDescription              = "EntranceDescription";
    public const string EntranceIsPrimary                 = "EntranceIsPrimary";
    public const string EntranceLocationQualityTagName   = "EntranceLocationQualityTagName";
    public const string EntrancePitDepthFeet             = "EntrancePitDepthFeet";
    public const string EntranceReportedOn               = "EntranceReportedOn";
    public const string EntranceStatusTagName            = "EntranceStatusTagName";
    public const string EntranceHydrologyTagName         = "EntranceHydrologyTagName";
    public const string EntranceFieldIndicationTagName   = "EntranceFieldIndicationTagName";
    public const string EntranceReportedByNameTagName    = "EntranceReportedByNameTagName";

    public const string Entrance = "Entrance";
    public const string Cave = "Cave";
    public const string File = "File";
}
