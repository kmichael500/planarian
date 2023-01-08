import { HttpClient } from "../../..";
import { TOKEN_KEY } from "../../../Shared/Constants/constants";
import { UserLoginVm } from "../Models/UserLoginVm";

const baseUrl = "api/authentication";
const AuthenticationService = {
  async Login(values: UserLoginVm): Promise<string> {
    const response = await HttpClient.post<string>(`${baseUrl}/login`, values);
    this.SetToken(response.data);
    return response.data;
  },
  async Logout(): Promise<string> {
    const response = await HttpClient.post<string>(`${baseUrl}/logout`, {});
    return response.data;
  },
  GetToken(): string | null {
    return localStorage.getItem(TOKEN_KEY);
  },
  SetToken(token: string): void {
    HttpClient.defaults.headers.common["Authorization"] = `Bearer ${token}`;

    localStorage.setItem(TOKEN_KEY, token);
  },
};

export { AuthenticationService };
