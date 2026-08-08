import { HttpClient, registerUnauthorizedHandler } from "../../../Shared/Http/HttpClient";
import { RequestRuntimeState } from "../../../Shared/Http/RequestRuntimeState";
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
let directUnauthorizedHandler: UnauthorizedHandler | null = null;
let isHandlingUnauthorized = false;

const invokeUnauthorizedHandler = () => {
  if (isHandlingUnauthorized) {
    return;
  }

  isHandlingUnauthorized = true;
  directUnauthorizedHandler?.();
};

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
    RequestRuntimeState.setCurrentAccountId(currentUser?.currentAccountId ?? null);

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
    RequestRuntimeState.setCurrentAccountId(null);
  },
  RegisterUnauthorizedHandler(handler: UnauthorizedHandler): () => void {
    directUnauthorizedHandler = handler;
    const unregisterHttpHandler = registerUnauthorizedHandler(invokeUnauthorizedHandler);
    return () => {
      if (directUnauthorizedHandler === handler) {
        directUnauthorizedHandler = null;
      }
      unregisterHttpHandler();
    };
  },
  HandleUnauthorized(): void {
    invokeUnauthorizedHandler();
  },
  ResetUnauthorizedHandling(): void {
    isHandlingUnauthorized = false;
  },
  IsAuthenticated(): boolean {
    return sessionSnapshot.currentUser != null;
  },
  GetAccountId(): string | null {
    return sessionSnapshot.currentUser?.currentAccountId ?? null;
  },
  SwitchAccount(accountId: string, redirectPath?: string | null): void {
    const userId = sessionSnapshot.currentUser?.id;
    if (userId) {
      this.SetStoredAccountId(userId, accountId);
    }

    const targetPath =
      redirectPath ??
      `${window.location.pathname}${window.location.search}${window.location.hash}`;
    window.location.assign(targetPath);
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
