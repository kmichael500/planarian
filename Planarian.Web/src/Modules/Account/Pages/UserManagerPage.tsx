import React, { useContext, useEffect } from "react";
import { AppContext } from "../../../Configuration/Context/AppContext";
import { UserManagerComponent } from "../Components/UserManagerComponent";

const UserManagerPage: React.FC = () => {
  const {
    resetContentStyle,
    setFullHeightContentStyle,
    setHeaderTitle,
    setHeaderButtons,
  } = useContext(AppContext);

  useEffect(() => {
    setHeaderButtons([]);
    setHeaderTitle(["User Manager"]);
    setFullHeightContentStyle();

    return () => {
      resetContentStyle();
    };
  }, [
    resetContentStyle,
    setFullHeightContentStyle,
    setHeaderButtons,
    setHeaderTitle,
  ]);

  return (
    <div className="user-manager-page">
      <UserManagerComponent />
    </div>
  );
};

export { UserManagerPage };
