import { useContext, useEffect } from "react";
import { Navigate, Outlet, useLocation } from "react-router-dom";
import { AppContext } from "../../../Configuration/Context/AppContext";
import { AuthenticationService } from "../Services/AuthenticationService";
import { PermissionKey } from "../Models/PermissionKey";
import { AppService } from "../../../Shared/Services/AppService";

const ProtectedRoutesComponent = ({
  permissionKey,
}: {
  permissionKey?: PermissionKey;
}) => {
  const location = useLocation();

  const { setIsAuthenticated } = useContext(AppContext);

  const redirectUrl = encodeURIComponent(location.pathname);

  let url = `login?redirectUrl=${redirectUrl}`;

  const isAuthenticated = AuthenticationService.IsAuthenticated();
  let hasPermission = true;

  if (permissionKey && isAuthenticated) {
    hasPermission = AppService.HasPermission(permissionKey);

    if (!hasPermission) {
      url = "unauthorized";
    }
  }

  useEffect(() => {
    setIsAuthenticated(isAuthenticated);
  }, []);

  if (isAuthenticated && hasPermission) {
    return <Outlet />;
  } else {
    return <Navigate to={url} />;
  }
};
export { ProtectedRoutesComponent };
