import {
  CheckOutlined,
  CloseOutlined,
  PlusOutlined,
  UploadOutlined,
} from "@ant-design/icons";
import { Input, Modal, Typography } from "antd";
import React, { useEffect, useMemo, useRef, useState } from "react";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
import {
  ApiErrorResponse,
  ApiExceptionType,
} from "../../../Shared/Models/ApiErrorResponse";
import { MessageDeliveryStatus } from "../../../Shared/Models/MessageDeliveryStatus";
import { RequestRuntimeState } from "../../../Shared/Http/RequestRuntimeState";
import { PermissionKey } from "../../Authentication/Models/PermissionKey";
import { UserManagerGridVm } from "../Models/UserManagerGridVm";
import { AccountUserManagerService } from "../Services/UserManagerService";
import {
  InvitationRow,
  MAX_BULK_INVITATIONS,
  parseInvitationRows,
  previewInvitations,
} from "./invitationRows";
import { runAccountBatch } from "./runAccountBatch";
import "./InvitePeopleModal.scss";

type Phase = "edit" | "review" | "sending" | "results";
type Outcome = {
  row: InvitationRow;
  description: string;
  userId?: string;
  failed?: boolean;
};

interface Props {
  open: boolean;
  existingUsers: UserManagerGridVm[];
  onClose: () => void;
  onChanged: () => Promise<void>;
  onManagePermissions: (userId: string) => void;
}

const emptyRow = (id: number): InvitationRow => ({
  id,
  firstName: "",
  lastName: "",
  emailAddress: "",
});

