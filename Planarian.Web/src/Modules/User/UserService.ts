import { HttpClient } from "../..";
import { UserVm } from "./Models/UserVm";

const baseUrl = "api/users";
const UserService = {
  async GetCurrentUser(): Promise<UserVm> {
    const response = await HttpClient.get<UserVm>(`${baseUrl}/current`);
    return response.data;
  },
  async UpdateCurrentUser(user: UserVm): Promise<void> {
    const response = await HttpClient.put(`${baseUrl}/current`, user);
  },
  async UpdateCurrentUserPassword(password: string): Promise<void> {
    const response = await HttpClient.put(
      `${baseUrl}/current/password`,
      password,
      {
        headers: { "Content-Type": "application/json" },
      }
    );
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
    const response = await HttpClient.post(
      `${baseUrl}/confirm-email?code=${code}`,
      {},
      {
        headers: { "Content-Type": "application/json" },
      }
    );
  },
};

export { UserService };
