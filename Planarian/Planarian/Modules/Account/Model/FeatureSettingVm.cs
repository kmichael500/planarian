using System.ComponentModel.DataAnnotations;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;

namespace Planarian.Modules.Account.Model;

public class FeatureSettingVm
{
    [MaxLength(PropertyLength.Id)] public string Id { get; set; }
    public FeatureKey Key { get; set; }
    public bool IsEnabled { get; set; }
}