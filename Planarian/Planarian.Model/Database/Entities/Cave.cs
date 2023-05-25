using Planarian.Model.Shared.Base;

namespace Planarian.Model.Database.Entities;

public class Cave : EntityBase
{
    public string CountyId { get; set; }
    public string ReportedByUserId { get; set; }
    public string PrimaryEntranceId { get; set; }

    public string Name { get; set; }
    public int CaveNumber { get; set; } // max of highest cave number in county + 1

    public double LengthFeet { get; set; }
    public double DepthFeet { get; set; }
    public double MaxPitDepthFeet { get; set; }
    public int NumberOfPits { get; set; }

    public string Narrative { get; set; }

    public DateTime? ReportedOn { get; set; }
    public string ReportedByName { get; set; }
    public bool IsArchived { get; set; }

    public virtual User ReportedByUser { get; set; } = null!;
    public virtual County County { get; set; } = null!;
    public virtual Entrance PrimaryEntrance { get; set; } = null!;

    public virtual ICollection<Map> Maps { get; set; } = new HashSet<Map>();
    public virtual ICollection<Entrance> Entrances { get; set; } = new HashSet<Entrance>();
    public virtual ICollection<CaveGeology> CaveGeology { get; set; } = new HashSet<CaveGeology>();
}

public class County : EntityBase
{
    public string Name { get; set; }
    public string DispayId { get; set; }
    
    public ICollection<Cave> Caves { get; set; } = null!;
}

public class CaveGeology : EntityBase
{
    public string CaveId { get; set; }
    public string GeologyId { get; set; }
    
    public Geology Geology { get; set; } = null!;
    public Cave Cave { get; set; } = null!;
}

public class Geology : EntityBaseNameId
{
}

public class Map : EntityBase
{
    public string CaveId { get; set; }
    
    public virtual Cave Cave { get; set; } = null!;
}

public class Entrance : EntityBase
{
    public string CaveId { get; set; } = null!;
    public string LocationQualityTagId { get; set; } = null!;
    public string ReportedByUserId { get; set; } = null!;
    
    public string Name { get; set; }
    public string Description { get; set; }

    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double ElevationFeet { get; set; }
    
    public DateTime? ReportedOn { get; set; }
    public User? ReportedByUser { get; set; }
    public string ReportedByName { get; set; }

    public double? PitFeet { get; set; }

    public virtual Cave Cave { get; set; } = null!;
    public virtual TagType LocationQualityTag { get; set; } = null!;
    public ICollection<EntranceStatusTag> EntranceStatusTags { get; set; } = new HashSet<EntranceStatusTag>();
    public ICollection<EntranceHydrologyFrequencyTag> EntranceHydrologyFrequencyTags { get; set; } = new HashSet<EntranceHydrologyFrequencyTag>();
    public ICollection<FieldIndicationTag> FieldIndicationTags { get; set; } = new HashSet<FieldIndicationTag>();
}

public class EntranceStatusTag : EntityBase
{
    public string TagTypeId { get; set; }
    public string EntranceId { get; set; }

    public TagType TagType { get; set; }
    public Entrance Entrance { get; set; }
}

public class EntranceHydrologyTag : EntityBase
{
    public string TagTypeId { get; set; }
    public string EntranceId { get; set; }

    public TagType TagType { get; set; }
    public Entrance Entrance { get; set; }
}

public class EntranceHydrologyFrequencyTag : EntityBase
{
    public string TagTypeId { get; set; }
    public string EntranceId { get; set; }

    public TagType TagType { get; set; }
    public Entrance Entrance { get; set; }
}

public class FieldIndicationTag : EntityBase
{
    public string TagTypeId { get; set; }
    public string EntranceId { get; set; }

    public TagType TagType { get; set; }
    public Entrance Entrance { get; set; }
}