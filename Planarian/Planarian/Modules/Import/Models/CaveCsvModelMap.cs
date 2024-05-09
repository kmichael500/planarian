using CsvHelper.Configuration;

namespace Planarian.Modules.Import.Models;

public sealed class CaveCsvModelMap : ClassMap<CaveCsvModel>
{
    public CaveCsvModelMap()
    {
        Map(m => m.CaveName);
        Map(m => m.AlternateNames);
        Map(m => m.State);
        Map(m => m.CountyCode);
        Map(m => m.CountyName);
        Map(m => m.CountyCaveNumber);
        Map(m => m.MapStatuses);
        Map(m => m.CartographerNames);
        Map(m => m.CaveLengthFt).Default(0.0);
        Map(m => m.CaveDepthFt).Default(0.0);
        Map(m => m.MaxPitDepthFt).Default(0.0);
        Map(m => m.NumberOfPits);
        Map(m => m.Narrative);
        Map(m => m.Geology);
        Map(m => m.GeologicAges);
        Map(m => m.PhysiographicProvinces);
        Map(m => m.Archeology);
        Map(m => m.Biology);
        Map(m => m.ReportedOnDate);
        Map(m => m.ReportedByNames);
        Map(m => m.IsArchived);
        Map(m => m.OtherTags);
    }
}