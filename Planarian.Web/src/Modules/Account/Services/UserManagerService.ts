import { HttpClient } from "../../..";
import { SelectListItemDescriptionData } from "../../../Shared/Models/SelectListItem";
import { PermissionKey } from "../../Authentication/Models/PermissionKey";
import { InviteUserRequest } from "../Models/InviteUserRequest";
import { PermissionSelectListData } from "../Models/PermissionSelectListData";
import {
  CavePermissionManagementVm,
  CreateUserCavePermissionsVm,
} from "../Models/UserLocationPermissionsVm";
import { UserManagerGridVm } from "../Models/UserManagerGridVm";

const baseUrl = "api/account/user-manager";
const AccountUserManagerService = {
  async GetAccountUsers(): Promise<UserManagerGridVm[]> {
    const response = await HttpClient.get<UserManagerGridVm[]>(`${baseUrl}`);
    return response.data;
  },
  async GetUserById(userId: string): Promise<UserManagerGridVm> {
    const response = await HttpClient.get<UserManagerGridVm>(
      `${baseUrl}/${userId}`
    );
    return response.data;
  },
  async InviteUser(request: InviteUserRequest): Promise<void> {
    await HttpClient.post(`${baseUrl}`, request);
  },
  async RevokeAccess(userId: string): Promise<void> {
    await HttpClient.delete(`${baseUrl}/${userId}`);
  },
  async ResendInvitation(userId: string): Promise<void> {
    await HttpClient.post(`${baseUrl}/${userId}/resend-invitation`, {});
  },

  //#region Manage Permissions

  async GetLocationPermissions(
    userId: string,
    permissionKey: PermissionKey
  ): Promise<CavePermissionManagementVm> {
    const response = await HttpClient.get<CavePermissionManagementVm>(
      `${baseUrl}/${userId}/location-permissions/${permissionKey}`
    );
    return response.data;
  },

  async UpdateLocationPermissions(
    userId: string,
    permissionKey: PermissionKey,
    newPermissions: CreateUserCavePermissionsVm
  ): Promise<void> {
    await HttpClient.put<void>(
      `${baseUrl}/${userId}/location-permissions/${permissionKey}`,
      newPermissions
    );
  },

  //#endregion

  //#region Select

  async GetPermissionSelectList(): Promise<
    SelectListItemDescriptionData<string, PermissionSelectListData>[]
  > {
    const response = await HttpClient.get<
      SelectListItemDescriptionData<string, PermissionSelectListData>[]
    >(`${baseUrl}/select/permissions`);
    return response.data;
  },
};
export { AccountUserManagerService };
