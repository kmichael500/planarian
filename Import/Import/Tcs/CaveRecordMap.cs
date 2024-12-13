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
        Map(m => m.LengthFt).Name("length");
        Map(m => m.DepthFt).Name("depth");
        Map(m => m.PitDepthFt).Name("pdep");
        Map(m => m.NumberOfPits).Name("ps");
        Map(m => m.CountyName).Name("co_name");
        Map(m => m.TopographicName).Name("topo_name");
        Map(m => m.ElevationFt).Name("elev");
        Map(m => m.Ownership).Name("ownership");
        Map(m => m.RequiredGear).Name("gear");
        Map(m => m.EntranceType).Name("ent_type");
        Map(m => m.FieldIndication).Name("field_indi");
        Map(m => m.MapStatus).Name("map_status");
        Map(m => m.CaveGeology).Name("geology");
        Map(m => m.GeologicalAge).Name("geo_age");
        Map(m => m.PhysiographicProvince).Name("phys_prov");
        Map(m => m.Narrative).Name("narrative");
    }
}