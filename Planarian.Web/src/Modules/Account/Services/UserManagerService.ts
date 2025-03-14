import { HttpClient } from "../../..";
import { SelectListItemDescriptionData } from "../../../Shared/Models/SelectListItem";
import { PermissionKey } from "../../Authentication/Models/PermissionKey";
import { PermissionType } from "../../Authentication/Models/PermissionType";
import { InviteUserRequest } from "../Models/InviteUserRequest";
import { PermissionSelectListData } from "../Models/PermissionSelectListData";
import { UserPermissionVm } from "../Models/UserAccessPermissionVm";
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

  async GetCavePermissions(
    userId: string,
    permissionKey: PermissionKey
  ): Promise<CavePermissionManagementVm> {
    const response = await HttpClient.get<CavePermissionManagementVm>(
      `${baseUrl}/${userId}/cave-permissions/${permissionKey}`
    );
    return response.data;
  },

  async UpdateCavePermissions(
    userId: string,
    permissionKey: PermissionKey,
    newPermissions: CreateUserCavePermissionsVm
  ): Promise<void> {
    await HttpClient.put<void>(
      `${baseUrl}/${userId}/cave-permissions/${permissionKey}`,
      newPermissions
    );
  },

  async GetUserPermissions(userId: string): Promise<UserPermissionVm[]> {
    const response = await HttpClient.get<UserPermissionVm[]>(
      `${baseUrl}/${userId}/user-permissions`
    );
    return response.data;
  },

  async AddUserPermission(
    userId: string,
    permissionKey: string
  ): Promise<void> {
    await HttpClient.post<void>(
      `${baseUrl}/${userId}/user-permissions/${permissionKey}`
    );
  },

  async RemoveUserPermission(
    userId: string,
    permissionKey: string
  ): Promise<void> {
    await HttpClient.delete<void>(
      `${baseUrl}/${userId}/user-permissions/${permissionKey}`
    );
  },

  //#endregion

  //#region Select

  async GetPermissionSelectList(
    permissionType: PermissionType
  ): Promise<
    SelectListItemDescriptionData<string, PermissionSelectListData>[]
  > {
    const queryString = `permissionType=${permissionType}`;
    const response = await HttpClient.get<
      SelectListItemDescriptionData<string, PermissionSelectListData>[]
    >(`${baseUrl}/select/permissions?${queryString}`);
    return response.data;
  },
};
export { AccountUserManagerService };
