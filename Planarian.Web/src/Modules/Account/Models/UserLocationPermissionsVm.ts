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
  requestUserHasAccess: boolean;
}

export interface StateCountyValue {
  states: string[];

  countiesByState: Record<string, string[]>;
}

export interface CreateUserCavePermissionsVm {
  hasAllLocations: boolean;
  stateIds: string[];
  countyIds: string[];
  caveIds: string[];
}
