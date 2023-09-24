import { HttpClient } from "../../..";
import { TOKEN_KEY } from "../../../Shared/Constants/TokenKeyConstant";
import { UserLoginVm } from "../Models/UserLoginVm";
import jwt_decode from "jwt-decode";
import { isNullOrWhiteSpace } from "../../../Shared/Helpers/StringHelpers";
const NAME_CLAIM_KEY =
  "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name";

interface JwtPayload {
  [key: string]: any;
  name: string;
  id: string;
  accountId: string;
}

const baseUrl = "api/authentication";
const AuthenticationService = {
  async Login(values: UserLoginVm): Promise<string> {
    const response = await HttpClient.post<string>(`${baseUrl}/login`, values);
    this.SetToken(response.data);
    return response.data;
  },
  Logout(): void {
    this.RemoveToken();
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
      const expiresAt = new Date(decodedToken.exp * 1000);
      const now = new Date();
      return expiresAt < now;
    } catch (err) {
      return true;
    }
  },
  GetName(): string | null {
    const token = this.GetToken();
    if (token) {
      const payload = jwt_decode(token) as JwtPayload;
      return payload ? payload[NAME_CLAIM_KEY] : null;
    }
    return null;
  },
  GetUserId(): string | null {
    const token = this.GetToken();
    if (token) {
      const payload = jwt_decode(token) as JwtPayload;
      return payload ? payload.id : null;
    }
    return null;
  },
  GetAccountId(): string | null {
    const token = this.GetToken();
    if (token) {
      const payload = jwt_decode<JwtPayload>(token) as JwtPayload;
      return payload ? payload.accountId : null;
    }
    return null;
  },
  GetUserGroupPrefix(): string {
    var accountId = this.GetAccountId();
    var userId = this.GetUserId();
    return `${userId}-${accountId}`;
  },
};

export { AuthenticationService };
