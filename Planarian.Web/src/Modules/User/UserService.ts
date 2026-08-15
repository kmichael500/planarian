import { HttpClient } from "../../Shared/Http/HttpClient";
import { AcceptInvitationVm } from "./Models/AcceptInvitationVm";
import { NameProfilePhotoVm } from "./Models/NameProfilePhotoVm";
import { UpdateCurrentUserVm } from "./Models/UpdateCurrentUserVm";
import { UpdatePasswordVm } from "./Models/UpdatePasswordVm";
import { UserVm } from "./Models/UserVm";

const baseUrl = "api/users";
const UserService = {
  async GetCurrentUser(): Promise<UserVm> {
    const response = await HttpClient.get<UserVm>(`${baseUrl}/current`);
    return response.data;
  },
  async GetUsersName(userId: string): Promise<NameProfilePhotoVm> {
    const response = await HttpClient.get<NameProfilePhotoVm>(
      `${baseUrl}/${userId}`
    );
    return response.data;
  },
  async UpdateCurrentUser(user: UpdateCurrentUserVm): Promise<void> {
    await HttpClient.put(`${baseUrl}/current`, user);
  },
  async UpdateCurrentUserPassword(
    request: Pick<UpdatePasswordVm, "currentPassword" | "password">
  ): Promise<void> {
    await HttpClient.put(`${baseUrl}/current/password`, request);
  },
  async SendPasswordResetEmail(email: string): Promise<void> {
    const response = await HttpClient.post(
      `${baseUrl}/reset-password/email/${encodeURIComponent(email)}`,
      {}
    );
  },
  async ResetPassword(code: string, password: string): Promise<void> {
    const response = await HttpClient.post(
      `${baseUrl}/reset-password?code=${code}`,
      password,
      {
        headers: { "Content-Type": "application/json" },
      }
    );
  },
  async ConfirmEmail(code: string): Promise<void> {
    await HttpClient.post(
      `${baseUrl}/confirm-email?code=${code}`,
      {},
      {
        headers: { "Content-Type": "application/json" },
      }
    );
  },
  async ResendEmailConfirmation(emailAddress: string): Promise<void> {
    await HttpClient.post(`${baseUrl}/confirm-email/resend`, { emailAddress });
  },
  async GetInvitation(invitationCode: string): Promise<AcceptInvitationVm> {
    const response = await HttpClient.get<AcceptInvitationVm>(
      `${baseUrl}/invitations/${invitationCode}`
    );
    return response.data;
  },
  async GetPendingInvitations(): Promise<AcceptInvitationVm[]> {
    const response = await HttpClient.get<AcceptInvitationVm[]>(
      `${baseUrl}/invitations`
    );
    return response.data;
  },

  async AcceptInvitation(invitationCode: string): Promise<void> {
    const response = await HttpClient.post(
      `${baseUrl}/invitations/${invitationCode}/accept`,
      {}
    );
  },
  async DeclineInvitation(invitationCode: string): Promise<void> {
    const response = await HttpClient.post(
      `${baseUrl}/invitations/${invitationCode}/decline`,
      {}
    );
  },
};

export { UserService };
