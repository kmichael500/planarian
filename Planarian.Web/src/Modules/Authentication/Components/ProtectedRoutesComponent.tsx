import { useContext, useEffect } from "react";
import { Navigate, Outlet, useLocation } from "react-router-dom";
import { AppContext } from "../../../Configuration/Context/AppContext";
import { AuthenticationService } from "../Services/AuthenticationService";

const ProtectedRoutesComponent = () => {
  const location = useLocation();

  const { setIsAuthenticated } = useContext(AppContext);

  const redirectUrl = encodeURIComponent(location.pathname);

  const url = `login?redirectUrl=${redirectUrl}`;

  const isAuthenticated = AuthenticationService.IsAuthenticated();

  useEffect(() => {
    setIsAuthenticated(isAuthenticated);
  }, []);

  if (isAuthenticated) {
    return <Outlet />;
  } else {
    return <Navigate to={url} />;
  }
};
export { ProtectedRoutesComponent };
