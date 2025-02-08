import React, { useContext, useEffect } from "react";
import { Row, Typography } from "antd";
import { AppContext } from "../../../Configuration/Context/AppContext";
import { AccountSettingsComponent } from "../Components/AccountSettingsComponent";

const { Title } = Typography;

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
