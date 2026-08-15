import React, { useContext, useEffect, useRef, useState } from "react";
import {
  Card,
  Col,
  ColProps,
  Form,
  Input,
  message,
  Row,
  Typography,
} from "antd";
import { CheckCircleOutlined } from "@ant-design/icons";
import { useLocation, useNavigate } from "react-router-dom";
import { MaskedInput } from "antd-mask-input";
import { nameof } from "../../../../Shared/Helpers/StringHelpers";
import { PasswordRegex } from "../../../../Shared/Constants/RegularExpressionConstants";
import { RegisterUserVm } from "../../Models/RegisterUserVm";
import { RegisterService } from "../Services/RegisterService";
import { ApiErrorResponse } from "../../../../Shared/Models/ApiErrorResponse";
import { MessageDeliveryStatus } from "../../../../Shared/Models/MessageDeliveryStatus";
import { AppContext } from "../../../../Configuration/Context/AppContext";
import { SubmitButtonComponent } from "../../../../Shared/Components/Buttons/SubmitButtonComponent";
import { LoginButtonComponent } from "../../../../Shared/Components/Buttons/LoginButtonComponent";
import { UserService } from "../../../User/UserService";
import { PlanarianButton } from "../../../../Shared/Components/Buttons/PlanarianButtton";
import { AcceptInvitationVm } from "../../../User/Models/AcceptInvitationVm";
import { ClientRoutes } from "../../../../Configuration/Routing/ClientRoutes.generated";

const { Text } = Typography;

const InvitationMessageCard: React.FC<{
  invitation?: AcceptInvitationVm;
  isLoading: boolean;
}> = ({ invitation, isLoading }) => {
  // Customize your message here. You can use invitation.accountName if available.
  const invitationMessage =
    invitation && invitation.accountName
      ? `You've been invited to access ${invitation.accountName} data on Planarian. Create an account to accept the invitation.`
      : "You've been invited to access data on Planarian.";

  return (
    <>
      {invitation && (
        <Card loading={isLoading} bordered={false} style={{ marginBottom: 16 }}>
          <Text>{invitationMessage}</Text>
        </Card>
      )}
    </>
  );
};

const RegisterPage: React.FC = () => {
  const [form] = Form.useForm();

  const [invitation, setInvitation] = useState<AcceptInvitationVm | undefined>(
    undefined
  );
  const [isLoading, setIsLoading] = useState(false);

  const navigate = useNavigate();
  const location = useLocation();
  const queryParams = new URLSearchParams(location.search);
  const invitationCode = queryParams.get("invitationCode") || undefined;

  const { setHeaderTitle, setHeaderButtons } = useContext(AppContext);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const isSubmittingRef = useRef(false);

  const passwordMessage =
    "Please choose a password that is at least 8 characters long and contains a combination of lowercase letters, uppercase letters, numbers, and special characters or is at least 15 characters long.";

  useEffect(() => {
    setHeaderTitle(["Register"]);
    setHeaderButtons([]);
  }, [setHeaderTitle, setHeaderButtons]);

  useEffect(() => {
    setInvitation(undefined);
    form.setFieldsValue({
      [nameof<RegisterUserVm>("firstName")]: undefined,
      [nameof<RegisterUserVm>("lastName")]: undefined,
      [nameof<RegisterUserVm>("emailAddress")]: undefined,
    });

    if (!invitationCode) {
      setIsLoading(false);
      return;
    }

    let isCurrent = true;
    setIsLoading(true);

    const fetchInvitationDetails = async () => {
      try {
        const invitationDetails = await UserService.GetInvitation(
          invitationCode
        );
        if (!isCurrent) return;
        setInvitation(invitationDetails);
        form.setFieldsValue({
          [nameof<RegisterUserVm>("firstName")]: invitationDetails.firstName,
          [nameof<RegisterUserVm>("lastName")]: invitationDetails.lastName,
          [nameof<RegisterUserVm>("emailAddress")]: invitationDetails.email,
        });
      } catch (error) {
        if (!isCurrent) return;
        message.error("Invalid or expired invitation code.");
      } finally {
        if (isCurrent) {
          setIsLoading(false);
        }
      }
    };

    void fetchInvitationDetails();

    return () => {
      isCurrent = false;
    };
  }, [invitationCode, form]);

  const currentInvitation =
    invitationCode != null && invitation?.invitationCode === invitationCode
      ? invitation
      : undefined;

  const onFinish = async (values: RegisterUserVm) => {
    if (
      isSubmittingRef.current ||
      (invitationCode != null && (isLoading || currentInvitation == null))
    ) {
      return;
    }

    isSubmittingRef.current = true;
    setIsSubmitting(true);
    try {
      const payload = { ...values, invitationCode };

      const result = await RegisterService.RegisterUser(payload);
      navigate(ClientRoutes.emailConfirmationPending.path, {
        replace: true,
        state: {
          emailAddress: values.emailAddress,
          confirmationEmailJustSent:
            result.confirmationEmailDeliveryStatus ===
            MessageDeliveryStatus.Submitted,
          confirmationEmailDeliveryStatus:
            result.confirmationEmailDeliveryStatus,
        },
      });
    } catch (e) {
      const error = e as ApiErrorResponse;
      message.error(error.message);
    } finally {
      isSubmittingRef.current = false;
      setIsSubmitting(false);
    }
  };

  const twoPerColProps = { xs: 24, sm: 24, md: 12, lg: 12, xl: 12 } as ColProps;

  return (
    <>
      <InvitationMessageCard
        invitation={currentInvitation}
        isLoading={isLoading}
      />
      <Card
        title="Register"
        actions={[
          invitationCode ? (
            <PlanarianButton
              type="primary"
              loading={isSubmitting || isLoading}
              disabled={isLoading || currentInvitation == null}
              onClick={() => form.submit()}
              icon={<CheckCircleOutlined />}
              alwaysShowChildren
            >
              Accept Invitation
            </PlanarianButton>
          ) : (
            <SubmitButtonComponent
              type="primary"
              loading={isSubmitting}
              onClick={() => form.submit()}
              alwaysShowChildren
            />
          ),
          <LoginButtonComponent
            alwaysShowChildren
            invitationCode={invitationCode}
          />,
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
                <Input disabled={isLoading} />
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
                  className="themed-mask-input"
                  name={nameof<RegisterUserVm>("phoneNumber")}
                  mask={"+1 (000) 000-0000"}
                />
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
                <Input.Password type="password" />
              </Form.Item>
            </Col>
            <Col span={24}>
              <Form.Item
                label="Confirm Password"
                name={"confirmPassword"}
                dependencies={[nameof<RegisterUserVm>("password")]}
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
                <Input.Password type="password" />
              </Form.Item>
            </Col>
          </Row>
        </Form>
      </Card>
    </>
  );
};

export { RegisterPage };
