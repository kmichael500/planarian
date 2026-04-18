using CsvHelper.Configuration;

namespace Tcs;

public sealed class CaveRecordMap : ClassMap<CaveRecord>
{
    public CaveRecordMap()
    {
        Map(m => m.TcsNumber).Name("tcsnumber");
        Map(m => m.CaveName).Name("name");
        Map(m => m.Latitude).Name("latitude");
        Map(m => m.Longitude).Name("longitude");
        Map(m => m.LengthFt).Name("length").TypeConverter<IntFromTextConverter>();
        Map(m => m.DepthFt).Name("depth", "vertical extent").TypeConverter<IntFromTextConverter>();
        Map(m => m.PitDepthFt).Name("pdep", "pit depth").TypeConverter<IntFromTextConverter>();
        Map(m => m.NumberOfPits).Name("ps", "pits").TypeConverter<IntFromTextConverter>();
        Map(m => m.CountyName).Name("co_name", "county");
        Map(m => m.TopographicName).Name("topo_name", "topo");
        Map(m => m.ElevationFt).Name("elev").TypeConverter<IntFromTextConverter>();
        Map(m => m.Ownership).Name("ownership");
        Map(m => m.RequiredGear).Name("gear", "equipment");
        Map(m => m.EntranceType).Name("ent_type", "entry");
        Map(m => m.FieldIndication).Name("field_indi");
        Map(m => m.MapStatus).Name("map_status", "map status");
        Map(m => m.CaveGeology).Name("geology");
        Map(m => m.GeologicalAge).Name("geo_age");
        Map(m => m.PhysiographicProvince).Name("phys_prov");
        Map(m => m.Narrative).Name("narrative", "desc");
    }
}
