using CsvHelper.Configuration;

namespace Planarian.Modules.Import.Models;

public sealed class EntranceCsvModelMap : ClassMap<EntranceCsvModel>
{
    public EntranceCsvModelMap()
    {
        Map(m => m.CountyCaveNumber);
        Map(m => m.EntranceName);
        Map(m => m.EntranceDescription);
        Map(m => m.IsPrimaryEntrance).Default(false);
        Map(m => m.EntrancePitDepth).Default(0.0);
        Map(m => m.EntranceStatus);
        Map(m => m.EntranceHydrology);
        Map(m => m.EntranceHydrologyFrequency);
        Map(m => m.FieldIndication);
        Map(m => m.CountyCode);
        Map(m => m.DecimalLatitude);
        Map(m => m.DecimalLongitude);
        Map(m => m.EntranceElevationFt).Default(0.0);
        Map(m => m.GeologyFormation);
        Map(m => m.ReportedOnDate);
        Map(m => m.ReportedByName);
        Map(m => m.LocationQuality);
    }
}