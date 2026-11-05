import { CloseOutlined, DeleteOutlined, RedoOutlined } from "@ant-design/icons";
import { Checkbox, Input, Typography } from "antd";
import React, { useContext, useEffect, useRef, useState } from "react";
import { AppContext } from "../../../Configuration/Context/AppContext";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
import { PlanarianModal } from "../../../Shared/Components/Buttons/PlanarianModal";
import { RequestRuntimeState } from "../../../Shared/Http/RequestRuntimeState";
import { ApiErrorResponse } from "../../../Shared/Models/ApiErrorResponse";
import { MessageDeliveryStatus } from "../../../Shared/Models/MessageDeliveryStatus";
import { PermissionKey } from "../../Authentication/Models/PermissionKey";
import { AccountAccessAction } from "../Models/AccountAccessChangeResultVm";
import { UserManagerGridVm } from "../Models/UserManagerGridVm";
import { AccountUserManagerService } from "../Services/UserManagerService";
import { runAccountBatch } from "./runAccountBatch";
import "./AccountAccessChangeModal.scss";

interface Props {
  open: boolean;
  action: AccountAccessAction;
  selected: UserManagerGridVm[];
  onClose: () => void;
  onFinished: () => Promise<void>;
}

type Phase = "review" | "processing" | "results";
type Outcome = {
  user: UserManagerGridVm;
  message: string;
  failed?: boolean;
  emailFailed?: boolean;
  skipped?: boolean;
};

const outcomeFor = (
  action: AccountAccessAction,
  user: UserManagerGridVm,
  deliveryStatus?: MessageDeliveryStatus
): Outcome => {
  if (user.hasActiveInvitation) {
    return { user, message: "Invitation canceled." };
  }
  const label =
    action === "enable"
      ? "Access restored"
      : action === "disable"
      ? "Access disabled"
      : "Membership revoked";
  if (deliveryStatus === MessageDeliveryStatus.SendFailed) {
    return {
      user,
      message: label + ", but the email couldn't be sent.",
      emailFailed: true,
    };
  }
  return { user, message: label + "." };
};

