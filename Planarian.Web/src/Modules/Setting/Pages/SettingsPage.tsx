import React, { useContext, useEffect, useState } from "react";
import { Form, Row, Typography } from "antd";
import { UserVm } from "../../User/Models/UserVm";
import { UserService } from "../../User/UserService";
import UserUpdateComponent from "../../User/Componenets/UserUpdateComponent";
import { AppContext } from "../../../Configuration/Context/AppContext";

const { Title } = Typography;

const ProfilePage: React.FC = () => {
  const [user, setUser] = useState<UserVm>();
  const [isLoading, setIsLoading] = useState(false);

  const { setHeaderTitle, setHeaderButtons } = useContext(AppContext);

  useEffect(() => {
    setHeaderButtons([]);
    setHeaderTitle(["Settings"]);
  }, []);

  useEffect(() => {
    async function fetchUser() {
      setIsLoading(true);
      try {
        const response = await UserService.GetCurrentUser();
        setUser(response);
      } catch (error) {
        console.error(error);
      }
      setIsLoading(false);
    }

    fetchUser();
  }, []);

  return (
    <>
      <Row align="middle"></Row>
      <UserUpdateComponent />
    </>
  );
};

export { ProfilePage };
