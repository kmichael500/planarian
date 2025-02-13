import { SelectListItem } from "../../../Shared/Models/SelectListItem";

export interface UserLocationPermissionsVm {
  hasAllLocations: boolean;
  countyIds: string[];
  caveIds: SelectListItem<UserLocationPermissionCaveVm>[];
}

export interface UserLocationPermissionCaveVm {
  caveId: string;
  countyId: string;
}
