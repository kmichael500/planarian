import { CloseOutlined, DeleteOutlined } from "@ant-design/icons";
import { Checkbox, Input, Modal, Typography } from "antd";
import React, { useContext, useEffect, useRef, useState } from "react";
import { AppContext } from "../../../Configuration/Context/AppContext";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
import { RequestRuntimeState } from "../../../Shared/Http/RequestRuntimeState";
import { ApiErrorResponse } from "../../../Shared/Models/ApiErrorResponse";
import { MessageDeliveryStatus } from "../../../Shared/Models/MessageDeliveryStatus";
import { PermissionKey } from "../../Authentication/Models/PermissionKey";
import { UserManagerGridVm } from "../Models/UserManagerGridVm";
import { DisableAccessResultVm } from "../Models/DisableAccessResultVm";
import { AccountUserManagerService } from "../Services/UserManagerService";
import { runAccountBatch } from "./runAccountBatch";
import "./InvitePeopleModal.scss";

interface Props {
  open: boolean;
  selected: UserManagerGridVm[];
  onClose: () => void;
  onFinished: () => Promise<void>;
}

type Outcome = {
  user: UserManagerGridVm;
  message: string;
  failed?: boolean;
  emailFailed?: boolean;
};

const describeResult = (
  user: UserManagerGridVm,
  result?: DisableAccessResultVm
): Outcome => {
  if (user.hasActiveInvitation) return { user, message: "Invitation removed." };
  if (
    result?.notificationEmailDeliveryStatus === MessageDeliveryStatus.SendFailed
  )
    return {
      user,
      message: "Access disabled. Notification email failed.",
      emailFailed: true,
    };
  if (
    result?.notificationEmailDeliveryStatus === MessageDeliveryStatus.Submitted
  )
    return { user, message: "Access disabled. Notification email submitted." };
  return { user, message: "Access disabled." };
};
type Phase = "review" | "processing" | "results";

