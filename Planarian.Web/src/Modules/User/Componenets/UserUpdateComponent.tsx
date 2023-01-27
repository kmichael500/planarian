import React, { useState, useEffect } from "react";
import {
  Form,
  Input,
  Button,
  Card,
  Col,
  Divider,
  Row,
  Typography,
  ColProps,
  message,
  InputRef,
} from "antd";
import { UserVm } from "../Models/UserVm";
import {
  formatPhoneNumber,
  nameof,
} from "../../../Shared/Helpers/StringHelpers";
import { UserService } from "../UserService";
import { MaskedInput } from "antd-mask-input";
import { ApiErrorResponse } from "../../../Shared/Models/ApiErrorResponse";
import { PasswordRegex } from "../../../Shared/Constants/RegularExpressionConstants";
import { UpdatePasswordVm } from "../Models/UpdatePasswordVm";

const { Title } = Typography;

const UserUpdateComponent: React.FC = () => {
  const [form] = Form.useForm();
  const [passwordForm] = Form.useForm();
  const [user, setUser] = useState<UserVm | undefined>(undefined);
  const [isLoading, setIsLoading] = useState(false);

  const [isSubmitting, setIsSubmitting] = useState(false);
  const [isChangingPassword, setIsChangingPassword] = useState(false);

  const passwordMessage =
    "Please choose a password that is at least 8 characters long and contains a combination of lowercase letters, uppercase letters, numbers, and special characters or is at least 15 characters long.";

  useEffect(() => {
    async function fetchUser() {
      setIsLoading(true);
      try {
        const response = await UserService.GetCurrentUser();
        response.phoneNumber = formatPhoneNumber(response.phoneNumber);
        setUser(response);
      } catch (error) {
        console.error(error);
      }
      setIsLoading(false);
    }
    fetchUser();
  }, []);

  const onFinish = async (values: UserVm) => {
    setIsSubmitting(true);
    try {
      await UserService.UpdateCurrentUser(values);

      message.success("Updated successfully");
    } catch (error: any) {
      message.error(error.message);
    }
    setIsLoading(false);
    setIsSubmitting(false);
  };

  const onChangePassword = async (values: UpdatePasswordVm) => {
    setIsSubmitting(true);
    try {
      console.log(values);
      await UserService.UpdateCurrentUserPassword(values.password);
      message.success("Updated successfully");
      setIsChangingPassword(false);
      passwordForm.resetFields();
    } catch (error: any) {
      message.error(error.message);
    }
    setIsLoading(false);
    setIsSubmitting(false);
  };

  const twoPerColProps = { xs: 24, sm: 24, md: 12, lg: 12, xl: 12 } as ColProps;

  return (
    <>
      <Card
        loading={isLoading}
        title="Your information"
        extra={[
          <>
            {!isChangingPassword && (
              <Button
                onClick={() => {
                  passwordForm.resetFields();
                  setIsChangingPassword(true);
                }}
              >
                Change Password
              </Button>
            )}
          </>,
          <>
            {isChangingPassword && (
              <Button
                onClick={() => {
                  setIsChangingPassword(false);
                  passwordForm.resetFields();
                }}
              >
                Change Info
              </Button>
            )}
          </>,
        ]}
        actions={[
          <Button
            type="primary"
            loading={isSubmitting}
            onClick={() => {
              if (!isChangingPassword) {
                form.submit();
              } else {
                passwordForm.submit();
              }
            }}
          >
            Save
          </Button>,
        ]}
      >
        {isChangingPassword && (
          <Form
            initialValues={user}
            layout="vertical"
            form={passwordForm}
            onFinish={onChangePassword}
          >
            <Form.Item
              label="Password"
              name={nameof<UpdatePasswordVm>("password")}
              rules={[
                {
                  required: true,
                  pattern: PasswordRegex,
                  message: passwordMessage,
                },
              ]}
            >
              <Input.Password></Input.Password>
            </Form.Item>
            <Form.Item
              label="Confirm Password"
              name={nameof<UpdatePasswordVm>("confirmPassword")}
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
                      getFieldValue(nameof<UpdatePasswordVm>("password")) ===
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

                {
                  required: true,
                  pattern: PasswordRegex,
                  message: passwordMessage,
                },
              ]}
            >
              <Input.Password></Input.Password>
            </Form.Item>
          </Form>
        )}
        {!isChangingPassword && (
          <Form
            initialValues={user}
            layout="vertical"
            form={form}
            onFinish={onFinish}
          >
            <Row gutter={8}>
              <Col {...twoPerColProps}>
                <Form.Item
                  label="First Name"
                  name={nameof<UserVm>("firstName")}
                  rules={[{ required: true }]}
                >
                  <Input />
                </Form.Item>
              </Col>
              <Col {...twoPerColProps}>
                <Form.Item
                  label="Last Name"
                  name={nameof<UserVm>("lastName")}
                  rules={[{ required: true }]}
                >
                  <Input />
                </Form.Item>
              </Col>
              <Col {...twoPerColProps}>
                <Form.Item
                  label="Email Address"
                  name={nameof<UserVm>("emailAddress")}
                  rules={[{ required: true, type: "email" }]}
                >
                  <Input type="email" />
                </Form.Item>
              </Col>
              <Col {...twoPerColProps}>
                <Form.Item
                  label="Phone Number"
                  name={nameof<UserVm>("phoneNumber")}
                  rules={[
                    {
                      required: true,
                      pattern: /\+1\s*\(?\d{3}\)?\s*-?\d{3}-\d{4}/,
                      message: "Please enter a valid phone number",
                    },
                  ]}
                >
                  <MaskedInput
                    name={nameof<UserVm>("phoneNumber")}
                    mask={"+1 (000) 000-0000"}
                  ></MaskedInput>
                </Form.Item>
              </Col>
            </Row>
          </Form>
        )}
      </Card>
    </>
  );
};

export default UserUpdateComponent;

export { UserUpdateComponent };
