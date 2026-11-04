import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { message } from "antd";
import React, { useContext } from "react";
import { MemoryRouter } from "react-router-dom";
import { AppContext } from "../../../Configuration/Context/AppContext";
import { MessageDeliveryStatus } from "../../../Shared/Models/MessageDeliveryStatus";
import { RequestRuntimeState } from "../../../Shared/Http/RequestRuntimeState";
import { InvitationEmailAttemptVm } from "../Models/InvitationEmailHistoryVm";
import { UserManagerGridVm } from "../Models/UserManagerGridVm";
import { AccountUserManagerService } from "../Services/UserManagerService";
import { UserManagerComponent } from "./UserManagerComponent";

jest.mock("../../../Shared/Components/Buttons/DeleteButtonComponent", () => ({
  DeleteButtonComponent: ({
    children,
    onConfirm,
  }: {
    children: React.ReactNode;
    onConfirm?: () => void;
  }) => (
    <button type="button" onClick={onConfirm}>
      {children}
    </button>
  ),
}));

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
    <AppContext.Provider value={{ ...defaults, hasPermission: () => true }}>
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
  accessRevokedOn: null,
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
  let revokeAccess: jest.SpyInstance;
  let restoreAccess: jest.SpyInstance;
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
    revokeAccess = jest
      .spyOn(AccountUserManagerService, "RevokeAccess")
      .mockResolvedValue();
    restoreAccess = jest
      .spyOn(AccountUserManagerService, "RestoreAccess")
      .mockResolvedValue();
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
    await waitFor(() => expect(screen.getByText(/email failed\. Use Resend/)).toBeInTheDocument());
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

  it("revokes an accepted user's access and refreshes the authoritative list", async () => {
    getUsers.mockResolvedValue([
      {
        ...pendingUser,
        hasActiveInvitation: false,
        invitationAcceptedOn: "2026-08-14T14:00:00Z",
      },
    ]);

    renderManager();
    await screen.findByText("Invited User");

    fireEvent.click(screen.getByRole("button", { name: "Revoke access" }));

    await waitFor(() =>
      expect(revokeAccess).toHaveBeenCalledWith("user123456")
    );
    await waitFor(() => expect(getUsers).toHaveBeenCalledTimes(2));
  });

  it("restores a revoked user's access instead of offering another revoke", async () => {
    getUsers.mockResolvedValue([
      {
        ...pendingUser,
        hasActiveInvitation: false,
        invitationAcceptedOn: "2026-08-14T14:00:00Z",
        accessRevokedOn: "2026-09-28T18:00:00Z",
      },
    ]);

    renderManager();
    await screen.findByText("Invited User");

    expect(screen.getByText("Access revoked")).toBeInTheDocument();
    expect(
      screen.queryByRole("button", { name: "Revoke access" })
    ).not.toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: /Restore access/ }));

    await waitFor(() =>
      expect(restoreAccess).toHaveBeenCalledWith("user123456")
    );
    await waitFor(() => expect(getUsers).toHaveBeenCalledTimes(2));
  });

  it("selects only eligible visible people and confirms a mixed revocation batch", async () => {
    const accepted = {
      ...pendingUser,
      userId: "accept0001",
      emailAddress: "member@example.com",
      fullName: "Accepted Member",
      hasActiveInvitation: false,
      invitationAcceptedOn: "2026-10-01T12:00:00Z",
    };
    const revoked = {
      ...accepted,
      userId: "revok00001",
      emailAddress: "revoked@example.com",
      fullName: "Revoked Member",
      accessRevokedOn: "2026-10-30T12:00:00Z",
    };
    getUsers.mockResolvedValue([pendingUser, accepted, revoked]);
    renderManager();
    await screen.findByText("Accepted Member");

    fireEvent.click(screen.getByRole("button", { name: /Select people$/ }));
    expect(screen.queryByRole("checkbox", { name: "Select Revoked Member" })).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole("checkbox", { name: /Select all shown/ }));
    expect(screen.getByText("2 selected")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: /Review access changes$/ }));
    expect(screen.getByRole("dialog")).toHaveTextContent("1 active member to revoke");
    expect(screen.getByRole("dialog")).toHaveTextContent("1 pending invitation to remove");
    fireEvent.click(screen.getByRole("button", { name: /Confirm changes for 2$/ }));

    await waitFor(() => {
      expect(revokeAccess).toHaveBeenCalledWith(pendingUser.userId, "account0001");
      expect(revokeAccess).toHaveBeenCalledWith(accepted.userId, "account0001");
    });
    await waitFor(() => expect(getUsers).toHaveBeenCalledTimes(2));
    expect(revokeAccess).not.toHaveBeenCalledWith(revoked.userId, "account0001");
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
