import { SelectListItemWithData } from "../../../Shared/Models/SelectListItem";

export interface CavePermissionManagementVm {
  hasAllLocations: boolean;
  stateCountyValues: StateCountyValue;
  cavePermissions: SelectListItemWithData<
    string,
    CavePermissionManagementData
  >[];
}

export interface CavePermissionManagementData {
  countyId: string;
}

export interface StateCountyValue {
  states: string[];

  countiesByState: Record<string, string[]>;
}

export interface CreateUserCavePermissionsVm {
  hasAllLocations: boolean;
  countyIds: string[];
  caveIds: string[];
}
