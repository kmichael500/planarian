import React, {
  createContext,
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
} from "react";
import { FeatureSettingVm } from "../../Modules/Account/Models/FeatureSettingVm";
import { AccountService } from "../../Modules/Account/Services/AccountService";
import { PermissionKey } from "../../Modules/Authentication/Models/PermissionKey";
import { BrowserLoginVm } from "../../Modules/Authentication/Models/BrowserLoginVm";
import { AuthenticationService } from "../../Modules/Authentication/Services/AuthenticationService";
import { UserService } from "../../Modules/User/UserService";
import {
  AppInitializeCurrentUserVm,
  AppInitializeVm,
  AppService,
  DisabledAccountVm,
} from "../../Shared/Services/AppService";
import { ApiErrorResponse, ApiExceptionType } from "../../Shared/Models/ApiErrorResponse";
import { SelectListItem } from "../../Shared/Models/SelectListItem";
import { hasPermission } from "../../Shared/Permissioning/PermissionHelpers";
import { useLocation, useNavigate } from "react-router-dom";
import { RequestRuntimeState } from "../../Shared/Http/RequestRuntimeState";
import { message } from "antd";
import { defaultAppContentStyle } from "../Layout/AppContentLayout";

interface FeaturePermissions {
  visibleFields: FeatureSettingVm[];
}

interface AppContextProps {
  isAuthenticated: boolean;
  currentUser: AppInitializeCurrentUserVm | null;
  currentAccountId: string | null;
  currentAccountName: string | null;
  userGroupPrefix: string | null;
  accountIds: SelectListItem<string>[];
  disabledAccountIds: DisabledAccountVm[];
  hasPermission: (permission: PermissionKey) => boolean;
  permissions: FeaturePermissions;
  setPermissions: (permissions: FeaturePermissions) => void;
  headerTitle: [React.ReactElement | string, string?];
  setHeaderTitle: (
    title: [React.ReactElement | string, string?],
    navigationTitle?: string
  ) => void;
  headerButtons: React.ReactElement[];
  setHeaderButtons: (buttons: React.ReactElement[]) => void;
  hideBodyPadding: boolean;
  setHideBodyPadding: (value: boolean) => void;
  isInitialized: boolean;
  isLoading: boolean;
  initializedError: ApiErrorResponse | null;
  refreshSession: (selectedAccountId?: string | null) => Promise<void>;
  login: (
    values: BrowserLoginVm,
    invitationCode?: string | null
  ) => Promise<void>;
  logout: () => Promise<void>;
  switchAccount: (accountId: string, redirectPath?: string | null) => void;
  defaultContentStyle: React.CSSProperties;
  pendingInvitationCount: number;
  refreshPendingInvitations: () => Promise<void>;
}

const defaultFeaturePermissions: FeaturePermissions = {
  visibleFields: [],
};

const emptyHeaderTitle: [React.ReactElement | string, string?] = ["", ""];
const emptyHeaderButtons: React.ReactElement[] = [];

export const AppContext = createContext<AppContextProps>({
  isAuthenticated: false,
  currentUser: null,
  currentAccountId: null,
  currentAccountName: null,
  userGroupPrefix: null,
  accountIds: [],
  disabledAccountIds: [],
  hasPermission: () => false,
  permissions: defaultFeaturePermissions,
  setPermissions: () => {},
  setHeaderTitle: () => {},
  headerTitle: ["", ""],
  headerButtons: [],
  setHeaderButtons: () => {},
  hideBodyPadding: false,
  setHideBodyPadding: () => {},
  isInitialized: false,
  isLoading: true,
  initializedError: null,
  refreshSession: async () => {},
  login: async () => {},
  logout: async () => {},
  switchAccount: () => {},
  defaultContentStyle: defaultAppContentStyle,
  pendingInvitationCount: 0,
  refreshPendingInvitations: async () => {},
});

interface AppProviderProps {
  children: React.ReactNode;
}

