import { HttpClient } from "../../..";
import { TOKEN_KEY } from "../../../Shared/Constants/TokenKeyConstant";
import { UserLoginVm } from "../Models/UserLoginVm";
import jwt_decode from "jwt-decode";
import { isNullOrWhiteSpace } from "../../../Shared/Helpers/StringHelpers";
import { AppOptions, AppService } from "../../../Shared/Services/AppService";
import { NotFoundError } from "../../../Shared/Exceptions/PlanarianErrors";
const NAME_CLAIM_KEY =
  "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name";

interface JwtPayload {
  [key: string]: any;
  name: string;
  id: string;
  currentAccountId: string;
}

const baseUrl = "api/authentication";
const AuthenticationService = {
  subscribers: [] as Array<() => void>,

  onAuthChange(callback: () => void) {
    this.subscribers.push(callback);
    return () => {
      this.subscribers = this.subscribers.filter((sub) => sub !== callback);
    };
  },

  notifyAuthChange() {
    this.subscribers.forEach((callback) => callback());
  },
  async Login(values: UserLoginVm, invitationCode: string | null = null): Promise<string> {
    if (!isNullOrWhiteSpace(invitationCode)) {
      values.invitationCode = invitationCode;
    }
    const response = await HttpClient.post<string>(`${baseUrl}/login`, values);
    this.SetToken(response.data);
    const accountId = this.GetAccountId();
    if (accountId) {
      this.SwitchAccount(accountId);
    }
    await AppService.InitializeApp();
    this.notifyAuthChange();
    return response.data;
  },
  async Logout(): Promise<void> {
    this.RemoveToken();
    await AppService.InitializeApp();
    this.notifyAuthChange();
  },
  GetToken(): string | null {
    return localStorage.getItem(TOKEN_KEY);
  },
  SetToken(token: string): void {
    HttpClient.defaults.headers.common["Authorization"] = `Bearer ${token}`;

    localStorage.setItem(TOKEN_KEY, token);
  },
  RemoveToken(): void {
    localStorage.removeItem(TOKEN_KEY);

    this.notifyAuthChange();
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
  GetAccountName(): string {
    const name = AppOptions.accountIds.find(
      (x) => x.value === this.GetAccountId()
    )?.display;
    if (!name) throw new NotFoundError("account name");

    return name;
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
    const userId = this.GetUserId();
    if (userId == null) return null;

    // allows user to switch tabs for different accounts
    const sessionAccountId = sessionStorage.getItem(
      currentIdStorageKey(userId)
    );
    if (sessionAccountId) {
      return sessionAccountId;
    }

    // allows user to login with last account as default
    const currentAccountId = localStorage.getItem(currentIdStorageKey(userId));
    if (currentAccountId) {
      return currentAccountId;
    }

    // if no account is found, use the account from the token
    const token = this.GetToken();
    if (token) {
      const payload = jwt_decode<JwtPayload>(token) as JwtPayload;
      return payload ? payload.currentAccountId : null;
    }
    return null;
  },
  GetUserGroupPrefix(): string {
    var accountId = this.GetAccountId();
    var userId = this.GetUserId();
    return `${userId}-${accountId}`;
  },
  SwitchAccount(accountId: string): void {
    const userId = this.GetUserId();
    if (userId == null) throw new NotFoundError("user");
    sessionStorage.setItem(currentIdStorageKey(userId), accountId);
    localStorage.setItem(currentIdStorageKey(userId), accountId);
  },
  ResetAccountId(): void {
    const userId = this.GetUserId();
    if (userId == null) throw new NotFoundError("user");
    sessionStorage.removeItem(currentIdStorageKey(userId));
    localStorage.removeItem(currentIdStorageKey(userId));
  },
};

const currentIdStorageKey = (userId: string) => `currentAccountId-${userId}`;

export { AuthenticationService };
