import React, { useState, useEffect } from "react";
import { Form, Input, Col, Row, ColProps, message, Card, Button } from "antd";
import { UserVm } from "../../../User/Models/UserVm";
import {
  formatPhoneNumber,
  nameof,
} from "../../../../Shared/Helpers/StringHelpers";
import { UserService } from "../../../User/user.service";
import { MaskedInput } from "antd-mask-input";
import { UpdatePasswordVm } from "../../../User/Models/UpdatePasswordVm";
import { PasswordRegex } from "../../../../Shared/RegEx/RegularExpressions";
import { RegisterUserVm } from "../../Models/RegisterUserVm";
import { ApiErrorResponse } from "../../../../Shared/Models/ApiErrorResponse";
import { RegisterService } from "../Services/register.service";
import { Link, useNavigate } from "react-router-dom";

const RegisterComponent: React.FC = () => {
  const [form] = Form.useForm();

  const [invitation, setUser] = useState<RegisterUserVm | undefined>(undefined);
  const [isLoading, setIsLoading] = useState(false);

  const navigate = useNavigate();

  const [isSubmitting, setIsSubmitting] = useState(false);
  const [isSubmissionSuccsess, setIsSubmissionSuccsess] = useState(false);

  const passwordMessage =
    "Please choose a password that is at least 8 characters long and contains a combination of lowercase letters, uppercase letters, numbers, and special characters or is at least 15 characters long.";

  useEffect(() => {
    async function fetchUser() {
      setIsLoading(true);
      try {
        // const response = await UserService.GetCurrentUser();
        // response.phoneNumber = formatPhoneNumber(response.phoneNumber);
        // setUser(response);
      } catch (error: any) {
        message.error(error.message);
      }
      setIsLoading(false);
    }
    fetchUser();
  }, []);

  const onFinish = async (values: RegisterUserVm) => {
    setIsSubmitting(true);
    try {
      await RegisterService.RegisterUser(values);

      message.success(
        "Thansk! Please check your email to confirm your account!"
      );

      navigate("../login");
    } catch (error: any) {
      message.error(error.message);
    }
    setIsLoading(false);
    setIsSubmitting(false);
  };

  const twoPerColProps = { xs: 24, sm: 24, md: 12, lg: 12, xl: 12 } as ColProps;
  return (
    <Card
      title="Register"
      actions={[
        <Button
          type="primary"
          loading={isSubmitting}
          onClick={() => form.submit()}
        >
          Register
        </Button>,
        <Link to={"../login"}>
          <Button>Login</Button>
        </Link>,
      ]}
    >
      <Form
        initialValues={invitation}
        layout="vertical"
        form={form}
        onFinish={onFinish}
      >
        <Row gutter={8}>
          <Col {...twoPerColProps}>
            <Form.Item
              label="First Name"
              name={nameof<RegisterUserVm>("firstName")}
              rules={[{ required: true }]}
              messageVariables={{ name: "First Name" }}
            >
              <Input />
            </Form.Item>
          </Col>
          <Col {...twoPerColProps}>
            <Form.Item
              label="Last Name"
              name={nameof<RegisterUserVm>("lastName")}
              messageVariables={{ name: "Last Name" }}
              rules={[{ required: true }]}
            >
              <Input />
            </Form.Item>
          </Col>
          <Col {...twoPerColProps}>
            <Form.Item
              label="Email Address"
              name={nameof<RegisterUserVm>("emailAddress")}
              rules={[{ required: true, type: "email" }]}
              messageVariables={{ name: "Email" }}
            >
              <Input type="email" />
            </Form.Item>
          </Col>
          <Col {...twoPerColProps}>
            <Form.Item
              label="Phone Number"
              name={nameof<RegisterUserVm>("phoneNumber")}
              rules={[
                {
                  required: true,
                  pattern: /\+1\s*\(?\d{3}\)?\s*-?\d{3}-\d{4}/,
                  message: "Please enter a valid phone number",
                },
              ]}
            >
              <MaskedInput
                name={nameof<RegisterUserVm>("phoneNumber")}
                mask={"+1 (000) 000-0000"}
              ></MaskedInput>
            </Form.Item>
          </Col>
          <Col span={24}>
            <Form.Item
              label="Password"
              name={nameof<RegisterUserVm>("password")}
              messageVariables={{ name: "Password" }}
              rules={[
                {
                  required: true,
                  pattern: PasswordRegex,
                  message: passwordMessage,
                },
              ]}
            >
              <Input.Password type="password"></Input.Password>
            </Form.Item>
          </Col>
          <Col span={24}>
            <Form.Item
              label="Confirm Password"
              name={"confirmPassword"}
              dependencies={["password"]}
              hasFeedback
              rules={[
                {
                  required: true,
                  message: "Please confirm your password!",
                },
                ({ getFieldValue }) => ({
                  validator(_, value) {
                    if (
                      !value ||
                      getFieldValue(nameof<RegisterUserVm>("password")) ===
                        value
                    ) {
                      return Promise.resolve();
                    }
                    return Promise.reject(
                      new Error(
                        "The two passwords that you entered do not match!"
                      )
                    );
                  },
                }),
              ]}
            >
              <Input.Password type="password"></Input.Password>
            </Form.Item>
          </Col>
        </Row>
      </Form>
    </Card>
  );
};

export { RegisterComponent };