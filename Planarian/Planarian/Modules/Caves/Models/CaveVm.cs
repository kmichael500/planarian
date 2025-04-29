using System.ComponentModel.DataAnnotations;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.Entities.RidgeWalker.ViewModels;
using Planarian.Model.Shared;
using Planarian.Modules.Files.Services;

namespace Planarian.Modules.Caves.Models;

public class CaveVm
{
    public CaveVm(string id, string stateId, string countyId, string displayId, string name,
        IEnumerable<string> alternateNames,
        double? lengthFeet,
        double? depthFeet, double? maxPitDepthFeet, int? numberOfPits, bool isArchived,
        EntranceVm primaryEntrance,
        IEnumerable<string> mapIds,
        IEnumerable<EntranceVm> entrances, IEnumerable<string> geologyTagIds, IEnumerable<FileVm> files)
    {
        Id = id;
        StateId = stateId;
        CountyId = countyId;
        DisplayId = displayId;
        Name = name;
        AlternateNames = alternateNames;
        LengthFeet = lengthFeet;
        DepthFeet = depthFeet;
        NumberOfPits = numberOfPits;
        IsArchived = isArchived;
        PrimaryEntrance = primaryEntrance;
        MapIds = mapIds;
        Entrances = entrances.ToList();
        GeologyTagIds = geologyTagIds;
        Files = files;
    }


    public CaveVm(string id, string reportedByUserId, string narrative, DateTime? reportedOn,
        IEnumerable<string> reportedByNameTagIds, string stateId, string countyId, string displayId, string name,
        IEnumerable<string> alternateNames,
        double? lengthFeet,
        double? depthFeet, double? maxPitDepthFeet, int? numberOfPits, bool isArchived,
        EntranceVm primaryEntrance,
        IEnumerable<string> mapIds,
        IEnumerable<EntranceVm> entrances, IEnumerable<string> geologyTagIds, IEnumerable<FileVm> files) : this(id,
        stateId,
        countyId, displayId, name, alternateNames, lengthFeet, depthFeet, maxPitDepthFeet, numberOfPits, isArchived,
        primaryEntrance, mapIds, entrances, geologyTagIds, files)
    {
        ReportedByUserId = reportedByUserId;
        MaxPitDepthFeet = maxPitDepthFeet;
        Narrative = narrative;
        ReportedOn = reportedOn;
        ReportedByNameTagIds = reportedByNameTagIds;
    }

    public CaveVm()
    {
    }

    [MaxLength(PropertyLength.Id)] public string Id { get; set; } = null!;
    [MaxLength(PropertyLength.Id)] public string? ReportedByUserId { get; set; }
    [MaxLength(PropertyLength.Id)] public string StateId { get; set; } = null!;
    [MaxLength(PropertyLength.Id)] public string CountyId { get; set; } = null!;

    public string DisplayId { get; set; } = null!;

    [MaxLength(PropertyLength.Name)] public string Name { get; set; } = null!;
    public IEnumerable<string> AlternateNames { get; set; } = new List<string>();

    public double? LengthFeet { get; set; }
    public double? DepthFeet { get; set; }
    public double? MaxPitDepthFeet { get; set; }
    public int? NumberOfPits { get; set; } = 0;

    public string? Narrative { get; set; }
    
    public bool IsFavorite { get; set; }

    public DateTime? ReportedOn { get; set; }
    public bool IsArchived { get; set; } = false;
    public IEnumerable<FileVm> Files { get; set; } = new HashSet<FileVm>();

    public EntranceVm PrimaryEntrance { get; set; } = null!;

