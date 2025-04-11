namespace Planarian.Modules.Caves.Models;

using System;

public class CaveSearchParamsVm
{

    #region Cave
    
    public string Id { get; set; }
    
    public string IsFavorite { get; set; }
    public string Name { get; set; }
    public string Narrative { get; set; }
    public string StateId { get; set; }
    public string CountyId { get; set; }
    public int LengthFeet { get; set; }
    public int DepthFeet { get; set; }
    public int ElevationFeet { get; set; }
    public int NumberOfPits { get; set; }
    public int MaxPitDepthFeet { get; set; }
    public string MapStatusTagIds { get; set; }
    public string CartographerNamePeopleTagIds { get; set; }
    public string GeologyTagIds { get; set; }
    public string GeologicAgeTagIds { get; set; }
    public string PhysiographicProvinceTagIds { get; set; }
    public string BiologyTagIds { get; set; }
    public string ArchaeologyTagIds { get; set; }
    public string CaveReportedByNameTagIds { get; set; }
    public DateTime CaveReportedOnDate { get; set; }
    
    public string CaveOtherTagIds { get; set; }

    #endregion

    #region Entrance
    
    public string EntranceStatusTagIds { get; set; }
    public string EntranceDescription { get; set; }
    public string EntranceFieldIndicationTagIds { get; set; }
    public int EntrancePitDepthFeet { get; set; }
    public string LocationQualityTagIds { get; set; }
    public string EntranceHydrologyTagIds { get; set; }
    public string EntranceReportedByPeopleTagIds { get; set; }
    public DateTime EntranceReportedOnDate { get; set; }

    #endregion

    #region Files
    
    public string FileTypeTagIds { get; set; }
    public string FileDisplayName { get; set; }
    
    #endregion
}