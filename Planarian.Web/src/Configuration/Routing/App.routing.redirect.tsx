import { useContext } from "react";
import { Navigate } from "react-router-dom";
import { isNullOrWhiteSpace } from "../../Shared/Helpers/StringHelpers";
import { AppContext } from "../Context/AppContext";

const AppRederect = () => {
  const { currentAccountId } = useContext(AppContext);
  if (!isNullOrWhiteSpace(currentAccountId)) {
    return <Navigate replace to="/caves" />;
  } else {
    return <Navigate replace to="/user/invitations" />;
  }
};

export { AppRederect };
