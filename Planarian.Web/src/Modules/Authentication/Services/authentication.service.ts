import { HttpClient } from "../../..";
import { UserLoginVm } from "../Models/UserLoginVm";

const baseUrl = "api/authentication";
const AuthenticationService = {
  async Login(values: UserLoginVm): Promise<string> {
    const response = await HttpClient.post<string>(`${baseUrl}/login`, values);
    return response.data;
  },
  async Logout(): Promise<string> {
    const response = await HttpClient.post<string>(`${baseUrl}/logout`, {});
    return response.data;
  },
};

export { AuthenticationService };
