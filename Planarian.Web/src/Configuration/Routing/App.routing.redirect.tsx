import { Navigate } from "react-router-dom";
import { AuthenticationService } from "../../Modules/Authentication/Services/AuthenticationService";
import { isNullOrWhiteSpace } from "../../Shared/Helpers/StringHelpers";

const AppRederect = () => {
  const accountId = AuthenticationService.GetAccountId();
  if (!isNullOrWhiteSpace(accountId)) {
    return <Navigate replace to="/caves" />;
  } else {
    return <Navigate replace to="/projects" />;
  }
};

export { AppRederect };
