import React, { useContext, useEffect } from "react";
import { AppContext } from "../../../Configuration/Context/AppContext";
import { AccountSettingsComponent } from "../Components/AccountSettingsComponent";

const AccountSettingsPage: React.FC = () => {
  const { setHeaderTitle, setHeaderButtons } = useContext(AppContext);

  useEffect(() => {
    setHeaderButtons([]);
    setHeaderTitle(["Account Settings"]);
  }, []);

  return (
    <>
      <AccountSettingsComponent />
    </>
  );
};

export { AccountSettingsPage };
