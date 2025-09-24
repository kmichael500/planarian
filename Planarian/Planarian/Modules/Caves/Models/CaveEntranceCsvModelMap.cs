using System.Collections.Generic;
using CsvHelper.Configuration;
using Planarian.Model.Database.Entities.RidgeWalker;

namespace Planarian.Modules.Caves.Models;

public class CaveEntranceCsvModelMap : ClassMap<CaveEntranceCsvModel>
{
    public CaveEntranceCsvModelMap(Dictionary<FeatureKey, bool> featureDict, HashSet<string>? exportFields)
    {

        bool Include(FeatureKey key) =>
            featureDict.TryGetValue(key, out var enabled) && enabled &&
            (exportFields == null || exportFields.Contains(key.ToString()));


        if (Include(FeatureKey.EnabledFieldEntranceName))
            Map(m => m.EntranceName).Name("Entrance Name");

        if (Include(FeatureKey.EnabledFieldEntranceDescription))
            Map(m => m.EntranceDescription).Name("Entrance Description");

        if (Include(FeatureKey.EnabledFieldEntranceCoordinates))
            Map(m => m.EntranceIsPrimary).Name("Entrance Is Primary");

        if (Include(FeatureKey.EnabledFieldEntranceReportedOn))
            Map(m => m.EntranceReportedOn).Name("Entrance Reported On");

        if (Include(FeatureKey.EnabledFieldEntrancePitDepth))
            Map(m => m.EntrancePitDepthFeet).Name("Entrance Pit Depth (ft)");

        if (Include(FeatureKey.EnabledFieldEntranceCoordinates))
        {
            Map(m => m.EntranceLatitude).Name("Entrance Latitude");
            Map(m => m.EntranceLongitude).Name("Entrance Longitude");
            Map(m => m.EntranceElevation).Name("Entrance Elevation");
        }

        if (Include(FeatureKey.EnabledFieldEntranceLocationQuality))
            Map(m => m.EntranceLocationQuality).Name("Entrance Location Quality");

        if (Include(FeatureKey.EnabledFieldEntranceStatusTags))
            Map(m => m.EntranceStatusTags).Name("Entrance Status");

        if (Include(FeatureKey.EnabledFieldEntranceFieldIndicationTags))
            Map(m => m.EntranceFieldIndicationTags).Name("Entrance Field Indication");

        if (Include(FeatureKey.EnabledFieldEntranceHydrologyTags))
            Map(m => m.EntranceHydrologyTags).Name("Entrance Hydrology");

        if (Include(FeatureKey.EnabledFieldEntranceReportedByNameTags))
            Map(m => m.EntranceReportedByTags).Name("Entrance Reported By");

        if (Include(FeatureKey.EnabledFieldEntranceOtherTags))
            Map(m => m.EntranceOtherTags).Name("Entrance Other");


        if (Include(FeatureKey.EnabledFieldCaveName))
            Map(m => m.CaveName).Name("Cave Name");

        if (Include(FeatureKey.EnabledFieldCaveAlternateNames))
            Map(m => m.CaveAlternateNames).Name("Cave Alternate Names");

        if (Include(FeatureKey.EnabledFieldCaveCounty))
            Map(m => m.CaveCounty).Name("Cave County");

        if (Include(FeatureKey.EnabledFieldCaveId))
        {
            Map(m => m.CaveCountyDisplayId).Name("Cave County Display ID");
            Map(m => m.CaveCountyNumber).Name("Cave County Number");
        }

        if (Include(FeatureKey.EnabledFieldCaveState))
            Map(m => m.CaveState).Name("Cave State");

        if (Include(FeatureKey.EnabledFieldCaveLengthFeet))
            Map(m => m.CaveLengthFeet).Name("Cave Length (ft)");

        if (Include(FeatureKey.EnabledFieldCaveDepthFeet))
            Map(m => m.CaveDepthFeet).Name("Cave Depth (ft)");

        if (Include(FeatureKey.EnabledFieldCaveMaxPitDepthFeet))
            Map(m => m.CaveMaxPitDepthFeet).Name("Cave Max Pit Depth (ft)");

        if (Include(FeatureKey.EnabledFieldCaveNumberOfPits))
            Map(m => m.CaveNumberOfPits).Name("Cave Number of Pits");

        if (Include(FeatureKey.EnabledFieldCaveNarrative))
            Map(m => m.CaveNarrative).Name("Cave Narrative");

        if (Include(FeatureKey.EnabledFieldCaveReportedOn))
            Map(m => m.CaveReportedOn).Name("Cave Reported On");

        if (Include(FeatureKey.EnabledFieldCaveId))
            Map(m => m.CaveIsArchived).Name("Cave Is Archived");

        if (Include(FeatureKey.EnabledFieldCaveGeologyTags))
            Map(m => m.CaveGeologyTags).Name("Cave Geology");

        if (Include(FeatureKey.EnabledFieldCaveMapStatusTags))
            Map(m => m.CaveMapStatusTags).Name("Cave Map Status");

        if (Include(FeatureKey.EnabledFieldCaveGeologicAgeTags))
            Map(m => m.CaveGeologicAgeTags).Name("Cave Geologic Age");

        if (Include(FeatureKey.EnabledFieldCavePhysiographicProvinceTags))
            Map(m => m.CavePhysiographicProvinceTags).Name("Cave Physiographic Province");

        if (Include(FeatureKey.EnabledFieldCaveBiologyTags))
            Map(m => m.CaveBiologyTags).Name("Cave Biology");

        if (Include(FeatureKey.EnabledFieldCaveArcheologyTags))
            Map(m => m.CaveArcheologyTags).Name("Cave Archeology");

        if (Include(FeatureKey.EnabledFieldCaveCartographerNameTags))
            Map(m => m.CaveCartographerNameTags).Name("Cave Cartographer Name");

        if (Include(FeatureKey.EnabledFieldCaveReportedByNameTags))
            Map(m => m.CaveReportedByTags).Name("Cave Reported By");

        if (Include(FeatureKey.EnabledFieldCaveOtherTags))
            Map(m => m.CaveOtherTags).Name("Cave Other");
    }
}