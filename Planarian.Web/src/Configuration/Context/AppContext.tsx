import React, { createContext, useState } from "react";
import { AuthenticationService } from "../../Modules/Authentication/Services/AuthenticationService";

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
      }}
    >
      {props.children}
    </AppContext.Provider>
  );
};
