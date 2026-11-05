import { HttpClient } from "../../../Shared/Http/HttpClient";
import { SelectListItemDescriptionData } from "../../../Shared/Models/SelectListItem";
import { PermissionKey } from "../../Authentication/Models/PermissionKey";
import { PermissionType } from "../../Authentication/Models/PermissionType";
import { InviteUserRequest } from "../Models/InviteUserRequest";
import { InviteUserResultVm } from "../Models/InviteUserResultVm";
import {
  AccountAccessAction,
  AccountAccessChangeResultVm,
} from "../Models/AccountAccessChangeResultVm";
import { InvitationEmailAttemptVm } from "../Models/InvitationEmailHistoryVm";
import { PermissionSelectListData } from "../Models/PermissionSelectListData";
import { UserPermissionVm } from "../Models/UserAccessPermissionVm";
import {
  CavePermissionManagementVm,
  CreateUserCavePermissionsVm,
} from "../Models/UserLocationPermissionsVm";
import { UserManagerGridVm } from "../Models/UserManagerGridVm";

const baseUrl = "api/account/user-manager";
const AccountUserManagerService = {
  async GetAccountUsers(accountId?: string): Promise<UserManagerGridVm[]> {
    const response = await HttpClient.get<UserManagerGridVm[]>(
      baseUrl,
      accountId ? { headers: { "x-account": accountId } } : undefined
    );
    return response.data;
  },
  async GetUserById(userId: string): Promise<UserManagerGridVm> {
    const response = await HttpClient.get<UserManagerGridVm>(
      `${baseUrl}/${userId}`
    );
    return response.data;
  },
  async InviteUser(
    request: InviteUserRequest,
    accountId?: string
  ): Promise<InviteUserResultVm> {
    return (
      await HttpClient.post<InviteUserResultVm>(
        `${baseUrl}`,
        request,
        accountId ? { headers: { "x-account": accountId } } : undefined
      )
    ).data;
  },
  async ChangeAccess(
    userId: string,
    accountId: string,
    action: AccountAccessAction,
    notificationMessage?: string,
    sendEmail = true
  ): Promise<AccountAccessChangeResultVm> {
    const response = await HttpClient.post<AccountAccessChangeResultVm>(
      `${baseUrl}/${userId}/access-change`,
      {
        action,
        notificationMessage: sendEmail ? notificationMessage || null : null,
        sendEmail,
      },
      { headers: { "x-account": accountId } }
    );
    return response.data;
  },
  async ResendInvitation(userId: string, accountId?: string): Promise<void> {
    await HttpClient.post(
      `${baseUrl}/${userId}/resend-invitation`,
      {},
      accountId ? { headers: { "x-account": accountId } } : undefined
    );
  },
  async GetInvitationEmailHistory(
    userId: string
  ): Promise<InvitationEmailAttemptVm[]> {
    return (
      await HttpClient.get<InvitationEmailAttemptVm[]>(
        `${baseUrl}/${userId}/invitation-email-history`
      )
    ).data;
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
