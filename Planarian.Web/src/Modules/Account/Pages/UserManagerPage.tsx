import React, { useContext, useEffect } from "react";
import { AppContext } from "../../../Configuration/Context/AppContext";
import { UserManagerComponent } from "../Components/UserManagerComponent";

const UserManagerPage: React.FC = () => {
  const { setHeaderTitle, setHeaderButtons } = useContext(AppContext);

  useEffect(() => {
    setHeaderButtons([]);
    setHeaderTitle(["User Manager"]);
  }, []);

  return (
    <>
      <UserManagerComponent />
    </>
  );
};

export { UserManagerPage };
