import { CloseOutlined, DeleteOutlined } from "@ant-design/icons";
import { Modal, Typography } from "antd";
import React, { useEffect, useRef, useState } from "react";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
import { RequestRuntimeState } from "../../../Shared/Http/RequestRuntimeState";
import { ApiErrorResponse } from "../../../Shared/Models/ApiErrorResponse";
import { PermissionKey } from "../../Authentication/Models/PermissionKey";
import { UserManagerGridVm } from "../Models/UserManagerGridVm";
import { AccountUserManagerService } from "../Services/UserManagerService";
import { runAccountBatch } from "./runAccountBatch";
import "./InvitePeopleModal.scss";

interface Props {
  open: boolean;
  selected: UserManagerGridVm[];
  onClose: () => void;
  onFinished: () => Promise<void>;
}

type Outcome = { user: UserManagerGridVm; message: string; failed?: boolean };
type Phase = "review" | "processing" | "results";

export const BulkRevokeModal: React.FC<Props> = ({
  open,
  selected,
  onClose,
  onFinished,
}) => {
  const [phase, setPhase] = useState<Phase>("review");
  const [outcomes, setOutcomes] = useState<Outcome[]>([]);
  const [processed, setProcessed] = useState(0);
  const [retryingId, setRetryingId] = useState<string | null>(null);
  const [error, setError] = useState("");
  const openedAccountId = useRef<string | null>(null);
  const pending = selected.filter((user) => user.hasActiveInvitation);
  const active = selected.filter((user) => !user.hasActiveInvitation);

  useEffect(() => {
    if (!open) return;
    openedAccountId.current = RequestRuntimeState.getCurrentAccountId();
    setPhase("review");
    setOutcomes([]);
    setProcessed(0);
    setRetryingId(null);
    setError("");
  }, [open]);

  const displayed: Outcome[] =
    phase === "results"
      ? outcomes
      : selected.map((user) => ({
          user,
          message: user.hasActiveInvitation
            ? "Remove invitation"
            : "Revoke access",
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
      await AccountUserManagerService.RevokeAccess(
        outcome.user.userId,
        accountId
      );
      next = {
        user: outcome.user,
        message: outcome.user.hasActiveInvitation
          ? "Invitation removed."
          : "Access revoked.",
      };
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

  const revoke = async () => {
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
      (user) => AccountUserManagerService.RevokeAccess(user.userId, accountId),
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
          : {
              user,
              message: user.hasActiveInvitation
                ? "Invitation removed."
                : "Access revoked.",
            };
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
            {active.length === 1 ? "member" : "members"} to revoke,{" "}
            <strong>{pending.length}</strong> pending{" "}
            {pending.length === 1 ? "invitation" : "invitations"} to remove.
          </Typography.Paragraph>
          <Typography.Paragraph type="secondary">
            Revoked memberships keep their permissions so an administrator can
            restore access. Pending invitations and their unused temporary
            permissions are deleted. Email-delivery history is preserved.
          </Typography.Paragraph>
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
        {displayed.map(({ user, message, failed }) => (
          <div className="invite-people-modal__review-item" key={user.userId}>
            <span>
              <strong>{user.fullName}</strong>
              <small>{user.emailAddress}</small>
            </span>
            <span className={failed ? "invite-people-modal__failure" : ""}>
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
            disabled={selected.length === 0}
            aria-label={`Confirm changes for ${selected.length}`}
            onClick={() => void revoke()}
          >
            Confirm changes for {selected.length}
          </PlanarianButton>
        )}
      </div>
    </Modal>
  );
};