export const AppProvider: React.FC<AppProviderProps> = (props) => {
  const location = useLocation();
  const navigate = useNavigate();
  const [headerTitleState, setHeaderTitleState] = useState({
    pathname: location.pathname,
    value: emptyHeaderTitle,
  });
  const [headerButtonsState, setHeaderButtonsState] = useState({
    pathname: location.pathname,
    value: emptyHeaderButtons,
  });
  const [hideBodyPadding, setHideBodyPadding] = useState<boolean>(false);
  const [permissions, setPermissions] =
    useState<FeaturePermissions>(defaultFeaturePermissions);
  const [permissionKeys, setPermissionKeys] = useState<PermissionKey[]>([]);
  const [currentUser, setCurrentUser] =
    useState<AppInitializeCurrentUserVm | null>(null);
  const [currentAccountId, setCurrentAccountId] = useState<string | null>(null);
  const [accountIds, setAccountIds] = useState<SelectListItem<string>[]>([]);
  const [disabledAccountIds, setDisabledAccountIds] = useState<DisabledAccountVm[]>([]);
  const [isInitialized, setIsInitialized] = useState<boolean>(false);
  const [isLoading, setIsLoading] = useState<boolean>(true);
  const [initializedError, setInitializedError] =
    useState<ApiErrorResponse | null>(null);
  const [pendingInvitationCount, setPendingInvitationCount] =
    useState<number>(0);
  const sessionGeneration = useRef(0);
  const isCurrentSession = useCallback(
    (generation: number) => generation === sessionGeneration.current,
    []
  );

  const headerTitle =
    headerTitleState.pathname === location.pathname
      ? headerTitleState.value
      : emptyHeaderTitle;
  const headerButtons =
    headerButtonsState.pathname === location.pathname
      ? headerButtonsState.value
      : emptyHeaderButtons;
  const setHeaderTitle = useCallback(
    (title: [React.ReactElement | string, string?]) => {
      setHeaderTitleState({ pathname: location.pathname, value: title });
    },
    [location.pathname]
  );
  const setHeaderButtons = useCallback(
    (buttons: React.ReactElement[]) => {
      setHeaderButtonsState({ pathname: location.pathname, value: buttons });
    },
    [location.pathname]
  );

  const clearSessionState = useCallback(() => {
    setCurrentUser(null);
    setCurrentAccountId(null);
    setAccountIds([]);
    setDisabledAccountIds([]);
    setPermissionKeys([]);
    setPermissions(defaultFeaturePermissions);
    setPendingInvitationCount(0);
    AuthenticationService.ClearRuntimeSession();
  }, []);

  const refreshPendingInvitations = useCallback(async () => {
    const generation = sessionGeneration.current;
    if (!AuthenticationService.IsAuthenticated()) {
      setPendingInvitationCount(0);
      return;
    }

    const pendingInvitations = await UserService.GetPendingInvitations();
    if (isCurrentSession(generation) && AuthenticationService.IsAuthenticated()) {
      setPendingInvitationCount(pendingInvitations.length);
    }
  }, [isCurrentSession]);

  const loadSession = useCallback(
    async (selectedAccountId?: string | null): Promise<AppInitializeVm> => {
      const appOptions = await AppService.InitializeApp(selectedAccountId);

      if (selectedAccountId !== undefined || !appOptions.currentUser?.id) {
        return appOptions;
      }

      const storedAccountId = AuthenticationService.GetStoredAccountId(
        appOptions.currentUser.id
      );

      // Preserve the disabled selection until the user chooses another account.
      if (
        storedAccountId &&
        appOptions.disabledAccountIds?.some((account) => account.value === storedAccountId)
      ) {
        return {
          ...appOptions,
          currentUser: { ...appOptions.currentUser, currentAccountId: null },
          permissions: [],
        };
      }

      const hasStoredAccount =
        storedAccountId != null &&
        appOptions.accountIds.some((account) => account.value === storedAccountId);

      if (
        !hasStoredAccount ||
        storedAccountId === appOptions.currentUser.currentAccountId
      ) {
        return appOptions;
      }

      return await AppService.InitializeApp(storedAccountId);
    },
    []
  );

  const applySessionState = useCallback(
    async (appOptions: AppInitializeVm, generation: number): Promise<void> => {
      if (!isCurrentSession(generation)) return;

      const isAuthenticated = appOptions.currentUser != null;
      const resolvedCurrentAccountId =
        appOptions.currentUser?.currentAccountId ?? null;

      AppService.ApplyInitializationOptions(appOptions);
      AuthenticationService.SyncSession(appOptions.currentUser);
      setCurrentUser(appOptions.currentUser);
      setCurrentAccountId(resolvedCurrentAccountId);
      setAccountIds(appOptions.accountIds);
      const disabled = appOptions.disabledAccountIds ?? [];
      setDisabledAccountIds(disabled);
      setPermissionKeys(appOptions.permissions);

      // Account settings and invitations are supplementary. Their failure must not
      // invalidate a session whose authentication was already confirmed by bootstrap.
      let auxiliaryLoadFailed = false;
      setPermissions(defaultFeaturePermissions);
      if (isAuthenticated && resolvedCurrentAccountId) {
        try {
          const featureSettings = await AccountService.GetFeatureSettings(
            false, resolvedCurrentAccountId
          );
          if (!isCurrentSession(generation)) return;
          setPermissions({ visibleFields: featureSettings });
        } catch (error) {
          if (!isCurrentSession(generation)) return;
          if ((error as ApiErrorResponse).statusCode === 401) throw error;
          auxiliaryLoadFailed = true;
        }
      }

      if (!isCurrentSession(generation)) return;
      if (isAuthenticated) {
        try {
          await refreshPendingInvitations();
        } catch (error) {
          if (!isCurrentSession(generation)) return;
          if ((error as ApiErrorResponse).statusCode === 401) throw error;
          setPendingInvitationCount(0);
          auxiliaryLoadFailed = true;
        }
      } else {
        setPendingInvitationCount(0);
      }

      if (!isCurrentSession(generation)) return;
      if (auxiliaryLoadFailed) {
        message.warning("Some account information could not be loaded. Refresh to try again.");
      }
      setInitializedError(null);
      setIsInitialized(true);
    },
    [isCurrentSession, refreshPendingInvitations]
  );

  const refreshSession = useCallback(
    async (selectedAccountId?: string | null): Promise<void> => {
      const generation = ++sessionGeneration.current;
      try {
        setIsLoading(true);
        const resolvedAccountId =
          selectedAccountId !== undefined
            ? selectedAccountId
            : AuthenticationService.GetAccountId() ?? undefined;

        const appOptions = await loadSession(resolvedAccountId);
        await applySessionState(appOptions, generation);
      } catch (e) {
        if (isCurrentSession(generation)) {
          const error = e as ApiErrorResponse;
          setInitializedError(error);
          throw error;
        }
      } finally {
        if (isCurrentSession(generation)) setIsLoading(false);
      }
    },
    [applySessionState, isCurrentSession, loadSession]
  );

  const login = useCallback(
    async (
      values: BrowserLoginVm,
      invitationCode?: string | null
    ): Promise<void> => {
      await AuthenticationService.Login(values, invitationCode);
      await refreshSession();
    },
    [refreshSession]
  );

  const logout = useCallback(async () => {
    const generation = ++sessionGeneration.current;
    await AuthenticationService.Logout();
    if (!isCurrentSession(generation)) return;
    clearSessionState();
    await refreshSession(null);
  }, [clearSessionState, isCurrentSession, refreshSession]);

  const switchAccount = useCallback(
    (accountId: string, redirectPath?: string | null) => {
      if (!currentUser?.id) {
        return;
      }

      ++sessionGeneration.current;
      AuthenticationService.SwitchAccount(accountId, redirectPath);
    },
    [currentUser]
  );

  useEffect(() => {
    const unregister = AuthenticationService.RegisterUnauthorizedHandler((reason) => {
      const generation = ++sessionGeneration.current;
      const wasAuthenticated = AuthenticationService.IsAuthenticated();
      if (reason === "unauthorized" || !wasAuthenticated) {
        AuthenticationService.ResetAccountId();
      }
      RequestRuntimeState.setCurrentAccountId(null);
      setCurrentAccountId(null);
      setPermissionKeys([]);
      setPermissions(defaultFeaturePermissions);

      const redirectToLogin = () => {
        if (!isCurrentSession(generation)) return;
        clearSessionState();
        setInitializedError(null);
        setIsInitialized(true);
        setIsLoading(false);

        const redirectPath = `${window.location.pathname}${window.location.search}${window.location.hash}`;
        const isLoginPage = window.location.pathname.startsWith("/login");
        if (!isLoginPage) {
          window.location.assign(
            `/login?redirectUrl=${encodeURIComponent(redirectPath)}`
          );
        } else {
          AuthenticationService.ResetUnauthorizedHandling();
        }
      };

      if (reason === "unauthorized" || !wasAuthenticated) {
        redirectToLogin();
        return;
      }

      // A disabled membership invalidates the selected account, not the user's login.
      // Bootstrap without an account header before treating the session as expired.
      void (async () => {
        setIsLoading(true);
        try {
          const appOptions = await loadSession();
          if (!isCurrentSession(generation)) return;
          if (!appOptions.currentUser) {
            redirectToLogin();
            return;
          }

          await applySessionState(appOptions, generation);
          if (!isCurrentSession(generation)) return;
          navigate("/", { replace: true });
          AuthenticationService.ResetUnauthorizedHandling();
        } catch (error) {
          if (!isCurrentSession(generation)) return;
          const apiError = error as ApiErrorResponse;
          if (
            apiError.statusCode === 401 ||
            apiError.errorCode === ApiExceptionType.Unauthorized
          ) {
            redirectToLogin();
          } else {
            // A transient bootstrap failure does not prove the login cookie expired.
            // Hide stale account data, and allow a page reload to retry initialization.
            clearSessionState();
            setInitializedError({
              ...apiError,
              message: "Could not refresh your account access. Reload to try again.",
            });
            setIsInitialized(false);
            AuthenticationService.ResetUnauthorizedHandling();
          }
        } finally {
          if (isCurrentSession(generation)) setIsLoading(false);
        }
      })();
    });

    return unregister;
  }, [applySessionState, clearSessionState, isCurrentSession, loadSession, navigate]);

  // Re-registering the handler (for example after navigation) is not a logout.
  // Only unmounting this provider invalidates its pending session work.
  useEffect(() => {
    return () => {
      ++sessionGeneration.current;
      AuthenticationService.ResetUnauthorizedHandling();
    };
  }, []);

  useEffect(() => {
    void refreshSession().catch(() => {});
  }, [refreshSession]);

  const currentAccountName = useMemo(() => {
    if (!currentAccountId) {
      return null;
    }

    return (
      accountIds.find((account) => account.value === currentAccountId)?.display ??
      null
    );
  }, [accountIds, currentAccountId]);

  const userGroupPrefix = useMemo(() => {
    if (!currentUser?.id || !currentAccountId) {
      return null;
    }

    return `${currentUser.id}-${currentAccountId}`;
  }, [currentAccountId, currentUser]);

  const hasCurrentPermission = useCallback(
    (permission: PermissionKey) => hasPermission(permissionKeys, permission),
    [permissionKeys]
  );

  const isAuthenticated = currentUser != null;

  return (
    <AppContext.Provider
      value={{
        isAuthenticated,
        currentUser,
        currentAccountId,
        currentAccountName,
        userGroupPrefix,
        accountIds,
        disabledAccountIds,
        hasPermission: hasCurrentPermission,
        permissions,
        setPermissions,
        headerTitle,
        setHeaderTitle,
        headerButtons,
        setHeaderButtons,
        hideBodyPadding,
        setHideBodyPadding,
        isInitialized,
        isLoading,
        initializedError,
        refreshSession,
        login,
        logout,
        switchAccount,
        defaultContentStyle: defaultAppContentStyle,
        pendingInvitationCount,
        refreshPendingInvitations,
      }}
    >
      {props.children}
    </AppContext.Provider>
  );
};
