import { Navigate, Outlet, useLocation } from "react-router-dom";
import { AuthenticationService } from "../Services/AuthenticationService";

const ProtectedRoutesComponent = () => {
  const location = useLocation();

  const redirectUrl = encodeURIComponent(location.pathname);

  const url = `login?redirectUrl=${redirectUrl}`;

  const isAuthenticated = AuthenticationService.IsAuthenticated();
  if (isAuthenticated) {
    return <Outlet />;
  } else {
    return <Navigate to={url} />;
  }
};
export { ProtectedRoutesComponent };
