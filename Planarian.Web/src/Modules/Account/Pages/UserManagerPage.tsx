import React, { useContext, useEffect } from "react";
import { Row } from "antd";
import { AppContext } from "../../../Configuration/Context/AppContext";
import { AccountSettingsComponent } from "../Components/AccountSettingsComponent";
import { UserManagerComponent } from "../Components/UserManagerComponent";
import { CavePermissionManagement } from "../Components/CavePermissionManagementProps";
import { PermissionKey } from "../../Authentication/Models/PermissionKey";

const UserManagerPage: React.FC = () => {
  const { setHeaderTitle, setHeaderButtons } = useContext(AppContext);

  useEffect(() => {
    setHeaderButtons([]);
    setHeaderTitle(["User Manager"]);
  }, []);

  return (
    <>
      <CavePermissionManagement
        userId="1mlAiqjdEE"
        permissionKey={PermissionKey.View}
        maxCaveSelectCount={5}
      />
    </>
  );
};

export { UserManagerPage };
