import React, { useContext, useEffect } from "react";
import { Row } from "antd";
import UserUpdateComponent from "../../User/Componenets/UserUpdateComponent";
import { AppContext } from "../../../Configuration/Context/AppContext";
const ProfilePage: React.FC = () => {
  const { setHeaderTitle, setHeaderButtons } = useContext(AppContext);

  useEffect(() => {
    setHeaderButtons([]);
    setHeaderTitle(["Settings"]);
  }, []);

  return (
    <>
      <Row align="middle"></Row>
      <UserUpdateComponent />
    </>
  );
};

export { ProfilePage };
