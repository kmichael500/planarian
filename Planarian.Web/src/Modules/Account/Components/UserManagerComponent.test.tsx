import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { message } from "antd";
import React, { useContext } from "react";
import { MemoryRouter } from "react-router-dom";
import { AppContext } from "../../../Configuration/Context/AppContext";
import { MessageDeliveryStatus } from "../../../Shared/Models/MessageDeliveryStatus";
import { InvitationEmailAttemptVm } from "../Models/InvitationEmailHistoryVm";
import { UserManagerGridVm } from "../Models/UserManagerGridVm";
import { AccountUserManagerService } from "../Services/UserManagerService";
import { UserManagerComponent } from "./UserManagerComponent";

jest.mock("../../../Shared/Components/Buttons/DeleteButtonComponent", () => ({
  DeleteButtonComponent: ({ children }: { children: React.ReactNode }) => (
    <button type="button">{children}</button>
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

describe("UserManagerComponent invitation delivery status", () => {
  let getUsers: jest.SpyInstance;
  let getHistory: jest.SpyInstance;
  let inviteUser: jest.SpyInstance;
  let resendInvitation: jest.SpyInstance;
  let warningSpy: jest.SpyInstance;
  let errorSpy: jest.SpyInstance;

  beforeEach(() => {
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
    warningSpy = jest
      .spyOn(message, "warning")
      .mockImplementation(() => undefined as never);
    errorSpy = jest
      .spyOn(message, "error")
      .mockImplementation(() => undefined as never);
  });

  afterEach(() => {
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

    fireEvent.click(screen.getByRole("button", { name: /Invite User$/ }));
    fireEvent.change(await screen.findByLabelText("First Name"), {
      target: { value: "New" },
    });
    fireEvent.change(screen.getByLabelText("Last Name"), {
      target: { value: "Invitee" },
    });
    fireEvent.change(screen.getByLabelText("Email Address"), {
      target: { value: "new@example.com" },
    });
    fireEvent.click(screen.getByRole("button", { name: "OK" }));

    await waitFor(() =>
      expect(inviteUser).toHaveBeenCalledWith({
        firstName: "New",
        lastName: "Invitee",
        emailAddress: "new@example.com",
      })
    );
    expect(warningSpy).toHaveBeenCalledWith(
      "Invitation created, but the email could not be sent. You can resend it from the user list."
    );
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

  it("puts email history inside the card body", async () => {
    renderManager();
    await screen.findByText("Invited User");

    const emailHistoryButton = screen.getByRole("button", {
      name: /Email History$/,
    });
    expect(
      emailHistoryButton.closest(".planarian-grid-card__body")
    ).not.toBeNull();
    expect(
      emailHistoryButton.closest(".planarian-grid-card__actions")
    ).toBeNull();
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
});