export const InvitePeopleModal: React.FC<Props> = ({
  open,
  existingUsers,
  onClose,
  onChanged,
  onManagePermissions,
}) => {
  const [rows, setRows] = useState<InvitationRow[]>([emptyRow(1)]);
  const [phase, setPhase] = useState<Phase>("edit");
  const [showErrors, setShowErrors] = useState(false);
  const [pasteOpen, setPasteOpen] = useState(false);
  const [pasteValue, setPasteValue] = useState("");
  const [notice, setNotice] = useState("");
  const [outcomes, setOutcomes] = useState<Outcome[]>([]);
  const [processed, setProcessed] = useState(0);
  const [retryingId, setRetryingId] = useState<number | null>(null);
  const nextId = useRef(2);
  const openedAccountId = useRef<string | null>(null);
  const fileRef = useRef<HTMLInputElement>(null);
  const preview = useMemo(
    () => previewInvitations(rows, existingUsers),
    [rows, existingUsers]
  );
  const ready = preview.filter((item) => item.disposition === "ready");
  const skipped = preview.filter((item) => item.disposition === "skipped");
  const invalid = preview.filter((item) => item.disposition === "invalid");

  useEffect(() => {
    if (!open) return;
    openedAccountId.current = RequestRuntimeState.getCurrentAccountId();
    nextId.current = 2;
    setRows([emptyRow(1)]);
    setPhase("edit");
    setShowErrors(false);
    setPasteOpen(false);
    setPasteValue("");
    setNotice("");
    setOutcomes([]);
    setProcessed(0);
    setRetryingId(null);
  }, [open]);

  const dismiss = () => {
    if (phase !== "sending") onClose();
  };

  const changeRow = (
    id: number,
    field: "firstName" | "lastName" | "emailAddress",
    value: string
  ) => {
    setRows((current) =>
      current.map((row) => (row.id === id ? { ...row, [field]: value } : row))
    );
  };

  const appendRows = (
    values: { firstName: string; lastName: string; emailAddress: string }[]
  ) => {
    const hasSingleEmpty =
      rows.length === 1 &&
      !rows[0].firstName.trim() &&
      !rows[0].lastName.trim() &&
      !rows[0].emailAddress.trim();
    const currentCount = hasSingleEmpty ? 0 : rows.length;
    if (currentCount + values.length > MAX_BULK_INVITATIONS) {
      setNotice(`A batch can contain at most ${MAX_BULK_INVITATIONS} people.`);
      return false;
    }
    const created = values.map((row) => ({ ...row, id: nextId.current++ }));
    setRows((current) => (hasSingleEmpty ? created : [...current, ...created]));
    setNotice("");
    setShowErrors(false);
    return true;
  };

  const importText = (source: string) => {
    try {
      const parsed = parseInvitationRows(source);
      if (appendRows(parsed)) {
        setPasteOpen(false);
        setPasteValue("");
      }
    } catch (error) {
      setNotice(
        error instanceof Error
          ? error.message
          : "Could not read invitation rows."
      );
    }
  };

  const importFile = async (file?: File) => {
    if (!file) return;
    if (file.size > 256_000) {
      setNotice("Use a CSV file smaller than 256 KB.");
      return;
    }
    try {
      importText(await file.text());
    } catch {
      setNotice("Could not read the CSV file.");
    }
  };

  const review = () => {
    setShowErrors(true);
    if (invalid.length > 0) {
      setNotice("Correct the highlighted entries before reviewing.");
      return;
    }
    setNotice("");
    setPhase("review");
  };

  const outcomeForInvitation = (
    row: InvitationRow,
    result: {
      value?: Awaited<ReturnType<typeof AccountUserManagerService.InviteUser>>;
      error?: unknown;
    }
  ): Outcome => {
    if (result.error) {
      const error = result.error as Partial<ApiErrorResponse>;
      const alreadyMember =
        error.errorCode === ApiExceptionType.UserAlreadyInAccount;
      const revoked =
        error.statusCode === 409 &&
        error.message?.toLowerCase().includes("restore access");
      return {
        row,
        description: alreadyMember
          ? "Already a member."
          : revoked
          ? "Access revoked — use Restore access."
          : error.message ||
            "Invitation failed. Check the account before retrying.",
        failed: !alreadyMember && !revoked,
      };
    }
    if (result.value?.alreadyInvited) {
      return {
        row,
        userId: result.value.userId,
        description: "Already invited — use Resend.",
      };
    }
    if (
      result.value?.invitationEmailDeliveryStatus ===
      MessageDeliveryStatus.SendFailed
    ) {
      return {
        row,
        userId: result.value.userId,
        description: "Invitation created, but email failed. Use Resend.",
        failed: true,
      };
    }
    return {
      row,
      userId: result.value?.userId,
      description: "Invitation submitted.",
    };
  };

  const retry = async (outcome: Outcome) => {
    const accountId = openedAccountId.current;
    if (!accountId || RequestRuntimeState.getCurrentAccountId() !== accountId) {
      setNotice(
        "The selected account changed. Reopen the invitation dialog before retrying."
      );
      return;
    }
    setNotice("");
    setRetryingId(outcome.row.id);
    let next: Outcome;
    try {
      if (outcome.userId) {
        // Retry delivery, not invitation creation.
        await AccountUserManagerService.ResendInvitation(
          outcome.userId,
          accountId
        );
        next = {
          ...outcome,
          failed: false,
          description: "Invitation resubmitted.",
        };
      } else {
        const value = await AccountUserManagerService.InviteUser(
          {
            firstName: outcome.row.firstName.trim(),
            lastName: outcome.row.lastName.trim(),
            emailAddress: outcome.row.emailAddress.trim(),
          },
          accountId
        );
        next = outcomeForInvitation(outcome.row, { value });
      }
    } catch (error) {
      const apiError = error as Partial<ApiErrorResponse>;
      next = {
        ...outcome,
        failed: true,
        description: apiError.message || "Retry failed.",
      };
    }
    setOutcomes((current) =>
      current.map((entry) => (entry.row.id === outcome.row.id ? next : entry))
    );
    setRetryingId(null);
    try {
      await onChanged();
    } catch {
      setNotice(
        "Could not refresh the user list. The individual results above are retained."
      );
    }
  };

  const submit = async () => {
    const accountId = openedAccountId.current;
    if (!accountId || RequestRuntimeState.getCurrentAccountId() !== accountId) {
      setNotice(
        "The selected account changed. Close this dialog and reopen it before sending."
      );
      return;
    }
    setNotice("");
    setOutcomes(
      skipped.map(({ row, reason }) => ({ row, description: reason }))
    );
    setProcessed(0);
    setPhase("sending");
    const completedIds = new Set<number>();

    await runAccountBatch(
      ready.map(({ row }) => row),
      (row) =>
        AccountUserManagerService.InviteUser(
          {
            firstName: row.firstName.trim(),
            lastName: row.lastName.trim(),
            emailAddress: row.emailAddress.trim(),
          },
          accountId
        ),
      () => RequestRuntimeState.getCurrentAccountId() === accountId,
      (row, result) => {
        completedIds.add(row.id);
        const outcome = outcomeForInvitation(row, result);
        setOutcomes((current) => [...current, outcome]);
        setProcessed((current) => current + 1);
      }
    );

    const unprocessed = ready.filter(({ row }) => !completedIds.has(row.id));
    if (unprocessed.length) {
      setOutcomes((current) => [
        ...current,
        ...unprocessed.map(({ row }) => ({
          row,
          description: "Not sent — the selected account changed.",
        })),
      ]);
    }
    setPhase("results");
    try {
      await onChanged();
    } catch {
      setNotice(
        "Could not refresh the user list. The individual results above are retained."
      );
    }
  };

  const title =
    phase === "results"
      ? "Invitation results"
      : phase === "review"
      ? "Review invitations"
      : "Invite people";
  const displayed: Outcome[] =
    phase === "results"
      ? outcomes
      : preview.map((item) => ({ row: item.row, description: item.reason }));

  return (
    <Modal
      open={open}
      title={title}
      onCancel={dismiss}
      maskClosable={false}
      closable={phase !== "sending"}
      footer={null}
      width={800}
      destroyOnClose
      className="invite-people-modal"
    >
      {phase === "edit" && (
        <>
          <Typography.Paragraph type="secondary">
            Enter first name, last name, and email for each person. You can also
            paste three spreadsheet columns or import a CSV.
          </Typography.Paragraph>
          <div className="invite-people-modal__rows">
            {rows.map((row, index) => {
              const item = preview[index];
              return (
                <div key={row.id} className="invite-people-modal__row">
                  <span className="invite-people-modal__number">
                    {index + 1}
                  </span>
                  <Input
                    aria-label={`First name for person ${index + 1}`}
                    placeholder="First name"
                    value={row.firstName}
                    onChange={(event) =>
                      changeRow(row.id, "firstName", event.target.value)
                    }
                  />
                  <Input
                    aria-label={`Last name for person ${index + 1}`}
                    placeholder="Last name"
                    value={row.lastName}
                    onChange={(event) =>
                      changeRow(row.id, "lastName", event.target.value)
                    }
                  />
                  <Input
                    aria-label={`Email for person ${index + 1}`}
                    type="email"
                    placeholder="Email address"
                    value={row.emailAddress}
                    onChange={(event) =>
                      changeRow(row.id, "emailAddress", event.target.value)
                    }
                  />
                  <PlanarianButton
                    icon={<CloseOutlined />}
                    aria-label={`Remove person ${index + 1}`}
                    type="text"
                    disabled={rows.length === 1}
                    onClick={() =>
                      setRows((current) =>
                        current.filter((entry) => entry.id !== row.id)
                      )
                    }
                  />
                  {item.disposition !== "ready" &&
                    (showErrors || item.disposition === "skipped") && (
                      <div
                        className="invite-people-modal__row-message"
                        role="status"
                      >
                        {item.reason}
                      </div>
                    )}
                </div>
              );
            })}
          </div>
          <div className="invite-people-modal__tools">
            <PlanarianButton
              alwaysShowChildren
              icon={<PlusOutlined />}
              disabled={rows.length >= MAX_BULK_INVITATIONS}
              onClick={() => appendRows([emptyRow(0)])}
            >
              Add person
            </PlanarianButton>
            <PlanarianButton
              alwaysShowChildren
              icon={<UploadOutlined />}
              onClick={() => setPasteOpen((value) => !value)}
              debounceTime={0}
            >
              Paste rows
            </PlanarianButton>
            <input
              aria-label="Import invitations CSV"
              type="file"
              accept=".csv,text/csv"
              ref={fileRef}
              className="invite-people-modal__file"
              onChange={(event) => {
                void importFile(event.target.files?.[0]);
                event.target.value = "";
              }}
            />
            <PlanarianButton
              alwaysShowChildren
              icon={<UploadOutlined />}
              onClick={() => fileRef.current?.click()}
            >
              Import CSV
            </PlanarianButton>
            <span className="invite-people-modal__count">
              {rows.length}/{MAX_BULK_INVITATIONS}
            </span>
          </div>
          {pasteOpen && (
            <div className="invite-people-modal__paste">
              <Input.TextArea
                aria-label="Paste three spreadsheet columns"
                rows={4}
                value={pasteValue}
                onChange={(event) => setPasteValue(event.target.value)}
                placeholder={
                  "First Name\tLast Name\tEmail\nJane\tSmith\tjane@example.com"
                }
              />
              <PlanarianButton
                icon={<CheckOutlined />}
                alwaysShowChildren
                onClick={() => importText(pasteValue)}
              >
                Add pasted rows
              </PlanarianButton>
            </div>
          )}
        </>
      )}

      {(phase === "review" || phase === "sending" || phase === "results") && (
        <div className="invite-people-modal__review">
          <Typography.Paragraph>
            {phase === "results"
              ? `${outcomes.length} of ${preview.length} processed or skipped.`
              : `${ready.length} ready to invite · ${skipped.length} to skip.`}
          </Typography.Paragraph>
          {phase === "sending" && (
            <Typography.Text role="status">
              Processing {processed} of {ready.length} invitations…
            </Typography.Text>
          )}
          <div className="invite-people-modal__review-list">
            {displayed.map((item) => (
              <div
                key={item.row.id}
                className="invite-people-modal__review-item"
              >
                <span>
                  <strong>
                    {item.row.firstName.trim()} {item.row.lastName.trim()}
                  </strong>
                  <small>{item.row.emailAddress.trim()}</small>
                </span>
                <span
                  className={item.failed ? "invite-people-modal__failure" : ""}
                >
                  {item.description}
                  {phase === "results" && item.failed && (
                    <PlanarianButton
                      type="link"
                      size="small"
                      alwaysShowChildren
                      icon={<UploadOutlined />}
                      aria-label={
                        item.userId
                          ? `Resend email to ${item.row.emailAddress}`
                          : `Retry invitation for ${item.row.emailAddress}`
                      }
                      loading={retryingId === item.row.id}
                      disabled={retryingId !== null}
                      onClick={() => void retry(item)}
                    >
                      {item.userId ? "Resend email" : "Retry invitation"}
                    </PlanarianButton>
                  )}
                  {phase === "results" && item.userId && !item.failed && (
                    <PlanarianButton
                      type="link"
                      size="small"
                      alwaysShowChildren
                      icon={<CheckOutlined />}
                      onClick={() => onManagePermissions(item.userId!)}
                    >
                      Manage permissions
                    </PlanarianButton>
                  )}
                </span>
              </div>
            ))}
          </div>
        </div>
      )}
      {notice && (
        <Typography.Text type="danger" role="alert">
          {notice}
        </Typography.Text>
      )}
      <div className="invite-people-modal__footer">
        {phase !== "sending" && (
          <PlanarianButton
            icon={<CloseOutlined />}
            alwaysShowChildren
            onClick={dismiss}
          >
            {phase === "results" ? "Close" : "Cancel"}
          </PlanarianButton>
        )}
        {phase === "edit" && (
          <PlanarianButton
            type="primary"
            icon={<CheckOutlined />}
            alwaysShowChildren
            aria-label={`Review ${rows.length} ${
              rows.length === 1 ? "person" : "people"
            }`}
            onClick={review}
          >
            Review {rows.length} {rows.length === 1 ? "person" : "people"}
          </PlanarianButton>
        )}
        {phase === "review" && (
          <>
            <PlanarianButton
              alwaysShowChildren
              icon={<CloseOutlined />}
              onClick={() => setPhase("edit")}
            >
              Back to editing
            </PlanarianButton>
            <PlanarianButton
              type="primary"
              icon={<CheckOutlined />}
              alwaysShowChildren
              permissionKey={PermissionKey.Admin}
              aria-label={`Send ${ready.length} ${
                ready.length === 1 ? "invitation" : "invitations"
              }`}
              disabled={ready.length === 0}
              onClick={() => void submit()}
            >
              Send {ready.length}{" "}
              {ready.length === 1 ? "invitation" : "invitations"}
            </PlanarianButton>
          </>
        )}
        {phase === "results" && (
          <PlanarianButton
            alwaysShowChildren
            icon={<PlusOutlined />}
            onClick={() => {
              nextId.current = 2;
              setRows([emptyRow(1)]);
              setOutcomes([]);
              setPhase("edit");
              setShowErrors(false);
            }}
          >
            Invite more
          </PlanarianButton>
        )}
      </div>
    </Modal>
  );
};
