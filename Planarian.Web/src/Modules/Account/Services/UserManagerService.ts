import { HttpClient } from "../../..";
import { InviteUserRequest } from "../Models/InviteUserRequest";
import { UserManagerGridVm } from "../Models/UserManagerGridVm";

const baseUrl = "api/account/user-manager";
const AccountUserManagerService = {
    async GetAccountUsers(): Promise<UserManagerGridVm[]> {
        const response = await HttpClient.get<UserManagerGridVm[]>(
            `${baseUrl}`
        );
        return response.data;
    },
    async InviteUser(request: InviteUserRequest): Promise<void> {
        await HttpClient.post(`${baseUrl}`, request);
    }
    ,
    async RevokeAccess(userId: string): Promise<void> {
        await HttpClient.delete(`${baseUrl}/${userId}`);
    },
    async ResendInvitation(userId: string): Promise<void> {
        await HttpClient.post(`${baseUrl}/${userId}/resend-invitation`, {});
    }

};
export { AccountUserManagerService };
