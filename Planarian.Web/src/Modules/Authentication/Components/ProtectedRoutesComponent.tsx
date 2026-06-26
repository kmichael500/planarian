import { useContext } from "react";
import { Navigate, Outlet, useLocation } from "react-router-dom";
import { AppContext } from "../../../Configuration/Context/AppContext";
import { PermissionKey } from "../Models/PermissionKey";

const ProtectedRoutesComponent = ({
  permissionKey,
}: {
  permissionKey?: PermissionKey;
}) => {
  const location = useLocation();

  const { hasPermission, isAuthenticated } = useContext(AppContext);

  const redirectUrl = encodeURIComponent(location.pathname);

  let url = `login?redirectUrl=${redirectUrl}`;

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
