import { LoginOutlined, MailOutlined, ReloadOutlined } from "@ant-design/icons";
import { Alert, Card, Form, Input, message, Space, Typography } from "antd";
import React, { useContext, useEffect, useRef, useState } from "react";
import { useLocation, useNavigate } from "react-router-dom";
import { AppContext } from "../../../Configuration/Context/AppContext";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
import {
  ApiErrorResponse,
  ApiExceptionType,
} from "../../../Shared/Models/ApiErrorResponse";
import { MessageDeliveryStatus } from "../../../Shared/Models/MessageDeliveryStatus";
import { UserService } from "../../User/UserService";

const { Paragraph, Text, Title } = Typography;

interface EmailConfirmationPendingLocationState {
  emailAddress?: string;
  confirmationEmailJustSent?: boolean;
  confirmationEmailDeliveryStatus?: MessageDeliveryStatus;
}

interface ResendFormValues {
  emailAddress: string;
}

const EmailConfirmationPendingPage: React.FC = () => {
  const { setHeaderButtons, setHeaderTitle } = useContext(AppContext);
  const location = useLocation();
  const navigate = useNavigate();
  const [form] = Form.useForm<ResendFormValues>();
  const [isResending, setIsResending] = useState(false);
  const isResendingRef = useRef(false);
  const [pendingContext] = useState(() => {
    const state = location.state as EmailConfirmationPendingLocationState | null;
    return {
      emailAddress: state?.emailAddress?.trim() || undefined,
      confirmationEmailJustSent: state?.confirmationEmailJustSent === true,
      confirmationEmailDeliveryStatus:
        state?.confirmationEmailDeliveryStatus,
    };
  });
  const { emailAddress, confirmationEmailJustSent } = pendingContext;
  const [confirmationEmailDeliveryStatus, setConfirmationEmailDeliveryStatus] =
    useState(pendingContext.confirmationEmailDeliveryStatus);

  useEffect(() => {
    setHeaderTitle(["Confirm Email"]);
    setHeaderButtons([]);
  }, [setHeaderButtons, setHeaderTitle]);

  useEffect(() => {
    if (location.state == null) return;

    navigate(`${location.pathname}${location.search}${location.hash}`, {
      replace: true,
      state: null,
    });
  }, [location.hash, location.pathname, location.search, location.state, navigate]);

  const resend = async (address: string) => {
    if (isResendingRef.current) return;

    isResendingRef.current = true;
    setIsResending(true);
    try {
      await UserService.ResendEmailConfirmation(address.trim());
      setConfirmationEmailDeliveryStatus(undefined);
      message.success(
        "We've processed your request. If an unconfirmed account exists for that email address, check your inbox and spam or junk folder shortly."
      );
    } catch (e) {
      const error = e as ApiErrorResponse;
      if (error.errorCode !== ApiExceptionType.TooManyRequests) {
        message.error(
          error.message ??
            "Unable to resend the confirmation email. Please try again."
        );
      }
    } finally {
      isResendingRef.current = false;
      setIsResending(false);
    }
  };

  const handleResend = () => {
    if (emailAddress) {
      void resend(emailAddress);
      return;
    }

    form.submit();
  };

  return (
    <Card>
      <Space direction="vertical" size="middle" style={{ width: "100%" }}>
        <MailOutlined style={{ fontSize: 36 }} />
        <Title level={2} style={{ marginBottom: 0 }}>
          Confirm your email
        </Title>
        {emailAddress &&
        confirmationEmailDeliveryStatus === MessageDeliveryStatus.SendFailed ? (
          <Paragraph>
            Your account was created, but Planarian could not submit a confirmation
            email to <Text strong>{emailAddress}</Text>. Request another confirmation
            email below.
          </Paragraph>
        ) : emailAddress &&
          confirmationEmailDeliveryStatus ===
            MessageDeliveryStatus.PermanentFailed ? (
          <Paragraph>
            Your email address still needs to be confirmed, and the most recent
            confirmation message to <Text strong>{emailAddress}</Text> could not be
            delivered.
          </Paragraph>
        ) : emailAddress && confirmationEmailJustSent ? (
          <Paragraph>
            We sent a confirmation link to <Text strong>{emailAddress}</Text>.
            Open the link in that email to verify your email address before signing in.
          </Paragraph>
        ) : emailAddress ? (
          <Paragraph>
            Your email address still needs to be confirmed before you can sign in.
            Check <Text strong>{emailAddress}</Text> for your confirmation email, or
            request another one below.
          </Paragraph>
        ) : (
          <Paragraph>
            Confirm your email address before signing in. Use the confirmation link
            from your email, or enter your email address below to request another one.
          </Paragraph>
        )}
        {confirmationEmailDeliveryStatus === MessageDeliveryStatus.SendFailed &&
          emailAddress && (
            <Alert
              type="error"
              showIcon
              message="We couldn't send your confirmation email."
              description="The email provider did not accept the latest submission. Request another confirmation email below."
            />
          )}
        {confirmationEmailDeliveryStatus ===
          MessageDeliveryStatus.TemporaryFailed &&
          emailAddress && (
            <Alert
              type="warning"
              showIcon
              message="Your confirmation email is temporarily delayed."
              description="The email provider reported a temporary delivery problem and may retry. You can wait or request another confirmation email below."
            />
          )}
        {confirmationEmailDeliveryStatus ===
          MessageDeliveryStatus.PermanentFailed &&
          emailAddress && (
            <Alert
              type="error"
              showIcon
              message="We couldn't deliver your confirmation email."
              description={
                <>
                  The email provider reported that the confirmation message to{" "}
                  <Text strong>{emailAddress}</Text> could not be delivered. Verify
                  that the address is correct. If it is correct and delivery keeps
                  failing, contact Planarian support.
                </>
              }
            />
          )}
        {confirmationEmailDeliveryStatus === MessageDeliveryStatus.Delivered &&
          emailAddress && (
            <Alert
              type="success"
              showIcon
              message="Your email provider accepted the confirmation email."
              description="The recipient mail server accepted the message. If you do not see it, check your spam or junk folder."
            />
          )}
        <Paragraph type="secondary">
          If you do not see the message, check your spam or junk folder. You can
          request another confirmation email below.
        </Paragraph>

        {!emailAddress && (
          <Form
            form={form}
            layout="vertical"
            onFinish={({ emailAddress: submittedEmail }) => {
              void resend(submittedEmail);
            }}
          >
            <Form.Item
              label="Email Address"
              name="emailAddress"
              rules={[
                {
                  required: true,
                  type: "email",
                  message: "Please enter a valid email address.",
                },
              ]}
            >
              <Input type="email" autoComplete="email" />
            </Form.Item>
          </Form>
        )}

        <Space wrap>
          <PlanarianButton
            type="primary"
            icon={<ReloadOutlined />}
            loading={isResending}
            disabled={isResending}
            onClick={handleResend}
            alwaysShowChildren
          >
            Resend confirmation email
          </PlanarianButton>
          <PlanarianButton
            icon={<LoginOutlined />}
            onClick={() =>
              navigate({ pathname: "/login", search: location.search })
            }
            alwaysShowChildren
          >
            Back to Login
          </PlanarianButton>
        </Space>
      </Space>
    </Card>
  );
};

export { EmailConfirmationPendingPage };
