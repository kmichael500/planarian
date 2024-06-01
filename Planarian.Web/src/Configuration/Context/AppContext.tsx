import React, { createContext, useState, useEffect } from "react";
import { AuthenticationService } from "../../Modules/Authentication/Services/AuthenticationService";
import { FeatureSettingVm } from "../../Modules/Account/Models/FeatureSettingVm";
import { AccountService } from "../../Modules/Account/Services/AccountService";

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
}

export const AppContext = createContext<AppContextProps>({
  isAuthenticated: AuthenticationService.IsAuthenticated() as boolean,
  setIsAuthenticated: () => {},
  setHeaderTitle: () => {},
  headerTitle: ["", ""],
  headerButtons: [],
  setHeaderButtons: () => {},
  hideBodyPadding: false,
  setHideBodyPadding: () => {},
  permissions: { visibleFields: [] },
  setPermissions: () => {},
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

  useEffect(() => {
    const init = async () => {
      const response = await AccountService.GetFeatureSettings();
      setPermissions({ visibleFields: response });
      setIsInitialized(true);
    };

    init();
  }, []);

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
      }}
    >
      {isInitialized ? props.children : null}
    </AppContext.Provider>
  );
};