    public IEnumerable<string> MapIds { get; set; } = new HashSet<string>();
    public List<EntranceVm> Entrances { get; set; } = new();
    public IEnumerable<string> GeologyTagIds { get; set; } = new HashSet<string>();
    public IEnumerable<string> ReportedByNameTagIds { get; set; } = new HashSet<string>();
    public IEnumerable<string> BiologyTagIds { get; set; } = new HashSet<string>();
    public IEnumerable<string> ArcheologyTagIds { get; set; } = new HashSet<string>();
    public IEnumerable<string> CartographerNameTagIds { get; set; } = new HashSet<string>();
    public IEnumerable<string> GeologicAgeTagIds { get; set; } = new HashSet<string>();
    public IEnumerable<string> PhysiographicProvinceTagIds { get; set; } = new HashSet<string>();
    public IEnumerable<string> OtherTagIds { get; set; } = new HashSet<string>();
    public IEnumerable<string> MapStatusTagIds { get; set; } = new HashSet<string>();
    
    public DateTime? UpdatedOn { get; set; }
    
    
}

public static class CaveVmExtensions
    {
        public static AddCave ToAddCave(this CaveVm vm)
        {
            ArgumentNullException.ThrowIfNull(vm);

            return new AddCave
            {
                Id = vm.Id,
                Name = vm.Name,
                StateId = vm.StateId,
                CountyId = vm.CountyId,
                LengthFeet = vm.LengthFeet ?? default,
                DepthFeet = vm.DepthFeet ?? default,
                MaxPitDepthFeet = vm.MaxPitDepthFeet ?? default,
                NumberOfPits = vm.NumberOfPits ?? default,

                Narrative = vm.Narrative,
                ReportedOn = vm.ReportedOn,

                AlternateNames = vm.AlternateNames?.ToList() ?? [],
                GeologyTagIds = vm.GeologyTagIds?.ToList() ?? [],
                ReportedByNameTagIds = vm.ReportedByNameTagIds?.ToList() ?? [],
                BiologyTagIds = vm.BiologyTagIds?.ToList() ?? [],
                ArcheologyTagIds = vm.ArcheologyTagIds?.ToList() ?? [],
                CartographerNameTagIds = vm.CartographerNameTagIds?.ToList() ?? [],
                MapStatusTagIds = vm.MapStatusTagIds?.ToList() ?? [],
                GeologicAgeTagIds = vm.GeologicAgeTagIds?.ToList() ?? [],
                PhysiographicProvinceTagIds = vm.PhysiographicProvinceTagIds?.ToList() ?? [],
                OtherTagIds = vm.OtherTagIds?.ToList() ?? [],

                Entrances = vm.Entrances?
                                .Select(e => e.ToAddEntrance())
                                .OrderByDescending(ee => ee.IsPrimary)
                                .ThenBy(ee => ee.ReportedOn).ToList()
                                .ToList()
                            ?? [],

                Files = vm.Files?
                            .Select(f => new EditFileMetadata { Id = f.Id })
                            .ToList()
                        ?? []
            };
        }

        public static AddEntrance ToAddEntrance(this EntranceVm vm)
        {
            ArgumentNullException.ThrowIfNull(vm);

            return new AddEntrance
            {
                Id = vm.Id,
                IsPrimary = vm.IsPrimary,
                LocationQualityTagId = vm.LocationQualityTagId,
                Name = vm.Name,
                Description = vm.Description,
                Latitude = vm.Latitude,
                Longitude = vm.Longitude,
                ElevationFeet = vm.ElevationFeet,
                PitFeet = vm.PitFeet ?? default,
                ReportedOn = vm.ReportedOn,

                EntranceStatusTagIds = vm.EntranceStatusTagIds?
                                           .ToList()
                                       ?? [],
                FieldIndicationTagIds = vm.FieldIndicationTagIds?
                                            .ToList()
                                        ?? [],
                EntranceHydrologyTagIds = vm.EntranceHydrologyTagIds?
                                              .ToList()
                                          ?? [],
                ReportedByNameTagIds = vm.ReportedByNameTagIds?
                                           .ToList()
                                       ?? [],

                EntranceOtherTagIds = []
            };
        }
    }