export const BulkDisableModal: React.FC<Props> = ({
  open,
  selected,
  onClose,
  onFinished,
}) => {
  const { currentAccountName } = useContext(AppContext);
  const defaultMessage =
    `An administrator has disabled your access to ${currentAccountName || "this account"} on Planarian. An administrator can restore your access.`;
  const [phase, setPhase] = useState<Phase>("review");
  const [outcomes, setOutcomes] = useState<Outcome[]>([]);
  const [processed, setProcessed] = useState(0);
  const [retryingId, setRetryingId] = useState<string | null>(null);
  const [error, setError] = useState("");
  const [disableMessage, setDisableMessage] = useState(defaultMessage);
  const [sendEmail, setSendEmail] = useState(true);
  const wasOpen = useRef(false);
  const messageEdited = useRef(false);
  const openedAccountId = useRef<string | null>(null);
  const pending = selected.filter((user) => user.hasActiveInvitation);
  const active = selected.filter((user) => !user.hasActiveInvitation);

  useEffect(() => {
    const newlyOpened = open && !wasOpen.current;
    wasOpen.current = open;
    if (!open) return;

    if (newlyOpened) {
      openedAccountId.current = RequestRuntimeState.getCurrentAccountId();
      setPhase("review");
      setOutcomes([]);
      setProcessed(0);
      setRetryingId(null);
      setError("");
      setSendEmail(true);
      messageEdited.current = false;
    }

    // Refresh the default while untouched, without discarding administrator edits.
    if (!messageEdited.current) setDisableMessage(defaultMessage);
  }, [open, defaultMessage]);

  const displayed: Outcome[] =
    phase === "results"
      ? outcomes
      : selected.map((user) => ({
          user,
          message: user.hasActiveInvitation
            ? "Remove invitation"
            : "Disable access",
        }));

  const dismiss = () => {
    if (phase !== "processing") onClose();
  };

  const retry = async (outcome: Outcome) => {
    const accountId = openedAccountId.current;
    if (!accountId || RequestRuntimeState.getCurrentAccountId() !== accountId) {
      setError(
        "The selected account changed. Reopen the dialog before retrying."
      );
      return;
    }
    setError("");
    setRetryingId(outcome.user.userId);
    let next: Outcome;
    try {
      const result = await AccountUserManagerService.DisableAccess(
        outcome.user.userId,
        accountId,
        outcome.user.hasActiveInvitation || !sendEmail ? undefined : disableMessage.trim(),
        !outcome.user.hasActiveInvitation && sendEmail
      );
      next = describeResult(outcome.user, result);
    } catch (error) {
      const apiError = error as Partial<ApiErrorResponse>;
      next = {
        ...outcome,
        message: apiError.message || "Unable to change access.",
        failed: true,
      };
    }
    setOutcomes((current) =>
      current.map((entry) =>
        entry.user.userId === outcome.user.userId ? next : entry
      )
    );
    setRetryingId(null);
    try {
      await onFinished();
    } catch {
      setError(
        "Could not refresh the user list. The individual results above are retained."
      );
    }
  };

  const disable = async () => {
    if (sendEmail && active.length > 0 && !disableMessage.trim()) {
      setError("Enter a message before disabling access.");
      return;
    }
    const accountId = openedAccountId.current;
    if (!accountId || RequestRuntimeState.getCurrentAccountId() !== accountId) {
      setError(
        "The selected account changed. Close this dialog and reopen it before changing access."
      );
      return;
    }
    setError("");
    setPhase("processing");
    const completedIds = new Set<string>();
    await runAccountBatch(
      selected,
      (user) =>
        AccountUserManagerService.DisableAccess(
          user.userId,
          accountId,
          user.hasActiveInvitation || !sendEmail ? undefined : disableMessage.trim(),
          !user.hasActiveInvitation && sendEmail
        ),
      () => RequestRuntimeState.getCurrentAccountId() === accountId,
      (user, result) => {
        completedIds.add(user.userId);
        const failure = result.error as Partial<ApiErrorResponse> | undefined;
        const outcome = failure
          ? {
              user,
              message: failure.message || "Unable to change access.",
              failed: true,
            }
          : describeResult(user, result.value);
        setOutcomes((current) => [...current, outcome]);
        setProcessed((current) => current + 1);
      }
    );
    const remaining = selected.filter((user) => !completedIds.has(user.userId));
    if (remaining.length) {
      setOutcomes((current) => [
        ...current,
        ...remaining.map((user) => ({
          user,
          message: "Not changed — the selected account changed.",
        })),
      ]);
    }
    setPhase("results");
    try {
      await onFinished();
    } catch {
      setError(
        "Could not refresh the user list. The individual results above are retained."
      );
    }
  };

  return (
    <Modal
      title={
        phase === "results" ? "Access change results" : "Review access changes"
      }
      open={open}
      onCancel={dismiss}
      maskClosable={false}
      closable={phase !== "processing"}
      footer={null}
      className="invite-people-modal"
      width={650}
      destroyOnClose
    >
      {phase !== "results" && (
        <>
          <Typography.Paragraph>
            <strong>{active.length}</strong> active{" "}
            {active.length === 1 ? "member" : "members"} to disable,{" "}
            <strong>{pending.length}</strong> pending{" "}
            {pending.length === 1 ? "invitation" : "invitations"} to remove.
          </Typography.Paragraph>
          <Typography.Paragraph type="secondary">
            Disabled memberships keep their permissions so an administrator can
            restore access. Pending invitations and their unused temporary
            permissions are deleted. Email-delivery history is preserved.
          </Typography.Paragraph>
          {phase === "review" && active.length > 0 && (
            <div className="user-manager-notification-message">
              <Checkbox
                checked={sendEmail}
                onChange={(event) => setSendEmail(event.target.checked)}
              >
                Send notification email to disabled members
              </Checkbox>
              {sendEmail && (
                <>
                  <label htmlFor="disabling-message">Message to users</label>
                  <Input.TextArea
                    id="disabling-message"
                    required
                    value={disableMessage}
                    onChange={(event) => {
                      messageEdited.current = true;
                      setDisableMessage(event.target.value);
                    }}
                    rows={3}
                    maxLength={1000}
                    showCount
                    placeholder="Enter the message users will receive"
                  />
                  <Typography.Text type="secondary">
                    Edit or replace this message. It will be included in each
                    disabled member's email and account notice.
                  </Typography.Text>
                </>
              )}
            </div>
          )}
          {phase === "processing" && (
            <Typography.Text role="status">
              Processing {processed} of {selected.length} people…
            </Typography.Text>
          )}
        </>
      )}

      {phase === "results" && (
        <Typography.Paragraph role="status">
          {
            outcomes.filter(
              (item) => !item.failed && !item.message.startsWith("Not changed")
            ).length
          }{" "}
          succeeded, {outcomes.filter((item) => item.failed).length} failed,{" "}
          {
            outcomes.filter((item) => item.message.startsWith("Not changed"))
              .length
          }{" "}
          not changed.
        </Typography.Paragraph>
      )}

      <div className="invite-people-modal__review-list">
        {displayed.map(({ user, message, failed, emailFailed }) => (
          <div className="invite-people-modal__review-item" key={user.userId}>
            <span>
              <strong>{user.fullName}</strong>
              <small>{user.emailAddress}</small>
            </span>
            <span
              className={
                failed || emailFailed ? "invite-people-modal__failure" : ""
              }
            >
              {message}
              {phase === "results" && failed && (
                <PlanarianButton
                  type="link"
                  size="small"
                  alwaysShowChildren
                  aria-label={`Retry access change for ${user.fullName}`}
                  icon={<DeleteOutlined />}
                  loading={retryingId === user.userId}
                  disabled={retryingId !== null}
                  onClick={() => void retry({ user, message, failed })}
                >
                  Retry
                </PlanarianButton>
              )}
            </span>
          </div>
        ))}
      </div>
      {error && (
        <Typography.Text type="danger" role="alert">
          {error}
        </Typography.Text>
      )}
      <div className="invite-people-modal__footer">
        {phase !== "processing" && (
          <PlanarianButton
            alwaysShowChildren
            icon={<CloseOutlined />}
            onClick={dismiss}
          >
            {phase === "results" ? "Close" : "Cancel"}
          </PlanarianButton>
        )}
        {phase === "review" && (
          <PlanarianButton
            type="primary"
            danger
            alwaysShowChildren
            icon={<DeleteOutlined />}
            permissionKey={PermissionKey.Admin}
            disabled={selected.length === 0 || (sendEmail && active.length > 0 && !disableMessage.trim())}
            aria-label={`Confirm changes for ${selected.length}`}
            onClick={() => void disable()}
          >
            Confirm changes for {selected.length}
          </PlanarianButton>
        )}
      </div>
    </Modal>
  );
};
