using System.ComponentModel.DataAnnotations;
using Planarian.Model.Database.Entities.RidgeWalker.ViewModels;
using Planarian.Model.Shared;

namespace Planarian.Modules.Caves.Models;

public class ProposedChangeRequestVm
{
    [MaxLength(PropertyLength.Id)] public string Id { get; set; } 
    public AddCave Cave { get; set; } = null!;
}