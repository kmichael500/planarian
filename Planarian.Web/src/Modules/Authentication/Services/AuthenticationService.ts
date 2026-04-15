import { HttpClient } from "../../..";
import { BrowserLoginVm } from "../Models/BrowserLoginVm";

const baseUrl = "api/authentication";

interface SessionCurrentUserSnapshot {
  id: string;
  currentAccountId: string | null;
}

interface SessionSnapshot {
  currentUser: SessionCurrentUserSnapshot | null;
}

type UnauthorizedHandler = () => void;

const currentIdStorageKey = (userId: string) => `currentAccountId-${userId}`;

const getStorage = () =>
  typeof window === "undefined"
    ? null
    : {
        local: window.localStorage,
        session: window.sessionStorage,
      };

let sessionSnapshot: SessionSnapshot = {
  currentUser: null,
};
let unauthorizedHandler: UnauthorizedHandler | null = null;
let isHandlingUnauthorized = false;

const AuthenticationService = {
  async Login(
    values: BrowserLoginVm,
    invitationCode: string | null = null
  ): Promise<void> {
    const payload = {
      ...values,
      invitationCode: invitationCode ?? values.invitationCode,
    };
    await HttpClient.post<void>(`${baseUrl}/login`, payload);
  },
  async Logout(): Promise<void> {
    await HttpClient.post(`${baseUrl}/logout`, {});
  },
  SyncSession(currentUser: SessionCurrentUserSnapshot | null): void {
    sessionSnapshot = {
      currentUser,
    };

    const storage = getStorage();
    if (!storage) {
      return;
    }

    if (currentUser?.currentAccountId) {
      this.SetStoredAccountId(currentUser.id, currentUser.currentAccountId);
    }
  },
  ClearRuntimeSession(): void {
    sessionSnapshot = {
      currentUser: null,
    };
  },
  RegisterUnauthorizedHandler(handler: UnauthorizedHandler): () => void {
    unauthorizedHandler = handler;
    return () => {
      if (unauthorizedHandler === handler) {
        unauthorizedHandler = null;
      }
    };
  },
  HandleUnauthorized(): void {
    if (isHandlingUnauthorized || !unauthorizedHandler) {
      return;
    }

    isHandlingUnauthorized = true;
    unauthorizedHandler?.();
  },
  ResetUnauthorizedHandling(): void {
    isHandlingUnauthorized = false;
  },
  GetAccountId(): string | null {
    return sessionSnapshot.currentUser?.currentAccountId ?? null;
  },
  SetStoredAccountId(userId: string, accountId: string): void {
    const storage = getStorage();
    if (!storage) {
      return;
    }

    storage.session.setItem(currentIdStorageKey(userId), accountId);
    storage.local.setItem(currentIdStorageKey(userId), accountId);
  },
  GetStoredAccountId(userId: string | null | undefined): string | null {
    const storage = getStorage();
    if (!storage || !userId) {
      return null;
    }

    return (
      storage.session.getItem(currentIdStorageKey(userId)) ??
      storage.local.getItem(currentIdStorageKey(userId))
    );
  },
  ResetAccountId(): void {
    const storage = getStorage();
    if (!storage) {
      return;
    }

    const targetUserId = sessionSnapshot.currentUser?.id;
    if (!targetUserId) {
      return;
    }

    storage.session.removeItem(currentIdStorageKey(targetUserId));
    storage.local.removeItem(currentIdStorageKey(targetUserId));
  },
};

export { AuthenticationService };
