import React, { useEffect, useState } from "react";
import { Button, Col, Divider, Form, Row, Typography } from "antd";
import { Link } from "react-router-dom";
import { UserVm } from "../../User/Models/UserVm";
import { UserService } from "../../User/UserService";
import UserUpdateComponent from "../../User/Componenets/UserUpdateComponent";

const { Title } = Typography;

const SettingsPage: React.FC = () => {
  const [form] = Form.useForm();
  const [user, setUser] = useState<UserVm>();
  const [isLoading, setIsLoading] = useState(false);

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

  const onFinish = async (values: any) => {
    setIsLoading(true);
    try {
      await UserService.UpdateCurrentUser(values);
    } catch (error) {
      console.error(error);
    }
    setIsLoading(false);
  };

  return (
    <>
      <Row align="middle" gutter={10}>
        <Col>
          <Title level={2}>Preferences</Title>
        </Col>
        {/* take up rest of space to push others to right and left side */}
        <Col flex="auto"></Col>
        <Col>
          <Link to={"./.."}>
            <Button>Back</Button>
          </Link>
        </Col>
        <Col></Col>
      </Row>
      <Divider />
      <Row align="middle"></Row>
      <UserUpdateComponent />
    </>
  );
};

export { SettingsPage };
