import React, { useContext, useEffect, useState } from "react";
import { Card, Form, Input, message } from "antd";
import { UserVm } from "../../User/Models/UserVm";
import {
  isNullOrWhiteSpace,
  nameof,
} from "../../../Shared/Helpers/StringHelpers";
import { PasswordRegex } from "../../../Shared/Constants/RegularExpressionConstants";
import { UpdatePasswordVm } from "../../User/Models/UpdatePasswordVm";
import { ResetPasswordEmailVm } from "../../User/Models/ResetPasswordEmailVm";
import { Link, useLocation, useNavigate } from "react-router-dom";
import { UserService } from "../../User/UserService";
import { ApiErrorResponse } from "../../../Shared/Models/ApiErrorResponse";
import { AppContext } from "../../../Configuration/Context/AppContext";
import { CancelButtonComponent } from "../../../Shared/Components/Buttons/CancelButtonComponent";
import { SaveButtonComponent } from "../../../Shared/Components/Buttons/SaveButtonComponent";
import { SubmitButtonComponent } from "../../../Shared/Components/Buttons/SubmitButtonComponent";

const ResetPasswordPage: React.FC = () => {
  const [passwordForm] = Form.useForm();
  const [emailForm] = Form.useForm();

  const [user, setUser] = useState<UserVm | undefined>(undefined);
  const [isLoading, setIsLoading] = useState(false);

  const { setHeaderTitle, setHeaderButtons } = useContext(AppContext);

  useEffect(() => {
    setHeaderTitle(["Forgot Password"]);

    setHeaderButtons([]);
  }, []);

  const [isSubmitting, setIsSubmitting] = useState(false);
  const [, setIsChangingPassword] = useState(false);
  const location = useLocation();
  const navigate = useNavigate();

  const code = new URLSearchParams(location.search).get("code");
  const isChangingPassword = !isNullOrWhiteSpace(code);

  const passwordMessage =
    "Please choose a password that is at least 8 characters long and contains a combination of lowercase letters, uppercase letters, numbers, and special characters or is at least 15 characters long.";

  const onChangePassword = async (values: UpdatePasswordVm) => {
    setIsSubmitting(true);
    try {
      if (isNullOrWhiteSpace(code) || code === null) {
        throw new Error("Invalid code");
      }
      await UserService.ResetPassword(code, values.password);
      message.success("Updated successfully");
      setIsChangingPassword(false);
      passwordForm.resetFields();
      navigate("/login");
    } catch (error: any) {
      message.error(error.message);
    }
    setIsLoading(false);
    setIsSubmitting(false);
  };

  const onSubmitEmail = async (values: ResetPasswordEmailVm) => {
    setIsSubmitting(true);
    try {
      await UserService.SendPasswordResetEmail(values.email);
      message.success("You should receive an email shortly");
      setIsChangingPassword(false);
      emailForm.resetFields();
      navigate("/login");
    } catch (e) {
      const error = e as ApiErrorResponse;

      message.error(error.message);
    }
    setIsLoading(false);
    setIsSubmitting(false);
  };

  return (
    <>
      {isChangingPassword && (
        <Card
          loading={isLoading}
          title="Reset Password"
          actions={[
            <SaveButtonComponent
              loading={isSubmitting}
              type="primary"
              onClick={(e) => passwordForm.submit()}
            />,
            <Link to={"/"}>
              <CancelButtonComponent />
            </Link>,
          ]}
        >
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
        </Card>
      )}
      {!isChangingPassword && (
        <Card
          loading={isLoading}
          title="Reset Password"
          actions={[
            <SubmitButtonComponent
              loading={isSubmitting}
              type="primary"
              onClick={(e) => emailForm.submit()}
            />,
            <Link to={"/"}>
              <CancelButtonComponent />
            </Link>,
          ]}
        >
          <Form layout="vertical" form={emailForm} onFinish={onSubmitEmail}>
            <Form.Item
              label="Email"
              name={nameof<ResetPasswordEmailVm>("email")}
              rules={[{ required: true, type: "email" }]}
            >
              <Input type="email"></Input>
            </Form.Item>
          </Form>
        </Card>
      )}
    </>
  );
};

export { ResetPasswordPage };
