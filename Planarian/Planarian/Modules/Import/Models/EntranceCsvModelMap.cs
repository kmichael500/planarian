using CsvHelper.Configuration;

namespace Planarian.Modules.Import.Models;

public sealed class EntranceCsvModelMap : ClassMap<EntranceCsvModel>
{
    public EntranceCsvModelMap()
    {
        Map(m => m.CountyCode);
        Map(m => m.CountyCaveNumber);
        Map(m => m.EntranceName);
        Map(m => m.DecimalLatitude);
        Map(m => m.DecimalLongitude);
        Map(m => m.EntranceElevationFt);
        Map(m => m.LocationQuality);
        Map(m => m.EntranceDescription);
        Map(m => m.EntrancePitDepth);
        Map(m => m.EntranceStatuses);
        Map(m => m.EntranceHydrology);
        Map(m => m.FieldIndication);
        Map(m => m.ReportedOnDate);
        Map(m => m.ReportedByNames);
        Map(m => m.IsPrimaryEntrance).Default(false);
    }
}