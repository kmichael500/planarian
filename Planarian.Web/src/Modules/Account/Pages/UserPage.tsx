import React, { useContext, useEffect } from "react";
import { AppContext } from "../../../Configuration/Context/AppContext";
import { PermissionKey } from "../../Authentication/Models/PermissionKey";
import { useParams } from "react-router-dom";
import { NotFoundError } from "../../../Shared/Exceptions/PlanarianErrors";
import { isNullOrWhiteSpace } from "../../../Shared/Helpers/StringHelpers";
import { AccountUserManagerService } from "../Services/UserManagerService";
import { message, Space } from "antd";
import { ApiErrorResponse } from "../../../Shared/Models/ApiErrorResponse";
import { UserManagerGridVm } from "../Models/UserManagerGridVm";
import { CavePermissionManagementList } from "../Components/CavePermissionManagementList";
import { BackButtonComponent } from "../../../Shared/Components/Buttons/BackButtonComponent";
import { UserPermissionManagementList } from "../Components/UserPermissionManagementList";
import { ShouldDisplay } from "../../../Shared/Permissioning/Components/ShouldDisplay";

const UserPage: React.FC = () => {
  const { setHeaderTitle, setHeaderButtons } = useContext(AppContext);

  const { userId } = useParams();
  if (isNullOrWhiteSpace(userId)) {
    throw new NotFoundError("User ID is invalid.");
  }
  const [user, setUser] = React.useState<UserManagerGridVm>();

  useEffect(() => {
    const fetchUser = async () => {
      try {
        const fetchedUser = await AccountUserManagerService.GetUserById(userId);
        setUser(fetchedUser);
        setHeaderTitle([fetchedUser.fullName]);
      } catch (err) {
        const error = err as ApiErrorResponse;
        message.error(error.message);
      }
    };

    fetchUser();
  }, [userId]);

  useEffect(() => {
    setHeaderButtons([<BackButtonComponent key="back" to={"../"} />]);
  }, []);

  return (
    <>
      <Space direction="vertical" style={{ width: "100%" }}>
        <ShouldDisplay permissionKey={PermissionKey.Admin}>
          <UserPermissionManagementList userId={userId} />
        </ShouldDisplay>
        <CavePermissionManagementList userId={userId} />
      </Space>
    </>
  );
};

export { UserPage };
