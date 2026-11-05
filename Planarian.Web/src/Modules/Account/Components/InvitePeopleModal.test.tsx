import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import React, { useContext } from "react";
import { AppContext } from "../../../Configuration/Context/AppContext";
import { RequestRuntimeState } from "../../../Shared/Http/RequestRuntimeState";
import { MessageDeliveryStatus } from "../../../Shared/Models/MessageDeliveryStatus";
import { UserManagerGridVm } from "../Models/UserManagerGridVm";
import { AccountUserManagerService } from "../Services/UserManagerService";
import { InvitePeopleModal } from "./InvitePeopleModal";

const TestPermissions: React.FC<{ children: React.ReactNode }> = ({
  children,
}) => {
  const value = useContext(AppContext);
  return (
    <AppContext.Provider value={{ ...value, hasPermission: () => true }}>
      {children}
    </AppContext.Provider>
  );
};

const member = (emailAddress: string): UserManagerGridVm => ({
  userId: "existing01",
  fullName: "Jane Smith",
  emailAddress,
  invitationAcceptedOn: "2026-09-01T12:00:00Z",
  invitationSentOn: null,
  lastActiveOn: null,
  accessDisabledOn: null,
  hasActiveInvitation: false,
  invitationEmailAttemptCount: 0,
});

const renderModal = (existingUsers: UserManagerGridVm[] = []) => {
  const onChanged = jest.fn().mockResolvedValue(undefined);
  render(
    <TestPermissions>
      <InvitePeopleModal
        open
        existingUsers={existingUsers}
        onChanged={onChanged}
        onClose={jest.fn()}
        onManagePermissions={jest.fn()}
      />
    </TestPermissions>
  );
  return { onChanged };
};

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

