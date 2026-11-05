import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { message } from "antd";
import React, { useContext } from "react";
import { MemoryRouter } from "react-router-dom";
import { AppContext } from "../../../Configuration/Context/AppContext";
import { MessageDeliveryStatus } from "../../../Shared/Models/MessageDeliveryStatus";
import { RequestRuntimeState } from "../../../Shared/Http/RequestRuntimeState";
import { InvitationEmailAttemptVm } from "../Models/InvitationEmailHistoryVm";
import { UserManagerGridVm } from "../Models/UserManagerGridVm";
import { AccountAccessAction } from "../Models/AccountAccessChangeResultVm";
import { AccountUserManagerService } from "../Services/UserManagerService";
import { UserManagerComponent } from "./UserManagerComponent";

jest.mock(
  "../../../Shared/Components/EmailHistoryModal/EmailHistoryModal",
  () => ({
    EmailHistoryModal: ({
      open,
      title,
      attempts,
      emptyText,
    }: {
      open: boolean;
      title?: React.ReactNode;
      attempts: unknown[];
      emptyText?: React.ReactNode;
    }) =>
      open ? (
        <div role="dialog">
          <span>{title}</span>
          <span>{attempts.length} attempts</span>
          {attempts.length === 0 ? emptyText : null}
        </div>
      ) : null,
  })
);

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

const AppContextWithPermissions: React.FC<{ children: React.ReactNode }> = ({
  children,
}) => {
  const defaults = useContext(AppContext);
  return (
    <AppContext.Provider value={{
      ...defaults,
      currentUser: { id: "admin00001", fullName: "Test Admin", currentAccountId: "account0001" },
      hasPermission: () => true,
    }}>
      {children}
    </AppContext.Provider>
  );
};

const pendingUser: UserManagerGridVm = {
  userId: "user123456",
  emailAddress: "invitee@example.com",
  fullName: "Invited User",
  invitationAcceptedOn: null,
  invitationSentOn: null,
  lastActiveOn: null,
  accessDisabledOn: null,
  hasActiveInvitation: true,
  invitationEmailAttemptCount: 2,
};

const history: InvitationEmailAttemptVm[] = [
  {
    messageLogId: "message001",
    createdOn: "2026-08-14T12:00:00Z",
    deliveryStatus: MessageDeliveryStatus.Delivered,
    deliveryStatusOn: "2026-08-14T12:01:00Z",
    events: [],
  },
];

const renderManager = () =>
  render(
    <MemoryRouter>
      <AppContextWithPermissions>
        <UserManagerComponent />
      </AppContextWithPermissions>
    </MemoryRouter>
  );

