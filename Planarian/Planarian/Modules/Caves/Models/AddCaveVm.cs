// using System.Collections;
// using System.ComponentModel.DataAnnotations;
// using Planarian.Model.Database.Entities.RidgeWalker.ViewModels;
// using Planarian.Model.Shared;
// using Planarian.Modules.Files.Controllers;
//
// namespace Planarian.Modules.Caves.Models;
//
// public class AddCaveVm : AddCave
// {
//     public AddCaveVm(AddCave cave)
//     {
//         Id = cave.Id;
//         Name = cave.Name;
//         AlternateNames = cave.AlternateNames;
//
//         CountyId = cave.CountyId;
//         StateId = cave.StateId;
//         LengthFeet = cave.LengthFeet;
//         DepthFeet = cave.DepthFeet;
//         MaxPitDepthFeet = cave.MaxPitDepthFeet;
//         NumberOfPits = cave.NumberOfPits;
//
//         Narrative = cave.Narrative;
//         ReportedOn = cave.ReportedOn;
//         ReportedByName = cave.ReportedByName;
//
//         Entrances = cave.Entrances;
//
//         Files = cave.Files;
//
//         GeologyTagIds = cave.GeologyTagIds;
//
//         ReportedByNameTagIds = cave.ReportedByNameTagIds;
//
//         BiologyTagIds = cave.BiologyTagIds;
//
//         ArcheologyTagIds = cave.ArcheologyTagIds;
//
//         CartographerNameTagIds = cave.CartographerNameTagIds;
//
//         MapStatusTagIds = cave.MapStatusTagIds;
//
//         GeologicAgeTagIds = cave.GeologicAgeTagIds;
//
//         PhysiographicProvinceTagIds = cave.PhysiographicProvinceTagIds;
//
//         OtherTagIds = cave.OtherTagIds;
//     }
//
//     public static AddCaveVm FromAddCave(AddCave cave) => new AddCaveVm(cave);
// }
