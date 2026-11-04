import { useContext } from "react";
import { Navigate, Outlet, useLocation } from "react-router-dom";
import { AppContext } from "../../../Configuration/Context/AppContext";
import { ClientRoutes } from "../../../Configuration/Routing/ClientRoutes.generated";
import { PermissionKey } from "../Models/PermissionKey";

const ProtectedRoutesComponent = ({
  permissionKey,
  requiresAccount = false,
}: {
  permissionKey?: PermissionKey;
  requiresAccount?: boolean;
}) => {
  const location = useLocation();

  const { currentAccountId, hasPermission, isAuthenticated } =
    useContext(AppContext);

  const redirectUrl = encodeURIComponent(location.pathname);

  let url = `login?redirectUrl=${redirectUrl}`;

  if (isAuthenticated && requiresAccount && !currentAccountId) {
    return <Navigate replace to={ClientRoutes.invitationList.path} />;
  }

  let isAuthorized = true;

  if (permissionKey && isAuthenticated) {
    isAuthorized = hasPermission(permissionKey);

    if (!isAuthorized) {
      url = "unauthorized";
    }
  }

  if (isAuthenticated && isAuthorized) {
    return <Outlet />;
  } else {
    return <Navigate to={url} />;
  }
};
export { ProtectedRoutesComponent };