describe("UserManagerComponent", () => {
  let getUsers: jest.SpyInstance;
  let getHistory: jest.SpyInstance;
  let inviteUser: jest.SpyInstance;
  let resendInvitation: jest.SpyInstance;
  let changeAccess: jest.SpyInstance;
  let warningSpy: jest.SpyInstance;
  let errorSpy: jest.SpyInstance;

  beforeEach(() => {
    RequestRuntimeState.setCurrentAccountId("account0001");
    getUsers = jest
      .spyOn(AccountUserManagerService, "GetAccountUsers")
      .mockResolvedValue([pendingUser]);
    getHistory = jest
      .spyOn(AccountUserManagerService, "GetInvitationEmailHistory")
      .mockResolvedValue(history);
    inviteUser = jest
      .spyOn(AccountUserManagerService, "InviteUser")
      .mockResolvedValue({
        userId: "newuser001",
        invitationEmailDeliveryStatus: MessageDeliveryStatus.Submitted,
      });
    resendInvitation = jest
      .spyOn(AccountUserManagerService, "ResendInvitation")
      .mockResolvedValue();
    changeAccess = jest
      .spyOn(AccountUserManagerService, "ChangeAccess")
      .mockResolvedValue({});
    warningSpy = jest
      .spyOn(message, "warning")
      .mockImplementation(() => undefined as never);
    errorSpy = jest
      .spyOn(message, "error")
      .mockImplementation(() => undefined as never);
  });

  afterEach(() => {
    RequestRuntimeState.setCurrentAccountId(null);
    jest.restoreAllMocks();
  });

  it("keeps a failed first-send invitation pending and exposes resend", async () => {
    renderManager();

    expect(await screen.findByText("Invited User")).toBeInTheDocument();
    expect(screen.getByText("Pending")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /Resend$/ })).toBeInTheDocument();
    expect(screen.getByText("Invitation Sent")).toBeInTheDocument();
    expect(screen.getByText("Invitation Accepted")).toBeInTheDocument();
    expect(screen.getByText("Last Active")).toBeInTheDocument();
    expect(screen.getAllByText("Not recorded")).toHaveLength(3);
  });

  it("shows legacy invitations as untracked while keeping resend available", async () => {
    getUsers.mockResolvedValue([
      {
        ...pendingUser,
        invitationSentOn: "2026-08-01T12:00:00Z",
        invitationEmailAttemptCount: 0,
      },
    ]);

    renderManager();
    await screen.findByText("Invited User");

    expect(screen.getByRole("button", { name: /Resend$/ })).toBeInTheDocument();
    expect(
      screen.queryByRole("button", { name: /Email History$/ })
    ).not.toBeInTheDocument();
  });

  it("keeps a newly created invitation when its first email submission fails", async () => {
    inviteUser.mockResolvedValue({
      userId: "newuser001",
      invitationEmailDeliveryStatus: MessageDeliveryStatus.SendFailed,
    });
    renderManager();
    await screen.findByText("Invited User");

    fireEvent.click(screen.getByRole("button", { name: /Invite people$/i }));
    fireEvent.change(await screen.findByRole("textbox", { name: "First name for person 1" }), {
      target: { value: "New" },
    });
    fireEvent.change(screen.getByRole("textbox", { name: "Last name for person 1" }), {
      target: { value: "Invitee" },
    });
    fireEvent.change(screen.getByRole("textbox", { name: "Email for person 1" }), {
      target: { value: "new@example.com" },
    });
    fireEvent.click(screen.getByRole("button", { name: /Review 1 person$/ }));
    fireEvent.click(screen.getByRole("button", { name: /Send 1 invitation$/ }));

    await waitFor(() =>
      expect(inviteUser).toHaveBeenCalledWith({
        firstName: "New",
        lastName: "Invitee",
        emailAddress: "new@example.com",
      }, "account0001")
    );
    await waitFor(() => expect(screen.getByText(/email could not be sent\. Use Resend/)).toBeInTheDocument());
    expect(warningSpy).not.toHaveBeenCalled();
  });

  it("reports resend failure without refreshing stale delivery state", async () => {
    resendInvitation.mockRejectedValue({
      message: "Mail provider unavailable.",
    });
    renderManager();
    await screen.findByText("Invited User");
    expect(getUsers).toHaveBeenCalledTimes(1);

    fireEvent.click(screen.getByRole("button", { name: /Resend$/ }));

    await waitFor(() =>
      expect(errorSpy).toHaveBeenCalledWith("Mail provider unavailable.")
    );
    expect(getUsers).toHaveBeenCalledTimes(1);
  });

  it("shows history-load failure and leaves an explicit empty state", async () => {
    getHistory.mockRejectedValue({ message: "History unavailable." });
    renderManager();
    await screen.findByText("Invited User");

    fireEvent.click(screen.getByRole("button", { name: /Email History$/ }));

    await waitFor(() =>
      expect(errorSpy).toHaveBeenCalledWith("History unavailable.")
    );
    expect(
      await screen.findByText("No tracked invitation emails.")
    ).toBeInTheDocument();
  });

  it("resends an active invitation and refreshes the authoritative list", async () => {
    renderManager();
    await screen.findByText("Invited User");
    expect(getUsers).toHaveBeenCalledTimes(1);

    fireEvent.click(screen.getByRole("button", { name: /Resend$/ }));

    await waitFor(() =>
      expect(resendInvitation).toHaveBeenCalledWith("user123456")
    );
    await waitFor(() => expect(getUsers).toHaveBeenCalledTimes(2));
  });

  it("loads invitation email history on demand", async () => {
    renderManager();
    await screen.findByText("Invited User");

    fireEvent.click(screen.getByRole("button", { name: /Email History$/ }));

    await waitFor(() => expect(getHistory).toHaveBeenCalledWith("user123456"));
    expect(
      await screen.findByText(/Invitation email history — Invited User/)
    ).toBeInTheDocument();
  });

  it("does not offer resend after the invitation has been accepted", async () => {
    getUsers.mockResolvedValue([
      {
        ...pendingUser,
        hasActiveInvitation: false,
        invitationAcceptedOn: "2026-08-14T14:00:00Z",
      },
    ]);

    renderManager();
    await screen.findByText("Invited User");

    expect(screen.queryByText("Pending")).not.toBeInTheDocument();
    expect(
      screen.queryByRole("button", { name: /Resend$/ })
    ).not.toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: /Email History$/ })
    ).toBeInTheDocument();
  });

  it("disables an accepted user's access and refreshes the authoritative list", async () => {
    getUsers.mockResolvedValue([
      {
        ...pendingUser,
        hasActiveInvitation: false,
        invitationAcceptedOn: "2026-08-14T14:00:00Z",
      },
    ]);

    renderManager();
    await screen.findByText("Invited User");

    fireEvent.click(screen.getByRole("button", { name: "Disable access" }));
    fireEvent.change(screen.getByRole("textbox", { name: "Message" }), {
      target: { value: "  Access review  " },
    });
    fireEvent.click(within(screen.getByRole("dialog", { name: "Disable access?" }))
      .getByRole("button", { name: "Disable access" }));

    await waitFor(() =>
      expect(changeAccess).toHaveBeenCalledWith("user123456", "account0001", AccountAccessAction.Disable, "Access review", true)
    );
    await waitFor(() => expect(getUsers).toHaveBeenCalledTimes(2));
  });

  it("restores a disabled user's access instead of offering another disable", async () => {
    getUsers.mockResolvedValue([
      {
        ...pendingUser,
        hasActiveInvitation: false,
        invitationAcceptedOn: "2026-08-14T14:00:00Z",
        accessDisabledOn: "2026-09-28T18:00:00Z",
      },
    ]);

    renderManager();
    await screen.findByText("Invited User");

    expect(screen.getByText("Access disabled")).toBeInTheDocument();
    expect(
      screen.queryByRole("button", { name: "Disable access" })
    ).not.toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: /Enable access/ }));
    const dialog = screen.getByRole("dialog", { name: "Enable access?" });
    expect(within(dialog).getByRole("textbox", { name: "Message" }))
      .toHaveValue("Your access to this account has been restored. If you have any questions, please reach out to an administrator.");
    fireEvent.click(within(dialog).getByRole("button", { name: "Enable access" }));

    await waitFor(() =>
      expect(changeAccess).toHaveBeenCalledWith(
        "user123456", "account0001", AccountAccessAction.Enable,
        "Your access to this account has been restored. If you have any questions, please reach out to an administrator.", true
      )
    );
    await waitFor(() => expect(getUsers).toHaveBeenCalledTimes(2));
  });

  it("keeps existing cards visible during a background refresh", async () => {
    const disabled = {
      ...pendingUser,
      hasActiveInvitation: false,
      invitationAcceptedOn: "2026-08-14T14:00:00Z",
      accessDisabledOn: "2026-09-28T18:00:00Z",
    };
    let finishRefresh!: (users: UserManagerGridVm[]) => void;
    const refresh = new Promise<UserManagerGridVm[]>((resolve) => {
      finishRefresh = resolve;
    });
    getUsers.mockReset().mockResolvedValueOnce([disabled]).mockReturnValueOnce(refresh);

    renderManager();
    await screen.findByText("Invited User");
    fireEvent.click(screen.getByRole("button", { name: /Enable access/ }));
    fireEvent.click(within(screen.getByRole("dialog", { name: "Enable access?" }))
      .getByRole("button", { name: "Enable access" }));
    await waitFor(() => expect(getUsers).toHaveBeenCalledTimes(2));

    expect(screen.getAllByText("Invited User").length).toBeGreaterThan(0);
    expect(document.querySelector(".planarian-spinner-card")).not.toBeInTheDocument();

    finishRefresh([{ ...disabled, accessDisabledOn: null }]);
    await waitFor(() =>
      expect(screen.queryByText("Access disabled")).not.toBeInTheDocument()
    );
  });

  it("requires explicit confirmation before revoking the membership of an accepted member", async () => {
    getUsers.mockResolvedValue([{
      ...pendingUser,
      hasActiveInvitation: false,
      invitationAcceptedOn: "2026-08-14T14:00:00Z",
    }]);
    renderManager();
    await screen.findByText("Invited User");

    fireEvent.click(screen.getByRole("button", { name: "Revoke membership" }));
    const dialog = screen.getByRole("dialog", { name: "Revoke membership?" });
    expect(dialog).toHaveTextContent("Invited User");
    expect(dialog).toHaveTextContent("will be completely removed from this account");
    expect(dialog).toHaveTextContent("membership, permissions, and favorites will be deleted");
    expect(dialog).toHaveTextContent("new invitation to rejoin");
    expect(dialog).toHaveTextContent("Previously contributed cave data will remain");
    const confirm = screen.getAllByRole("button", { name: "Revoke membership" })
      .find(button => dialog.contains(button))!;
    expect(confirm).toBeDisabled();
    expect(changeAccess.mock.calls.filter(([, , requestedAction]) => requestedAction === AccountAccessAction.Revoke)).toHaveLength(0);

    fireEvent.change(screen.getByRole("textbox", { name: /Type REVOKE to confirm/ }), {
      target: { value: "remove" },
    });
    expect(confirm).toBeDisabled();
    fireEvent.change(screen.getByRole("textbox", { name: /Type REVOKE to confirm/ }), {
      target: { value: "REVOKE" },
    });
    fireEvent.click(confirm);
    await waitFor(() =>
      expect(changeAccess).toHaveBeenCalledWith("user123456", "account0001", AccountAccessAction.Revoke,
        expect.stringContaining("access to this account has been removed"), true));
    await waitFor(() => expect(getUsers).toHaveBeenCalledTimes(2));
    expect(await screen.findByText("Membership revoked.")).toBeInTheDocument();
    expect(screen.getByRole("dialog", { name: "Results" })).toBeInTheDocument();
  });

  it("requires a message only if membership revocation emails are enabled", async () => {
    getUsers.mockResolvedValue([{
      ...pendingUser,
      hasActiveInvitation: false,
      invitationAcceptedOn: "2026-08-14T14:00:00Z",
    }]);
    renderManager();
    await screen.findByText("Invited User");
    fireEvent.click(screen.getByRole("button", { name: "Revoke membership" }));
    const dialog = screen.getByRole("dialog", { name: "Revoke membership?" });
    const notification = screen.getByRole("checkbox", { name: "Notify by email" });
    expect(notification).toBeChecked();

    const emailMessage = screen.getByRole("textbox", { name: "Message" }) as HTMLTextAreaElement;
    expect(emailMessage.value).toContain("access to this account has been removed");
    expect(emailMessage.value).not.toContain("favorites");
    fireEvent.change(emailMessage, { target: { value: "  " } });
    fireEvent.change(screen.getByRole("textbox", { name: /Type REVOKE to confirm/ }), {
      target: { value: "REVOKE" },
    });
    const confirm = screen.getAllByRole("button", { name: "Revoke membership" })
      .find(button => dialog.contains(button))!;
    expect(confirm).toBeDisabled();
    fireEvent.click(notification);
    expect(screen.queryByRole("textbox", { name: "Message" })).not.toBeInTheDocument();
    expect(confirm).toBeEnabled();
    fireEvent.click(confirm);
    await waitFor(() => expect(changeAccess).toHaveBeenCalledWith(
      "user123456", "account0001", AccountAccessAction.Revoke, undefined, false
    ));
  });

  it("reports email failure separately from completed membership revocation", async () => {
    changeAccess.mockResolvedValue({
      notificationEmailDeliveryStatus: MessageDeliveryStatus.SendFailed,
    });
    getUsers.mockResolvedValue([{
      ...pendingUser,
      hasActiveInvitation: false,
      invitationAcceptedOn: "2026-08-14T14:00:00Z",
    }]);
    renderManager();
    await screen.findByText("Invited User");
    fireEvent.click(screen.getByRole("button", { name: "Revoke membership" }));
    fireEvent.change(screen.getByRole("textbox", { name: /Type REVOKE to confirm/ }), {
      target: { value: "REVOKE" },
    });
    const dialog = screen.getByRole("dialog", { name: "Revoke membership?" });
    fireEvent.click(screen.getAllByRole("button", { name: "Revoke membership" })
      .find(button => dialog.contains(button))!);
    expect(await screen.findByText("Membership revoked, but the email couldn't be sent."))
      .toBeInTheDocument();
    await waitFor(() => expect(getUsers).toHaveBeenCalledTimes(2));
    expect(screen.getByRole("dialog", { name: "Results" })).toBeInTheDocument();
  });

  it("never claims an email was submitted when removal has no delivery status", async () => {
    const successSpy = jest.spyOn(message, "success")
      .mockImplementation(() => undefined as never);
    getUsers.mockResolvedValue([{
      ...pendingUser,
      hasActiveInvitation: false,
      invitationAcceptedOn: "2026-08-14T14:00:00Z",
    }]);
    renderManager();
    await screen.findByText("Invited User");
    fireEvent.click(screen.getByRole("button", { name: "Revoke membership" }));
    fireEvent.change(screen.getByRole("textbox", { name: /Type REVOKE to confirm/ }), {
      target: { value: "REVOKE" },
    });
    const dialog = screen.getByRole("dialog", { name: "Revoke membership?" });
    fireEvent.click(screen.getAllByRole("button", { name: "Revoke membership" })
      .find(button => dialog.contains(button))!);

    expect(await screen.findByText("Membership revoked.")).toBeInTheDocument();
  });

  it("never submits membership revocation for a different selected account", async () => {
    getUsers.mockResolvedValue([{
      ...pendingUser,
      hasActiveInvitation: false,
      invitationAcceptedOn: "2026-08-14T14:00:00Z",
    }]);
    renderManager();
    await screen.findByText("Invited User");

    fireEvent.click(screen.getByRole("button", { name: "Revoke membership" }));
    fireEvent.change(screen.getByRole("textbox", { name: /Type REVOKE to confirm/ }), {
      target: { value: "REVOKE" },
    });

    RequestRuntimeState.setCurrentAccountId("account0002");
    const dialog = screen.getByRole("dialog", { name: "Revoke membership?" });
    fireEvent.click(screen.getAllByRole("button", { name: "Revoke membership" })
      .find(button => dialog.contains(button))!);

    expect(changeAccess.mock.calls.filter(([, , requestedAction]) => requestedAction === AccountAccessAction.Revoke)).toHaveLength(0);
    expect(screen.getByRole("alert")).toHaveTextContent("selected account changed");
    expect(screen.getByRole("dialog", { name: "Revoke membership?" })).toBeInTheDocument();
  });

  it("allows membership revocation after disabling but not for a pending invitation", async () => {
    getUsers.mockResolvedValue([pendingUser, {
      ...pendingUser,
      userId: "disab00001",
      emailAddress: "disabled@example.com",
      fullName: "Disabled Member",
      hasActiveInvitation: false,
      accessDisabledOn: "2026-11-01T12:00:00Z",
    }]);
    renderManager();
    await screen.findByText("Disabled Member");
    expect(screen.getAllByRole("button", { name: "Revoke membership" })).toHaveLength(1);
    expect(screen.getByRole("button", { name: "Remove invitation" })).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Revoke membership" }));
    fireEvent.change(screen.getByRole("textbox", { name: /Type REVOKE to confirm/ }), {
      target: { value: "REVOKE" },
    });
    const dialog = screen.getByRole("dialog", { name: "Revoke membership?" });
    fireEvent.click(screen.getAllByRole("button", { name: "Revoke membership" })
      .find(button => dialog.contains(button))!);
    await waitFor(() => expect(changeAccess).toHaveBeenCalledWith(
      "disab00001", "account0001", AccountAccessAction.Revoke,
      expect.stringContaining("access to this account has been removed"), true));
  });

  it("keeps membership revocation confirmation available when deletion fails", async () => {
    changeAccess.mockRejectedValue({ message: "Cannot revoke the membership of the last administrator." });
    getUsers.mockResolvedValue([{
      ...pendingUser,
      hasActiveInvitation: false,
      invitationAcceptedOn: "2026-08-14T14:00:00Z",
    }]);
    renderManager();
    await screen.findByText("Invited User");
    fireEvent.click(screen.getByRole("button", { name: "Revoke membership" }));
    fireEvent.change(screen.getByRole("textbox", { name: /Type REVOKE to confirm/ }), {
      target: { value: "REVOKE" },
    });
    const dialog = screen.getByRole("dialog", { name: "Revoke membership?" });
    fireEvent.click(screen.getAllByRole("button", { name: "Revoke membership" })
      .find(button => dialog.contains(button))!);
    expect(await screen.findByText("Cannot revoke the membership of the last administrator."))
      .toBeInTheDocument();
    expect(dialog).toBeInTheDocument();
    await waitFor(() => expect(getUsers).toHaveBeenCalledTimes(2));
  });

  it("selects only eligible visible people and confirms a mixed disabling batch", async () => {
    const accepted = {
      ...pendingUser,
      userId: "accept0001",
      emailAddress: "member@example.com",
      fullName: "Accepted Member",
      hasActiveInvitation: false,
      invitationAcceptedOn: "2026-10-01T12:00:00Z",
    };
    const disabled = {
      ...accepted,
      userId: "disab00001",
      emailAddress: "disabled@example.com",
      fullName: "Disabled Member",
      accessDisabledOn: "2026-10-30T12:00:00Z",
    };
    getUsers.mockResolvedValue([pendingUser, accepted, disabled]);
    renderManager();
    await screen.findByText("Accepted Member");

    fireEvent.click(screen.getByRole("button", { name: /Select people$/ }));
    expect(screen.getByRole("checkbox", { name: "Select Disabled Member" })).toBeInTheDocument();
    fireEvent.click(screen.getByRole("checkbox", { name: /Select all shown/ }));
    expect(screen.getByText("3 selected")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Disable selected access" })).toBeDisabled();
    fireEvent.click(screen.getByRole("checkbox", { name: "Select Disabled Member" }));
    expect(screen.getByText("2 selected")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Disable selected access" }));
    expect(screen.getByRole("dialog")).toHaveTextContent("Accepted Member will lose access");
    expect(screen.getByRole("dialog")).toHaveTextContent("Invited User's invitation will be canceled.");
    fireEvent.click(screen.getByRole("button", { name: /Apply changes$/ }));

    await waitFor(() => {
      expect(changeAccess).toHaveBeenCalledWith(pendingUser.userId, "account0001", AccountAccessAction.Disable, undefined, false);
      expect(changeAccess).toHaveBeenCalledWith(accepted.userId, "account0001", AccountAccessAction.Disable,
        "Your access to this account has been disabled. If you have any questions or believe this was a mistake, please reach out to an administrator.", true);
    });
    await waitFor(() => expect(getUsers).toHaveBeenCalledTimes(2));
    expect(changeAccess).not.toHaveBeenCalledWith(disabled.userId, "account0001", AccountAccessAction.Disable, undefined);
    expect(screen.getByRole("button", { name: "Cancel selection" })).toBeInTheDocument();
    const results = screen.getByRole("dialog", { name: "Results" });
    fireEvent.click(within(results).getByRole("button", { name: /Close$/ }));
    expect(screen.queryByRole("button", { name: "Cancel selection" })).not.toBeInTheDocument();
  });

  it("offers bulk revocation for active and disabled memberships", async () => {
    const accepted = {
      ...pendingUser,
      userId: "accept0001",
      emailAddress: "accepted@example.com",
      fullName: "Accepted Member",
      hasActiveInvitation: false,
      invitationAcceptedOn: "2026-10-01T12:00:00Z",
    };
    const disabled = {
      ...accepted,
      userId: "disab00001",
      emailAddress: "disabled@example.com",
      fullName: "Disabled Member",
      accessDisabledOn: "2026-10-30T12:00:00Z",
    };
    getUsers.mockResolvedValue([accepted, disabled]);
    renderManager();
    await screen.findByText("Disabled Member");

    fireEvent.click(screen.getByRole("button", { name: "Select people" }));
    fireEvent.click(screen.getByRole("checkbox", { name: /Select all shown/ }));
    expect(screen.getByText("2 selected")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Disable selected access" })).toBeDisabled();
    fireEvent.click(screen.getByRole("button", { name: "Revoke selected memberships" }));
    const dialog = screen.getByRole("dialog", { name: "Revoke memberships?" });
    const confirm = within(dialog).getByRole("button", { name: "Revoke memberships" });
    expect(confirm).toBeDisabled();
    fireEvent.change(within(dialog).getByRole("textbox", { name: /Type REVOKE to confirm/ }), {
      target: { value: "REVOKE" },
    });
    fireEvent.click(confirm);
    await waitFor(() => expect(changeAccess).toHaveBeenCalledTimes(2));
    expect(changeAccess).toHaveBeenCalledWith("accept0001", "account0001", AccountAccessAction.Revoke,
      expect.stringContaining("access to this account has been removed"), true);
    expect(changeAccess).toHaveBeenCalledWith("disab00001", "account0001", AccountAccessAction.Revoke,
      expect.stringContaining("access to this account has been removed"), true);
    expect(changeAccess.mock.calls.filter(([, , requestedAction]) => requestedAction === AccountAccessAction.Disable)).toHaveLength(0);
    expect(await screen.findByText("Results")).toBeInTheDocument();
  });

  it("enables selected disabled accounts through the same batch dialog, optionally without email", async () => {
    const disabledUsers: UserManagerGridVm[] = ["disab00001", "disab00002"].map((userId) => ({
      ...pendingUser,
      userId,
      fullName: userId === "disab00001" ? "First Disabled" : "Second Disabled",
      hasActiveInvitation: false,
      invitationAcceptedOn: "2026-08-14T14:00:00Z",
      accessDisabledOn: "2026-09-28T18:00:00Z",
    }));
    getUsers.mockResolvedValue(disabledUsers);
    renderManager();
    await screen.findByText("First Disabled");

    fireEvent.click(screen.getByRole("button", { name: "Select people" }));
    fireEvent.click(screen.getByRole("checkbox", { name: /Select all shown/ }));
    expect(screen.getByRole("button", { name: "Disable selected access" })).toBeDisabled();
    fireEvent.click(screen.getByRole("button", { name: "Enable selected access" }));

    const dialog = screen.getByRole("dialog", { name: "Enable access?" });
    expect(dialog).toHaveTextContent("2 members will regain access");
    fireEvent.click(within(dialog).getByRole("checkbox", { name: "Notify by email" }));
    expect(within(dialog).queryByRole("textbox", { name: "Message" })).not.toBeInTheDocument();
    fireEvent.click(within(dialog).getByRole("button", { name: "Enable access" }));

    await waitFor(() => expect(changeAccess).toHaveBeenCalledTimes(2));
    expect(changeAccess).toHaveBeenCalledWith("disab00001", "account0001", AccountAccessAction.Enable, undefined, false);
    expect(changeAccess).toHaveBeenCalledWith("disab00002", "account0001", AccountAccessAction.Enable, undefined, false);
    expect(changeAccess.mock.calls.filter(([, , requestedAction]) => requestedAction === AccountAccessAction.Disable)).toHaveLength(0);
    expect(await screen.findAllByText("Access restored.")).toHaveLength(2);
  });

  it("refreshes users on account changes and discards late responses from the previous tenant", async () => {
    let finishPrevious!: (value: UserManagerGridVm[]) => void;
    const previous = new Promise<UserManagerGridVm[]>((resolve) => { finishPrevious = resolve; });
    const other = { ...pendingUser, userId: "other00001", fullName: "Other Account Person" };
    getUsers.mockReset()
      .mockImplementationOnce(() => previous)
      .mockResolvedValueOnce([other]);

    const AccountView: React.FC<{ accountId: string }> = ({ accountId }) => {
      const defaults = useContext(AppContext);
      return (
        <AppContext.Provider value={{
          ...defaults,
          currentUser: { id: "admin00001", fullName: "Test Admin", currentAccountId: accountId },
          hasPermission: () => true,
        }}>
          <UserManagerComponent />
        </AppContext.Provider>
      );
    };

    const { rerender } = render(
      <MemoryRouter><AccountView accountId="account0001" /></MemoryRouter>
    );
    expect(getUsers).toHaveBeenCalledWith("account0001");

    RequestRuntimeState.setCurrentAccountId("account0002");
    rerender(<MemoryRouter><AccountView accountId="account0002" /></MemoryRouter>);
    await screen.findByText("Other Account Person");

    finishPrevious([pendingUser]);
    await waitFor(() => expect(getUsers).toHaveBeenCalledTimes(2));
    expect(screen.queryByText("Invited User")).not.toBeInTheDocument();
    expect(getUsers).toHaveBeenNthCalledWith(2, "account0002");
  });

  it("clears bulk selection when the search changes", async () => {
    getUsers.mockResolvedValue([pendingUser]);
    renderManager();
    await screen.findByText("Invited User");
    fireEvent.click(screen.getByRole("button", { name: /Select people$/ }));
    fireEvent.click(screen.getByRole("checkbox", { name: "Select Invited User" }));
    expect(screen.getByText("1 selected")).toBeInTheDocument();
    fireEvent.change(screen.getByPlaceholderText("Search users"), {
      target: { value: "other" },
    });
    expect(screen.getByText("0 selected")).toBeInTheDocument();
  });
});
