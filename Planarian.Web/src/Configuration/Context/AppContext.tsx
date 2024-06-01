import React, { createContext, useState, useEffect, useCallback } from "react";
import { AuthenticationService } from "../../Modules/Authentication/Services/AuthenticationService";
import { FeatureSettingVm } from "../../Modules/Account/Models/FeatureSettingVm";
import { AccountService } from "../../Modules/Account/Services/AccountService";
import { AppService } from "../../Shared/Services/AppService";
import { ApiErrorResponse } from "../../Shared/Models/ApiErrorResponse";

interface Permissions {
  visibleFields: FeatureSettingVm[];
}

interface AppContextProps {
  isAuthenticated: boolean;
  setIsAuthenticated: (isAuthenticated: boolean) => void;
  headerTitle: [React.ReactElement | string, string?];
  setHeaderTitle: (
    title: [React.ReactElement | string, string?],
    navigationTitle?: string
  ) => void;
  headerButtons: React.ReactElement[];
  setHeaderButtons: (buttons: React.ReactElement[]) => void;
  hideBodyPadding: boolean;
  setHideBodyPadding: (value: boolean) => void;
  permissions: Permissions;
  setPermissions: (permissions: Permissions) => void;
  isInitialized: boolean;
  isLoading: boolean;
  initializedError: string | null;
}

export const AppContext = createContext<AppContextProps>({
  isAuthenticated: AuthenticationService.IsAuthenticated(),
  setIsAuthenticated: () => {},
  setHeaderTitle: () => {},
  headerTitle: ["", ""],
  headerButtons: [],
  setHeaderButtons: () => {},
  hideBodyPadding: false,
  setHideBodyPadding: () => {},
  permissions: { visibleFields: [] },
  setPermissions: () => {},
  isInitialized: false,
  isLoading: true,
  initializedError: null,
});

interface AppProviderProps {
  children: React.ReactNode;
}

export const AppProvider: React.FC<AppProviderProps> = (props) => {
  const [isAuthenticated, setIsAuthenticated] = useState<boolean>(
    AuthenticationService.IsAuthenticated()
  );
  const [headerTitle, setHeaderTitle] = useState<
    [React.ReactElement | string, string?]
  >(["", ""]);
  const [headerButtons, setHeaderButtons] = useState<React.ReactElement[]>([]);
  const [hideBodyPadding, setHideBodyPadding] = useState<boolean>(false);
  const [permissions, setPermissions] = useState<Permissions>({
    visibleFields: [],
  });
  const [isInitialized, setIsInitialized] = useState<boolean>(false);
  const [isLoading, setIsLoading] = useState<boolean>(true);
  const [initializedError, setInitializedError] = useState<string | null>(null);

  const initializeApp = async () => {
    try {
      setIsLoading(true);
      await AppService.InitializeApp();
      if (AuthenticationService.IsAuthenticated()) {
        const response = await AccountService.GetFeatureSettings();
        setPermissions({ visibleFields: response });
      }
      setIsInitialized(true);
      setInitializedError(null);
    } catch (e) {
      const error = e as ApiErrorResponse;
      setInitializedError(error.message);
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    initializeApp();
  }, []);

  useEffect(() => {
    if (isAuthenticated) {
      setIsInitialized(false);
      initializeApp();
    }
  }, [isAuthenticated]);

  return (
    <AppContext.Provider
      value={{
        isAuthenticated: isAuthenticated,
        setIsAuthenticated: setIsAuthenticated,
        headerTitle: headerTitle,
        setHeaderTitle: setHeaderTitle,
        headerButtons: headerButtons,
        setHeaderButtons: setHeaderButtons,
        hideBodyPadding: hideBodyPadding,
        setHideBodyPadding: setHideBodyPadding,
        permissions: permissions,
        setPermissions: setPermissions,
        isInitialized: isInitialized,
        isLoading: isLoading,
        initializedError: initializedError,
      }}
    >
      {props.children}
    </AppContext.Provider>
  );
};
