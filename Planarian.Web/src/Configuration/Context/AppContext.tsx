import React, {
  createContext,
  useCallback,
  useEffect,
  useMemo,
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
} from "../../Shared/Services/AppService";
import { ApiErrorResponse } from "../../Shared/Models/ApiErrorResponse";
import { SelectListItem } from "../../Shared/Models/SelectListItem";
import { hasPermission } from "../../Shared/Permissioning/PermissionHelpers";
import { useLocation } from "react-router-dom";
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
  const [isInitialized, setIsInitialized] = useState<boolean>(false);
  const [isLoading, setIsLoading] = useState<boolean>(true);
  const [initializedError, setInitializedError] =
    useState<ApiErrorResponse | null>(null);
  const [pendingInvitationCount, setPendingInvitationCount] =
    useState<number>(0);

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
    setPermissionKeys([]);
    setPermissions(defaultFeaturePermissions);
    setPendingInvitationCount(0);
    AuthenticationService.ClearRuntimeSession();
  }, []);

  const refreshPendingInvitations = useCallback(async () => {
    if (!AuthenticationService.IsAuthenticated()) {
      setPendingInvitationCount(0);
      return;
    }

    const pendingInvitations = await UserService.GetPendingInvitations();
    setPendingInvitationCount(pendingInvitations.length);
  }, []);

  const loadSession = useCallback(
    async (selectedAccountId?: string | null): Promise<AppInitializeVm> => {
      const appOptions = await AppService.InitializeApp(selectedAccountId);

      if (selectedAccountId !== undefined || !appOptions.currentUser?.id) {
        return appOptions;
      }

      const storedAccountId = AuthenticationService.GetStoredAccountId(
        appOptions.currentUser.id
      );
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
    async (appOptions: AppInitializeVm): Promise<void> => {
      const isAuthenticated = appOptions.currentUser != null;
      const resolvedCurrentAccountId =
        appOptions.currentUser?.currentAccountId ?? null;

      AuthenticationService.SyncSession(appOptions.currentUser);
      setCurrentUser(appOptions.currentUser);
      setCurrentAccountId(resolvedCurrentAccountId);
      setAccountIds(appOptions.accountIds);
      setPermissionKeys(appOptions.permissions);

      const featureSettings =
        isAuthenticated && resolvedCurrentAccountId
          ? await AccountService.GetFeatureSettings(false, resolvedCurrentAccountId)
          : [];
      setPermissions({ visibleFields: featureSettings });

      if (isAuthenticated) {
        await refreshPendingInvitations();
      } else {
        setPendingInvitationCount(0);
      }

      setInitializedError(null);
      setIsInitialized(true);
    },
    [refreshPendingInvitations]
  );

  const refreshSession = useCallback(
    async (selectedAccountId?: string | null): Promise<void> => {
      try {
        setIsLoading(true);
        const resolvedAccountId =
          selectedAccountId !== undefined
            ? selectedAccountId
            : AuthenticationService.GetAccountId() ?? undefined;

        const appOptions = await loadSession(resolvedAccountId);
        await applySessionState(appOptions);
      } catch (e) {
        const error = e as ApiErrorResponse;
        setInitializedError(error);
        throw error;
      } finally {
        setIsLoading(false);
      }
    },
    [applySessionState, loadSession]
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
    await AuthenticationService.Logout();
    clearSessionState();
    await refreshSession(null);
  }, [clearSessionState, refreshSession]);

  const switchAccount = useCallback(
    (accountId: string, redirectPath?: string | null) => {
      if (!currentUser?.id) {
        return;
      }

      AuthenticationService.SwitchAccount(accountId, redirectPath);
    },
    [currentUser]
  );

  useEffect(() => {
    const unregister = AuthenticationService.RegisterUnauthorizedHandler(() => {
      AuthenticationService.ResetAccountId();
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
    });

    return () => {
      unregister();
      AuthenticationService.ResetUnauthorizedHandling();
    };
  }, [clearSessionState]);

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