describe("InvitePeopleModal", () => {
  let invite: jest.SpyInstance;
  beforeEach(() => {
    RequestRuntimeState.setCurrentAccountId("account0001");
    invite = jest
      .spyOn(AccountUserManagerService, "InviteUser")
      .mockResolvedValue({
        userId: "newuser001",
        invitationEmailDeliveryStatus: MessageDeliveryStatus.Submitted,
      });
  });

  afterEach(() => {
    RequestRuntimeState.setCurrentAccountId(null);
    jest.restoreAllMocks();
  });

  const paste = async (text: string) => {
    fireEvent.click(screen.getByRole("button", { name: /Paste rows$/ }));
    fireEvent.change(
      screen.getByRole("textbox", { name: "Paste three spreadsheet columns" }),
      {
        target: { value: text },
      }
    );
    fireEvent.click(screen.getByRole("button", { name: /Add pasted rows$/ }));
  };

  it("pastes and reviews multiple people before dispatching account-pinned requests", async () => {
    const { onChanged } = renderModal();
    await paste(
      "First Name\tLast Name\tEmail\nJane\tSmith\tjane@example.test\nJohn\tDoe\tjohn@example.test"
    );
    expect(
      screen.getByRole("textbox", { name: "First name for person 2" })
    ).toHaveValue("John");
    fireEvent.click(screen.getByRole("button", { name: "Review 2 people" }));
    expect(
      screen.getByText("2 ready to invite · 0 to skip.")
    ).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Send 2 invitations" }));
    await waitFor(() => expect(invite).toHaveBeenCalledTimes(2));
    expect(invite).toHaveBeenCalledWith(
      {
        firstName: "Jane",
        lastName: "Smith",
        emailAddress: "jane@example.test",
      },
      "account0001"
    );
    expect(invite).toHaveBeenCalledWith(
      {
        firstName: "John",
        lastName: "Doe",
        emailAddress: "john@example.test",
      },
      "account0001"
    );
    await waitFor(() => expect(onChanged).toHaveBeenCalledTimes(1));
    expect(screen.getAllByText("Invitation submitted.")).toHaveLength(2);
  });

  it("previews existing members as skips rather than sending to them", async () => {
    renderModal([member("JANE@example.test")]);
    await paste("Jane,Smith,jane@example.test\nJohn,Doe,john@example.test");
    fireEvent.click(screen.getByRole("button", { name: "Review 2 people" }));
    expect(
      screen.getByText("1 ready to invite · 1 to skip.")
    ).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Send 1 invitation" }));
    await waitFor(() => expect(invite).toHaveBeenCalledTimes(1));
    expect(invite).toHaveBeenCalledWith(
      {
        firstName: "John",
        lastName: "Doe",
        emailAddress: "john@example.test",
      },
      "account0001"
    );
    expect(screen.getAllByText("Already a member.").length).toBeGreaterThan(0);
  });

  it("shows server-side already-invited responses without claiming an email was sent", async () => {
    invite.mockResolvedValue({ userId: "existing01", alreadyInvited: true });
    renderModal();
    fireEvent.change(
      screen.getByRole("textbox", { name: "First name for person 1" }),
      {
        target: { value: "Jane" },
      }
    );
    fireEvent.change(
      screen.getByRole("textbox", { name: "Last name for person 1" }),
      {
        target: { value: "Smith" },
      }
    );
    fireEvent.change(
      screen.getByRole("textbox", { name: "Email for person 1" }),
      {
        target: { value: "jane@example.test" },
      }
    );
    fireEvent.click(screen.getByRole("button", { name: "Review 1 person" }));
    fireEvent.click(screen.getByRole("button", { name: "Send 1 invitation" }));
    expect(
      await screen.findByText("Already invited — use Resend.")
    ).toBeInTheDocument();
    expect(screen.queryByText("Invitation submitted.")).not.toBeInTheDocument();
  });

  it("resends mail after an invitation was created but the provider failed", async () => {
    invite.mockResolvedValueOnce({
      userId: "newuser001",
      invitationEmailDeliveryStatus: MessageDeliveryStatus.SendFailed,
    });
    const resend = jest
      .spyOn(AccountUserManagerService, "ResendInvitation")
      .mockResolvedValue();
    renderModal();
    await paste("Jane,Smith,jane@example.test");
    fireEvent.click(screen.getByRole("button", { name: "Review 1 person" }));
    fireEvent.click(screen.getByRole("button", { name: "Send 1 invitation" }));
    fireEvent.click(
      await screen.findByRole("button", {
        name: "Resend email to jane@example.test",
      })
    );
    await waitFor(() =>
      expect(resend).toHaveBeenCalledWith("newuser001", "account0001")
    );
    expect(invite).toHaveBeenCalledTimes(1);
    expect(
      await screen.findByText("Invitation resubmitted.")
    ).toBeInTheDocument();
  });

  it("retries one failed invitation without retrying the successful recipients", async () => {
    invite
      .mockRejectedValueOnce({
        statusCode: 503,
        message: "Mail gateway unavailable.",
      })
      .mockResolvedValue({
        userId: "newuser001",
        invitationEmailDeliveryStatus: MessageDeliveryStatus.Submitted,
      });
    renderModal();
    await paste("Jane,Smith,jane@example.test\nJohn,Doe,john@example.test");
    fireEvent.click(screen.getByRole("button", { name: "Review 2 people" }));
    fireEvent.click(screen.getByRole("button", { name: "Send 2 invitations" }));
    fireEvent.click(
      await screen.findByRole("button", {
        name: "Retry invitation for jane@example.test",
      })
    );
    await waitFor(() => expect(invite).toHaveBeenCalledTimes(3));
    expect(invite).toHaveBeenNthCalledWith(
      3,
      {
        firstName: "Jane",
        lastName: "Smith",
        emailAddress: "jane@example.test",
      },
      "account0001"
    );
    await waitFor(() =>
      expect(screen.getAllByText("Invitation submitted.")).toHaveLength(2)
    );
  });

  it("imports CSV through the file chooser", async () => {
    renderModal();
    const file = new File(
      ["First Name,Last Name,Email\nJane,Smith,jane@example.test"],
      "invites.csv",
      { type: "text/csv" }
    );
    Object.defineProperty(file, "text", {
      value: async () =>
        "First Name,Last Name,Email\nJane,Smith,jane@example.test",
    });
    fireEvent.change(screen.getByLabelText("Import invitations CSV"), {
      target: { files: [file] },
    });
    await waitFor(() =>
      expect(
        screen.getByRole("textbox", { name: "First name for person 1" })
      ).toHaveValue("Jane")
    );
  });

  it("refuses to dispatch if the account changes between review and sending", async () => {
    renderModal();
    await paste("Jane,Smith,jane@example.test");
    fireEvent.click(screen.getByRole("button", { name: "Review 1 person" }));
    RequestRuntimeState.setCurrentAccountId("other00001");
    fireEvent.click(screen.getByRole("button", { name: "Send 1 invitation" }));
    expect(screen.getByRole("alert")).toHaveTextContent(
      "selected account changed"
    );
    expect(invite).not.toHaveBeenCalled();
  });

  it("requires names and email and prevents incomplete requests", () => {
    renderModal();
    fireEvent.click(screen.getByRole("button", { name: "Review 1 person" }));
    expect(screen.getByRole("alert")).toHaveTextContent(
      "Correct the highlighted entries"
    );
    expect(invite).not.toHaveBeenCalled();
  });
});