export const AccountAccessChangeModal: React.FC<Props> = ({
  open,
  action,
  selected,
  onClose,
  onFinished,
}) => {
  const { currentAccountName } = useContext(AppContext);
  const accountName = currentAccountName || "this account";
  const defaultMessage =
    action === "enable"
      ? `Your access to ${accountName} has been restored. If you have any questions, please reach out to an administrator.`
      : `Your access to ${accountName} has been ${
          action === "disable" ? "disabled" : "removed"
        }. If you have any questions or believe this was a mistake, please reach out to an administrator.`;
  const [phase, setPhase] = useState<Phase>("review");
  const [outcomes, setOutcomes] = useState<Outcome[]>([]);
  const [processed, setProcessed] = useState(0);
  const [retryingId, setRetryingId] = useState<string | null>(null);
  const [error, setError] = useState("");
  const [notificationMessage, setNotificationMessage] =
    useState(defaultMessage);
  const [sendEmail, setSendEmail] = useState(true);
  const [confirmation, setConfirmation] = useState("");
  const openedAccountId = useRef<string | null>(null);
  const wasOpen = useRef(false);
  const messageEdited = useRef(false);
  const pending = selected.filter((user) => user.hasActiveInvitation);
  const active = selected.filter((user) => !user.hasActiveInvitation);
  const canNotify = active.length > 0;
  const requiresRevokeConfirmation = action === "revoke";
  const validMessage = !sendEmail || !canNotify || !!notificationMessage.trim();

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
      setConfirmation("");
      messageEdited.current = false;
    }
    if (!messageEdited.current) setNotificationMessage(defaultMessage);
  }, [open, defaultMessage]);

  const title =
    phase === "results"
      ? "Results"
      : phase === "processing"
      ? "Updating memberships"
      : action === "enable"
      ? "Enable access?"
      : action === "revoke"
      ? selected.length === 1
        ? "Revoke membership?"
        : "Revoke memberships?"
      : active.length && pending.length
      ? "Review changes"
      : active.length
      ? "Disable access?"
      : pending.length === 1
      ? "Cancel invitation?"
      : "Cancel invitations?";

  const actionLabel =
    action === "enable"
      ? "Enable access"
      : action === "revoke"
      ? selected.length === 1
        ? "Revoke membership"
        : "Revoke memberships"
      : active.length && pending.length
      ? "Apply changes"
      : active.length
      ? "Disable access"
      : pending.length === 1
      ? "Cancel invitation"
      : "Cancel invitations";

  const summary =
    action === "enable"
      ? selected.length === 1
        ? selected[0].fullName + " will regain access to " + accountName + "."
        : selected.length +
          " members will regain access to " +
          accountName +
          "."
      : action === "revoke"
      ? selected.length === 1
        ? selected[0].fullName +
          " will be completely removed from this account. Their membership, permissions, and favorites will be deleted. They'll need a new invitation to rejoin. Previously contributed cave data will remain."
        : selected.length +
          " members will be completely removed from this account. Their memberships, permissions, and favorites will be deleted. They'll need new invitations to rejoin. Previously contributed cave data will remain."
      : [
          active.length === 1
            ? active[0].fullName +
              " will lose access, but their membership, permissions, and favorites will be preserved. Access can be restored without a new invitation."
            : active.length > 1
            ? active.length +
              " members will lose access, but their memberships, permissions, and favorites will be preserved. Access can be restored without new invitations."
            : "",
          pending.length === 1
            ? pending[0].fullName + "'s invitation will be canceled."
            : pending.length > 1
            ? pending.length + " invitations will be canceled."
            : "",
        ]
          .filter(Boolean)
          .join(" ");

  const perform = (user: UserManagerGridVm, accountId: string) => {
    const notify = sendEmail && !user.hasActiveInvitation;
    const text = notify ? notificationMessage.trim() : undefined;
    return AccountUserManagerService.ChangeAccess(
      user.userId,
      accountId,
      action,
      text,
      notify
    );
  };

  const describe = (
    user: UserManagerGridVm,
    result: Awaited<ReturnType<typeof perform>>
  ): Outcome =>
    outcomeFor(
      action,
      user,
      result.notificationEmailDeliveryStatus ?? undefined
    );

  const accountStillSelected = (accountId: string) =>
    RequestRuntimeState.getCurrentAccountId() === accountId;

  const refresh = async () => {
    try {
      await onFinished();
    } catch {
      setError("Changes saved, but the user list couldn't refresh.");
    }
  };

  const retry = async (outcome: Outcome) => {
    const accountId = openedAccountId.current;
    if (!accountId || !accountStillSelected(accountId)) {
      setError(
        "The selected account changed. Reopen the dialog before retrying."
      );
      return;
    }
    setError("");
    setRetryingId(outcome.user.userId);
    let next: Outcome;
    try {
      next = describe(outcome.user, await perform(outcome.user, accountId));
    } catch (exception) {
      const failure = exception as Partial<ApiErrorResponse>;
      next = {
        ...outcome,
        message: failure.message || "Unable to change membership.",
        failed: true,
      };
    }
    setOutcomes((current) =>
      current.map((item) =>
        item.user.userId === outcome.user.userId ? next : item
      )
    );
    setRetryingId(null);
    await refresh();
  };

  const apply = async () => {
    if (
      !validMessage ||
      (requiresRevokeConfirmation && confirmation !== "REVOKE")
    )
      return;
    const accountId = openedAccountId.current;
    if (!accountId || !accountStillSelected(accountId)) {
      setError("The selected account changed. Close and reopen the dialog.");
      return;
    }
    setError("");
    setPhase("processing");
    const completedIds = new Set<string>();
    await runAccountBatch(
      selected,
      (user) => perform(user, accountId),
      () => accountStillSelected(accountId),
      (user, result) => {
        completedIds.add(user.userId);
        const failure = result.error as Partial<ApiErrorResponse> | undefined;
        const outcome: Outcome = failure
          ? {
              user,
              message: failure.message || "Unable to change membership.",
              failed: true,
            }
          : describe(user, result.value!);
        setOutcomes((current) => [...current, outcome]);
        setProcessed((count) => count + 1);
      }
    );
    const remaining = selected.filter((user) => !completedIds.has(user.userId));
    if (remaining.length) {
      setOutcomes((current) => [
        ...current,
        ...remaining.map((user) => ({
          user,
          message: "Not changed — the selected account changed.",
          skipped: true,
        })),
      ]);
    }
    setPhase("results");
    await refresh();
  };

  const completed = outcomes.filter(
    (item) => !item.failed && !item.skipped
  ).length;
  const failed = outcomes.filter((item) => item.failed).length;
  const skipped = outcomes.filter((item) => item.skipped).length;
  const displayed: Outcome[] =
    phase === "results"
      ? outcomes
      : selected.map((user) => ({
          user,
          message:
            action === "enable"
              ? "Enable access"
              : action === "revoke"
              ? "Revoke membership"
              : user.hasActiveInvitation
              ? "Cancel invitation"
              : "Disable access",
        }));

  return (
    <PlanarianModal
      open={open}
      header={title}
      onClose={() => {
        if (phase !== "processing" && retryingId === null) onClose();
      }}
      dismissible={phase !== "processing" && retryingId === null}
      closeOnBackdrop={false}
      width={720}
      height="auto"
      maxHeight="90dvh"
      className="account-access-modal"
      footer={
        phase === "processing" ? undefined : (
          <div className="account-access-modal__footer">
            <PlanarianButton
              icon={<CloseOutlined />}
              alwaysShowChildren
              onClick={onClose}
              disabled={retryingId !== null}
            >
              {phase === "results" ? "Close" : "Cancel"}
            </PlanarianButton>
            {phase === "review" && (
              <PlanarianButton
                icon={
                  action === "enable" ? <RedoOutlined /> : <DeleteOutlined />
                }
                danger={action !== "enable"}
                type="primary"
                alwaysShowChildren
                permissionKey={PermissionKey.Admin}
                aria-label={actionLabel}
                disabled={
                  selected.length === 0 ||
                  !validMessage ||
                  (requiresRevokeConfirmation && confirmation !== "REVOKE")
                }
                onClick={() => void apply()}
              >
                {actionLabel}
              </PlanarianButton>
            )}
          </div>
        )
      }
    >
      {phase === "review" && (
        <>
          <Typography.Paragraph className="account-access-modal__summary">
            {summary}
          </Typography.Paragraph>
          {canNotify && (
            <div className="account-access-modal__notification">
              <Checkbox
                checked={sendEmail}
                onChange={(event) => setSendEmail(event.target.checked)}
              >
                Notify by email
              </Checkbox>
              {sendEmail && (
                <>
                  <label htmlFor="account-access-message">Message</label>
                  <Input.TextArea
                    id="account-access-message"
                    value={notificationMessage}
                    onChange={(event) => {
                      messageEdited.current = true;
                      setNotificationMessage(event.target.value);
                    }}
                    required
                    maxLength={1000}
                    autoSize={{ minRows: 2, maxRows: 4 }}
                  />
                </>
              )}
            </div>
          )}
          {requiresRevokeConfirmation && (
            <div className="account-access-modal__confirmation">
              <label htmlFor="account-access-confirmation">
                Type <strong>REVOKE</strong> to confirm.
              </label>
              <Input
                id="account-access-confirmation"
                autoComplete="off"
                value={confirmation}
                onChange={(event) => setConfirmation(event.target.value)}
              />
            </div>
          )}
        </>
      )}
      {phase === "processing" && (
        <Typography.Text role="status">
          Updating {processed} of {selected.length} memberships…
        </Typography.Text>
      )}
      {phase === "results" && (
        <Typography.Paragraph role="status">
          {completed} completed
          {failed ? " · " + failed + " failed" : ""}
          {skipped ? " · " + skipped + " unchanged" : ""}
        </Typography.Paragraph>
      )}
      {(phase === "results" || (phase === "review" && selected.length > 1)) && (
        <div
          className="account-access-modal__list"
          aria-label="Affected people"
        >
          {displayed.map(({ user, message, failed, emailFailed }) => (
            <div className="account-access-modal__person" key={user.userId}>
              <span>
                <strong>{user.fullName}</strong>
                <small>{user.emailAddress}</small>
              </span>
              <span
                className={
                  failed || emailFailed ? "account-access-modal__failure" : ""
                }
              >
                {message}
                {phase === "results" && failed && (
                  <PlanarianButton
                    icon={<DeleteOutlined />}
                    type="link"
                    size="small"
                    alwaysShowChildren
                    aria-label={"Retry access change for " + user.fullName}
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
      )}
      {error && (
        <Typography.Text type="danger" role="alert">
          {error}
        </Typography.Text>
      )}
    </PlanarianModal>
  );
};
