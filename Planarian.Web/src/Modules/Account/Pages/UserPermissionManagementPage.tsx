import React, { useContext, useEffect, useState } from "react";
import { AppContext } from "../../../Configuration/Context/AppContext";
import { useParams } from "react-router-dom";
import { NotFoundError } from "../../../Shared/Exceptions/PlanarianErrors";
import { AccountUserManagerService } from "../Services/UserManagerService";
import { isNullOrWhiteSpace } from "../../../Shared/Helpers/StringHelpers";
import { UserManagerGridVm } from "../Models/UserManagerGridVm";
import { UserPermissionManagement } from "../Components/UserPermissionManagement";
import { PermissionKey } from "../../Authentication/Models/PermissionKey";
import { BackButtonComponent } from "../../../Shared/Components/Buttons/BackButtonComponent";

export const UserPermissionManagementPage: React.FC = () => {
  const { userId, permissionKey } = useParams<{
    userId: string;
    permissionKey: PermissionKey;
  }>();

  if (isNullOrWhiteSpace(userId)) {
    throw new NotFoundError("User ID is invalid.");
  }
  if (isNullOrWhiteSpace(permissionKey)) {
    throw new NotFoundError("Permission key is invalid.");
  }

  const [user, setUser] = useState<UserManagerGridVm | null>(null);

  const { setHeaderTitle, setHeaderButtons } = useContext(AppContext);

  useEffect(() => {
    setHeaderButtons([<BackButtonComponent key="back" to={"../../"} />]);
  }, []);

  useEffect(() => {
    const fetchUser = async () => {
      const user = await AccountUserManagerService.GetUserById(userId);
      setUser(user);
      setHeaderTitle([user.fullName]);
    };
    fetchUser();
  }, [setHeaderButtons, setHeaderTitle, userId]);

  return (
    <div style={{ padding: 16 }}>
      <UserPermissionManagement userId={userId} permissionKey={permissionKey} />
    </div>
  );
};
