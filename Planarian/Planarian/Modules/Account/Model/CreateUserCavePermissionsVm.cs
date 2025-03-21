using System.ComponentModel.DataAnnotations;
using Planarian.Model.Shared;

namespace Planarian.Modules.Account.Model;

public class CreateUserCavePermissionsVm
{
    public bool HasAllLocations { get; set; }
    public IEnumerable<string> CountyIds { get; set; } = new HashSet<string>();
    public IEnumerable<string> CaveIds { get; set; } = new HashSet<string>();
}

public class CavePermissionManagementVm
{

    public bool HasAllLocations { get; set; }
    public StateCountyValue StateCountyValues { get; set; } = null!;

    public IEnumerable<SelectListItem<string, CavePermissionManagementData>> CavePermissions { get; set; } =
        new HashSet<SelectListItem<string, CavePermissionManagementData>>();
}

public class StateCountyValue
{
    public List<string> States { get; set; } = new List<string>();
    public Dictionary<string, List<string>> CountiesByState { get; set; }
        = new Dictionary<string, List<string>>();
}

public class CavePermissionManagementData
{
    public string CountyId { get; set; } = null!;
    public bool RequestUserHasAccess { get; set; }
}