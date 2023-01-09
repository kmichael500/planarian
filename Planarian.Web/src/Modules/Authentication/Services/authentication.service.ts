import { HttpClient } from "../../..";
import { TOKEN_KEY } from "../../../Shared/Constants/constants";
import { UserLoginVm } from "../Models/UserLoginVm";
import jwt_decode from "jwt-decode";
import { isNullOrWhiteSpace } from "../../../Shared/Helpers/StringHelpers";

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
  RemoveToken(): void {
    delete HttpClient.defaults.headers.common["Authorization"];
    localStorage.removeItem(TOKEN_KEY);
  },
  IsAuthenticated(): boolean {
    // check if token is valid
    const token = this.GetToken();
    if (this.IsTokenExpired()) return false;

    return !isNullOrWhiteSpace(token);
  },
  IsTokenExpired(): boolean {
    try {
      const token = this.GetToken();
      if (!token) return true;
      const decodedToken = jwt_decode(token) as { exp: number };
      const currentTime = Date.now() / 1000;
      return decodedToken.exp < currentTime;
    } catch (err) {
      return true;
    }
  },
};

export { AuthenticationService };
