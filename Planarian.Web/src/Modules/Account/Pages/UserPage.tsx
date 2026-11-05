import React, { useContext, useEffect, useState } from "react";
import { Result, Space, Spin } from "antd";
import { useParams } from "react-router-dom";
import { AppContext } from "../../../Configuration/Context/AppContext";
import { BackButtonComponent } from "../../../Shared/Components/Buttons/BackButtonComponent";
import { NotFoundError } from "../../../Shared/Exceptions/PlanarianErrors";
import { isNullOrWhiteSpace } from "../../../Shared/Helpers/StringHelpers";
import {
  ApiErrorResponse,
  ApiExceptionType,
} from "../../../Shared/Models/ApiErrorResponse";
import { PermissionKey } from "../../Authentication/Models/PermissionKey";
import { ShouldDisplay } from "../../../Shared/Permissioning/Components/ShouldDisplay";
import { CavePermissionManagementList } from "../Components/CavePermissionManagementList";
import { UserPermissionManagementList } from "../Components/UserPermissionManagementList";
import { AccountUserManagerService } from "../Services/UserManagerService";

type UserLoadStatus = "loading" | "ready" | "not-found" | "error";

const UserPage: React.FC = () => {
  const { setHeaderTitle, setHeaderButtons, currentAccountId } =
    useContext(AppContext);
  const { userId } = useParams();
  if (isNullOrWhiteSpace(userId)) {
    throw new NotFoundError("User ID is invalid.");
  }

  const [status, setStatus] = useState<UserLoadStatus>("loading");

  useEffect(() => {
    let current = true;
    setStatus("loading");
    setHeaderTitle(["User"]);

    const fetchUser = async () => {
      try {
        const user = await AccountUserManagerService.GetUserById(userId);
        if (!current) return;
        setHeaderTitle([user.fullName]);
        setStatus("ready");
      } catch (error) {
        if (!current) return;
        const response = error as ApiErrorResponse;
        const missing =
          response.statusCode === 404 ||
          response.errorCode === ApiExceptionType.NotFound;
        setStatus(missing ? "not-found" : "error");
      }
    };

    void fetchUser();
    return () => {
      current = false;
    };
  }, [userId, currentAccountId, setHeaderTitle]);

  useEffect(() => {
    setHeaderButtons([<BackButtonComponent key="back" to="../" />]);
  }, [setHeaderButtons]);

  if (status === "loading") {
    return (
      <div role="status" style={{ padding: "48px 0", textAlign: "center" }}>
        <Spin size="large" /> Loading user…
      </div>
    );
  }

  if (status !== "ready") {
    return (
      <Result
        status={status === "not-found" ? "404" : "error"}
        title={
          status === "not-found" ? "User not found" : "Unable to load user"
        }
        subTitle={
          status === "not-found"
            ? "This user is not available in this account."
            : "Please try again."
        }
        extra={<BackButtonComponent to="../" alwaysShowChildren />}
      />
    );
  }

  return (
    <Space direction="vertical" style={{ width: "100%" }}>
      <ShouldDisplay permissionKey={PermissionKey.Admin}>
        <UserPermissionManagementList userId={userId} />
      </ShouldDisplay>
      <CavePermissionManagementList userId={userId} />
    </Space>
  );
};

export { UserPage };
