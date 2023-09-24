import React, { useContext, useEffect, useState } from "react";
import { Form, Row, Typography } from "antd";
import { UserVm } from "../../User/Models/UserVm";
import { UserService } from "../../User/UserService";
import UserUpdateComponent from "../../User/Componenets/UserUpdateComponent";
import { AppContext } from "../../../Configuration/Context/AppContext";
import { AccountSettingsComponent } from "../Components/AccountSettingsComponent";

const { Title } = Typography;

const AccountSettingsPage: React.FC = () => {
  const { setHeaderTitle, setHeaderButtons } = useContext(AppContext);

  useEffect(() => {
    setHeaderButtons([]);
    setHeaderTitle(["Account Settings"]);
  }, []);

  return (
    <>
      <Row align="middle"></Row>
      <AccountSettingsComponent />
    </>
  );
};

export { AccountSettingsPage };
