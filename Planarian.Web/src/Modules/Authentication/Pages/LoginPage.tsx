import { Card, Checkbox, Form, Input, message } from "antd";
import { useContext, useEffect, useState } from "react";
import { Link, useLocation, useNavigate } from "react-router-dom";
import { AppContext } from "../../../Configuration/Context/AppContext";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
import {
  LoginOutlined,
  QuestionCircleOutlined,
  UserAddOutlined,
} from "@ant-design/icons";
import {
  isNullOrWhiteSpace,
  nameof,
} from "../../../Shared/Helpers/StringHelpers";
import {
  ApiErrorResponse,
  ApiExceptionType,
} from "../../../Shared/Models/ApiErrorResponse";
import { MessageDeliveryStatus } from "../../../Shared/Models/MessageDeliveryStatus";

import { BrowserLoginVm } from "../Models/BrowserLoginVm";
import React from "react";

const LoginPage: React.FC = () => {
  const [form] = Form.useForm<BrowserLoginVm>();
  const { login, setHeaderTitle, setHeaderButtons } = useContext(AppContext);
  const [isLoggingIn, setIsLoading] = useState<boolean>(false);

  useEffect(() => {
    setHeaderTitle(["Login"]);
    setHeaderButtons([]);
  }, []);

  const location = useLocation();
  const encodedRedirectUrl = new URLSearchParams(location.search).get(
    "redirectUrl"
  );

  let redirectUrl = "/";
  if (!isNullOrWhiteSpace(encodedRedirectUrl)) {
    // TODO: Validate redirectUrl as a local Planarian path before navigating to prevent open redirects.
    redirectUrl = decodeURIComponent(encodedRedirectUrl as string);
  }

  const navigate = useNavigate();

  const queryParams = new URLSearchParams(location.search);
  const invitationCode = queryParams.get("invitationCode") || undefined;

  const onSubmit = async (values: BrowserLoginVm) => {
    try {
      setIsLoading(true);
      await login(values, invitationCode);

      if (!isNullOrWhiteSpace(invitationCode)) {
        navigate(`/user/invitations/${invitationCode}`);
      } else {
        navigate(redirectUrl);
      }
    } catch (e) {
      const error = e as ApiErrorResponse;
      if (error.errorCode === ApiExceptionType.EmailNotConfirmed) {
        const emailNotConfirmedData = error.data as
          | { confirmationEmailDeliveryStatus?: MessageDeliveryStatus }
          | undefined;
        navigate(
          {
            pathname: "/confirm-email/pending",
            search: location.search,
          },
          {
            state: {
              emailAddress: values.emailAddress,
              confirmationEmailJustSent: false,
              confirmationEmailDeliveryStatus:
                emailNotConfirmedData?.confirmationEmailDeliveryStatus,
            },
          }
        );
      } else {
        message.error(error.message);
      }
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <Card
      title="Login"
      actions={[
        <PlanarianButton
          loading={isLoggingIn}
          type="primary"
          onClick={(e) => form.submit()}
          icon={<LoginOutlined />}
          alwaysShowChildren
        >
          Login
        </PlanarianButton>,

        <Link
          to={
            !isNullOrWhiteSpace(invitationCode)
              ? `../register?invitationCode=${invitationCode}`
              : "../register"
          }
        >
          <PlanarianButton alwaysShowChildren icon={<UserAddOutlined />}>
            Register
          </PlanarianButton>
        </Link>,
        <Link to={"../reset-password"}>
          <PlanarianButton
            tooltip="Forgot Password"
            icon={<QuestionCircleOutlined />}
          >
            Forgot Password?
          </PlanarianButton>
        </Link>,
      ]}
    >
      <Form
        form={form}
        name="basic"
        layout="vertical"
        initialValues={{ remember: true }}
        onFinish={onSubmit}
        onKeyDown={(e) => {
          if (e.key === "Enter") {
            form.submit();
          }
        }}
        autoComplete="off"
      >
        <Form.Item
          label="Email Address"
          name={nameof<BrowserLoginVm>("emailAddress")}
          rules={[
            { required: true, message: "Please enter your email address!" },
          ]}
        >
          <Input />
        </Form.Item>

        <Form.Item
          label="Password"
          name={nameof<BrowserLoginVm>("password")}
          rules={[{ required: true, message: "Please enter your password!" }]}
        >
          <Input.Password />
        </Form.Item>

        <Form.Item name="remember" valuePropName="checked">
          <Checkbox>Remember me</Checkbox>
        </Form.Item>

        <Form.Item></Form.Item>
      </Form>
    </Card>
  );
};

export { LoginPage };
