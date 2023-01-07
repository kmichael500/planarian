import React, { useState, useEffect } from "react";
import { Form, Input, Button, Card, Col, Divider, Row, Typography } from "antd";
import { Link } from "react-router-dom";
import {
  MemberGridComponent,
  MemberGridType,
} from "../../../Shared/Components/MemberGridComponent";
import { TripCreateButton } from "../../Trip/Components/trip.create.button.component";

const { Title } = Typography;
interface User {
  firstName: string;
  lastName: string;
  email: string;
}

const UserUpdateForm: React.FC = () => {
  const [form] = Form.useForm();
  const [user, setUser] = useState<User>({
    firstName: "",
    lastName: "",
    email: "",
  });
  const [isLoading, setIsLoading] = useState(false);

  useEffect(() => {
    async function fetchUser() {
      setIsLoading(true);
      try {
        const response = await fetch("/api/user");
        const data = await response.json();
        setUser(data);
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
      await fetch("/api/user", {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(values),
      });
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
      <Card title="Your information">
        <Form
          layout="vertical"
          form={form}
          onFinish={onFinish}
          initialValues={user}
        >
          <Form.Item label="Name" name="name" rules={[{ required: true }]}>
            <Input />
          </Form.Item>
          <Form.Item
            label="Email"
            name="email"
            rules={[{ required: true, type: "email" }]}
          >
            <Input />
          </Form.Item>
          <Form.Item>
            <Button type="primary" htmlType="submit" loading={isLoading}>
              Update
            </Button>
          </Form.Item>
        </Form>
      </Card>
    </>
  );
};

export default UserUpdateForm;

export { UserUpdateForm as SettingsComponent };
