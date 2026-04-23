import React, { useContext, useEffect } from "react";
import { Space } from "antd";
import { AppContext } from "../../../Configuration/Context/AppContext";
import { CreateAccountComponent } from "../Components/CreateAccountComponent";

const PlanarianSettingsPage: React.FC = () => {
  const { setHeaderTitle, setHeaderButtons } = useContext(AppContext);

  useEffect(() => {
    setHeaderButtons([]);
    setHeaderTitle(["Planarian Settings"]);
  }, [setHeaderButtons, setHeaderTitle]);

  return (
    <Space direction="vertical" style={{ width: "100%" }}>
      <CreateAccountComponent />
    </Space>
  );
};

export { PlanarianSettingsPage };
