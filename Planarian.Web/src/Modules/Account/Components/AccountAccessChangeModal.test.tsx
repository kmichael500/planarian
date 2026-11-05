import {
  fireEvent,
  render,
  screen,
  waitFor,
  within,
} from "@testing-library/react";
import React, { useContext } from "react";
import { AppContext } from "../../../Configuration/Context/AppContext";
import { RequestRuntimeState } from "../../../Shared/Http/RequestRuntimeState";
import { MessageDeliveryStatus } from "../../../Shared/Models/MessageDeliveryStatus";
import { UserManagerGridVm } from "../Models/UserManagerGridVm";
import { AccountUserManagerService } from "../Services/UserManagerService";
import { AccountAccessAction } from "../Models/AccountAccessChangeResultVm";
import { AccountAccessChangeModal } from "./AccountAccessChangeModal";

const Permissions: React.FC<{
  children: React.ReactNode;
  accountName?: string;
}> = ({ children, accountName = "Test Survey" }) => {
  const value = useContext(AppContext);
  return (
    <AppContext.Provider
      value={{
        ...value,
        currentAccountName: accountName,
        hasPermission: () => true,
      }}
    >
      {children}
    </AppContext.Provider>
  );
};
const member = (id: string, pending: boolean): UserManagerGridVm => ({
  userId: id,
  fullName: pending ? "Pending Person" : "Active Member",
  emailAddress: `${id}@example.test`,
  hasActiveInvitation: pending,
  invitationAcceptedOn: pending ? null : "2026-10-01T10:00:00Z",
  invitationSentOn: null,
  lastActiveOn: null,
  accessDisabledOn: null,
  invitationEmailAttemptCount: 0,
});

beforeAll(() => {
  Object.defineProperty(window, "matchMedia", {
    writable: true,
    value: (query: string) => ({
      matches: false,
      media: query,
      onchange: null,
      addListener: () => undefined,
      removeListener: () => undefined,
      addEventListener: () => undefined,
      removeEventListener: () => undefined,
      dispatchEvent: () => false,
    }),
  });
});

