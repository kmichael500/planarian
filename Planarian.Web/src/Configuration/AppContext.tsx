import React, { createContext, useState } from "react";
import { AuthenticationService } from "../Modules/Authentication/Services/AuthenticationService";

interface AppContextProps {
  isAuthenticated: boolean;
  setIsAuthenticated: (isAuthenticated: boolean) => void;
}

export const AppContext = createContext<AppContextProps>({
  isAuthenticated: AuthenticationService.IsAuthenticated(),
  setIsAuthenticated: () => {},
});

interface AppProviderProps {
  children: React.ReactNode;
}
export const AppProvider: React.FC<AppProviderProps> = (props) => {
  const [isAuthenticated, setIsAuthenticated] = useState<boolean>(true);

  return (
    <AppContext.Provider
      value={{
        isAuthenticated: isAuthenticated,
        setIsAuthenticated: setIsAuthenticated,
      }}
    >
      {props.children}
    </AppContext.Provider>
  );
};