describe("AccountAccessChangeModal", () => {
  let changeAccess: jest.SpyInstance;
  const people = [member("person0001", true), member("person0002", false)];
  const defaultMessage =
    "Your access to Test Survey has been disabled. If you have any questions or believe this was a mistake, please reach out to an administrator.";

  beforeEach(() => {
    RequestRuntimeState.setCurrentAccountId("account0001");
    changeAccess = jest
      .spyOn(AccountUserManagerService, "ChangeAccess")
      .mockResolvedValue({
        notificationEmailDeliveryStatus: MessageDeliveryStatus.Submitted,
      });
  });
  afterEach(() => {
    RequestRuntimeState.setCurrentAccountId(null);
    jest.restoreAllMocks();
  });
  const renderModal = (
    members = people,
    action: AccountAccessAction = "disable"
  ) => {
    const onFinished = jest.fn().mockResolvedValue(undefined);
    render(
      <Permissions>
        <AccountAccessChangeModal
          action={action}
          open
          selected={members}
          onClose={jest.fn()}
          onFinished={onFinished}
        />
      </Permissions>
    );
    return onFinished;
  };

  it("enables access with an editable optional notification and tracks email failure separately", async () => {
    changeAccess.mockResolvedValueOnce({
      notificationEmailDeliveryStatus: MessageDeliveryStatus.SendFailed,
    });
    renderModal(
      [{ ...people[1], accessDisabledOn: "2026-10-30T12:00:00Z" }],
      "enable"
    );
    const dialog = screen.getByRole("dialog", { name: "Enable access?" });
    expect(dialog).toHaveTextContent("Active Member will regain access");
    const message = within(dialog).getByRole("textbox", { name: "Message" });
    expect(message).toHaveValue(
      "Your access to Test Survey has been restored. If you have any questions, please reach out to an administrator."
    );
    fireEvent.change(message, { target: { value: "Welcome back." } });
    fireEvent.click(
      within(dialog).getByRole("button", { name: "Enable access" })
    );
    await waitFor(() =>
      expect(changeAccess).toHaveBeenCalledWith(
        "person0002",
        "account0001",
        "enable",
        "Welcome back.",
        true
      )
    );
    expect(
      await screen.findByText(
        "Access restored, but the email couldn't be sent."
      )
    ).toBeInTheDocument();
    expect(
      changeAccess.mock.calls.filter(
        ([, , requestedAction]) => requestedAction === "disable"
      )
    ).toHaveLength(0);
    expect(
      changeAccess.mock.calls.filter(
        ([, , requestedAction]) => requestedAction === "revoke"
      )
    ).toHaveLength(0);
  });

  it("enables access without sending an email when notification is unchecked", async () => {
    renderModal(
      [{ ...people[1], accessDisabledOn: "2026-10-30T12:00:00Z" }],
      "enable"
    );
    fireEvent.click(screen.getByRole("checkbox", { name: "Notify by email" }));
    expect(
      screen.queryByRole("textbox", { name: "Message" })
    ).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Enable access" }));
    await waitFor(() =>
      expect(changeAccess).toHaveBeenCalledWith(
        "person0002",
        "account0001",
        "enable",
        undefined,
        false
      )
    );
    expect(await screen.findByText("Access restored.")).toBeInTheDocument();
  });

  it("refuses enable when the selected account changes before confirmation", () => {
    renderModal(
      [{ ...people[1], accessDisabledOn: "2026-10-30T12:00:00Z" }],
      "enable"
    );
    RequestRuntimeState.setCurrentAccountId("other00001");
    fireEvent.click(screen.getByRole("button", { name: "Enable access" }));
    expect(screen.getByRole("alert")).toHaveTextContent(
      "selected account changed"
    );
    expect(
      changeAccess.mock.calls.filter(
        ([, , requestedAction]) => requestedAction === "enable"
      )
    ).toHaveLength(0);
  });

  it("revokes a mixed batch only after explicit confirmation, preserving email semantics", async () => {
    const onFinished = renderModal(people, "revoke");
    const confirm = screen.getByRole("button", { name: "Revoke memberships" });
    expect(confirm).toBeDisabled();
    expect(
      screen.getByRole("dialog", { name: "Revoke memberships?" })
    ).toHaveTextContent("account permissions and favorites");
    const message = screen.getByRole("textbox", { name: "Message" });
    expect(message).toHaveValue(
      "Your access to Test Survey has been removed. If you have any questions or believe this was a mistake, please reach out to an administrator."
    );
    expect(screen.getByLabelText("Affected people").contains(message)).toBe(
      false
    );
    fireEvent.change(
      screen.getByRole("textbox", { name: /Type REVOKE to confirm/ }),
      {
        target: { value: "REVOKE" },
      }
    );
    fireEvent.click(confirm);
    await waitFor(() => expect(changeAccess).toHaveBeenCalledTimes(2));
    expect(changeAccess).toHaveBeenCalledWith(
      "person0001",
      "account0001",
      "revoke",
      undefined,
      false
    );
    expect(changeAccess).toHaveBeenCalledWith(
      "person0002",
      "account0001",
      "revoke",
      expect.stringContaining("access to Test Survey has been removed"),
      true
    );
    expect(
      changeAccess.mock.calls.filter(
        ([, , requestedAction]) => requestedAction === "disable"
      )
    ).toHaveLength(0);
    expect(await screen.findByText("Membership revoked.")).toBeInTheDocument();
    expect(onFinished).toHaveBeenCalledTimes(1);
  });

  it("can revoke a disabled member and retry only that failed membership", async () => {
    changeAccess.mockRejectedValueOnce({ message: "Last administrator." });
    renderModal(
      [{ ...people[1], accessDisabledOn: "2026-11-01T12:00:00Z" }],
      "revoke"
    );
    fireEvent.change(
      screen.getByRole("textbox", { name: /Type REVOKE to confirm/ }),
      {
        target: { value: "REVOKE" },
      }
    );
    fireEvent.click(screen.getByRole("button", { name: "Revoke membership" }));
    fireEvent.click(
      await screen.findByRole("button", {
        name: "Retry access change for Active Member",
      })
    );
    await waitFor(() => expect(changeAccess).toHaveBeenCalledTimes(2));
    expect(await screen.findByText("Membership revoked.")).toBeInTheDocument();
  });

  it("refuses bulk revocation when the selected account changed", () => {
    renderModal(people, "revoke");
    fireEvent.change(
      screen.getByRole("textbox", { name: /Type REVOKE to confirm/ }),
      {
        target: { value: "REVOKE" },
      }
    );
    RequestRuntimeState.setCurrentAccountId("other00001");
    fireEvent.click(screen.getByRole("button", { name: "Revoke memberships" }));
    expect(screen.getByRole("alert")).toHaveTextContent(
      "selected account changed"
    );
    expect(
      changeAccess.mock.calls.filter(
        ([, , requestedAction]) => requestedAction === "revoke"
      )
    ).toHaveLength(0);
  });

  it("distinguishes pending cleanup from reversible disabling, with per-person results", async () => {
    const onFinished = renderModal();
    expect(screen.getByRole("dialog")).toHaveTextContent(
      "Active Member will lose access"
    );
    expect(screen.getByRole("dialog")).toHaveTextContent(
      "Pending Person's invitation will be canceled."
    );
    expect(screen.getByRole("textbox", { name: "Message" })).toBeRequired();
    expect(screen.getByRole("textbox", { name: "Message" })).toHaveValue(
      defaultMessage
    );
    fireEvent.click(screen.getByRole("button", { name: "Apply changes" }));
    await waitFor(() => expect(changeAccess).toHaveBeenCalledTimes(2));
    expect(changeAccess).toHaveBeenCalledWith(
      "person0001",
      "account0001",
      "disable",
      undefined,
      false
    );
    expect(changeAccess).toHaveBeenCalledWith(
      "person0002",
      "account0001",
      "disable",
      defaultMessage,
      true
    );
    expect(await screen.findByText("Invitation canceled.")).toBeInTheDocument();
    expect(await screen.findByText("Access disabled.")).toBeInTheDocument();
    expect(onFinished).toHaveBeenCalledTimes(1);
  });

  it("retries a single failed access change without repeating successful access changes", async () => {
    changeAccess.mockRejectedValueOnce({
      statusCode: 409,
      message: "Last active administrator.",
    });
    const onFinished = renderModal();
    fireEvent.click(screen.getByRole("button", { name: "Apply changes" }));
    fireEvent.click(
      await screen.findByRole("button", {
        name: "Retry access change for Pending Person",
      })
    );
    await waitFor(() => expect(changeAccess).toHaveBeenCalledTimes(3));
    expect(changeAccess).toHaveBeenNthCalledWith(
      3,
      "person0001",
      "account0001",
      "disable",
      undefined,
      false
    );
    expect(await screen.findByText("Invitation canceled.")).toBeInTheDocument();
    expect(onFinished).toHaveBeenCalledTimes(2);
  });

  it("includes one shared explanation for accepted members but notifies no pending invitees", async () => {
    const onFinished = renderModal();
    fireEvent.change(screen.getByRole("textbox", { name: "Message" }), {
      target: { value: "Membership has ended." },
    });
    fireEvent.click(screen.getByRole("button", { name: "Apply changes" }));
    await waitFor(() => expect(changeAccess).toHaveBeenCalledTimes(2));
    expect(changeAccess).toHaveBeenCalledWith(
      "person0002",
      "account0001",
      "disable",
      "Membership has ended.",
      true
    );
    expect(changeAccess).toHaveBeenCalledWith(
      "person0001",
      "account0001",
      "disable",
      undefined,
      false
    );
    expect(await screen.findByText("Invitation canceled.")).toBeInTheDocument();
    expect(onFinished).toHaveBeenCalledTimes(1);
  });

  it("refreshes the default only until the administrator edits it", async () => {
    const onFinished = jest.fn().mockResolvedValue(undefined);
    const dialog = (accountName: string, open = true) => (
      <Permissions accountName={accountName}>
        <AccountAccessChangeModal
          action="disable"
          open={open}
          selected={people}
          onClose={jest.fn()}
          onFinished={onFinished}
        />
      </Permissions>
    );
    const { rerender } = render(dialog("Test Survey"));
    const messageField = screen.getByRole("textbox", { name: "Message" });
    expect(messageField).toHaveValue(defaultMessage);

    rerender(dialog("Renamed Survey"));
    expect(messageField).toHaveValue(
      "Your access to Renamed Survey has been disabled. If you have any questions or believe this was a mistake, please reach out to an administrator."
    );

    fireEvent.change(messageField, {
      target: { value: "Membership review complete." },
    });
    rerender(dialog("Another Rename"));
    expect(messageField).toHaveValue("Membership review complete.");

    fireEvent.click(screen.getByRole("button", { name: "Apply changes" }));
    await waitFor(() =>
      expect(changeAccess).toHaveBeenCalledWith(
        "person0002",
        "account0001",
        "disable",
        "Membership review complete.",
        true
      )
    );
    expect(await screen.findByText("Access disabled.")).toBeInTheDocument();

    rerender(dialog("Another Rename", false));
    rerender(dialog("Another Rename"));
    expect(screen.getByRole("textbox", { name: "Message" })).toHaveValue(
      "Your access to Another Rename has been disabled. If you have any questions or believe this was a mistake, please reach out to an administrator."
    );
  });

  it("requires a nonblank message when disabling an accepted member", () => {
    renderModal();
    fireEvent.change(screen.getByRole("textbox", { name: "Message" }), {
      target: { value: "   " },
    });
    expect(
      screen.getByRole("button", { name: "Apply changes" })
    ).toBeDisabled();
    expect(
      changeAccess.mock.calls.filter(
        ([, , requestedAction]) => requestedAction === "disable"
      )
    ).toHaveLength(0);
  });

  it("allows disabling access silently, including retrying a failed operation", async () => {
    changeAccess
      .mockRejectedValueOnce({ message: "Temporary failure" })
      .mockResolvedValue({});
    renderModal([people[1]]);
    fireEvent.change(screen.getByRole("textbox", { name: "Message" }), {
      target: { value: "   " },
    });
    expect(
      screen.getByRole("button", { name: "Disable access" })
    ).toBeDisabled();
    fireEvent.click(
      screen.getByRole("checkbox", {
        name: "Notify by email",
      })
    );
    expect(
      screen.queryByRole("textbox", { name: "Message" })
    ).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Disable access" }));
    const retry = await screen.findByRole("button", {
      name: "Retry access change for Active Member",
    });
    expect(changeAccess).toHaveBeenNthCalledWith(
      1,
      "person0002",
      "account0001",
      "disable",
      undefined,
      false
    );
    fireEvent.click(retry);
    await waitFor(() => expect(changeAccess).toHaveBeenCalledTimes(2));
    expect(changeAccess).toHaveBeenNthCalledWith(
      2,
      "person0002",
      "account0001",
      "disable",
      undefined,
      false
    );
    expect(await screen.findByText("Access disabled.")).toBeInTheDocument();
  });

  it("does not require a message when only canceling pending invitations", async () => {
    renderModal([people[0]]);
    expect(
      screen.queryByRole("textbox", { name: "Message" })
    ).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Cancel invitation" }));
    await waitFor(() =>
      expect(changeAccess).toHaveBeenCalledWith(
        "person0001",
        "account0001",
        "disable",
        undefined,
        false
      )
    );
  });

  it("reports email failure separately from successful disabling", async () => {
    changeAccess.mockResolvedValueOnce({}).mockResolvedValueOnce({
      notificationEmailDeliveryStatus: MessageDeliveryStatus.SendFailed,
    });
    renderModal();
    fireEvent.click(screen.getByRole("button", { name: "Apply changes" }));
    expect(
      await screen.findByText(
        "Access disabled, but the email couldn't be sent."
      )
    ).toBeInTheDocument();
    expect(screen.getByText(/2 completed/)).toBeInTheDocument();
    expect(
      screen.queryByRole("button", {
        name: /Retry access change for Active Member/,
      })
    ).not.toBeInTheDocument();
  });

  it("refuses to act if the account selection changed while reviewing", () => {
    renderModal();
    RequestRuntimeState.setCurrentAccountId("other00001");
    fireEvent.click(screen.getByRole("button", { name: "Apply changes" }));
    expect(screen.getByRole("alert")).toHaveTextContent(
      "selected account changed"
    );
    expect(
      changeAccess.mock.calls.filter(
        ([, , requestedAction]) => requestedAction === "disable"
      )
    ).toHaveLength(0);
  });

  it("surfaces last-administrator conflicts rather than reporting success", async () => {
    changeAccess.mockRejectedValueOnce({
      statusCode: 409,
      message:
        "Cannot disable access for the account's last active administrator.",
    });
    const onFinished = renderModal();
    fireEvent.click(screen.getByRole("button", { name: "Apply changes" }));
    expect(
      await screen.findByText(
        /Cannot disable access for the account's last active administrator/
      )
    ).toBeInTheDocument();
    await waitFor(() => expect(changeAccess).toHaveBeenCalledTimes(2));
    expect(onFinished).toHaveBeenCalledTimes(1);
    expect(screen.getByText(/1 failed/)).toBeInTheDocument();
  });
});
