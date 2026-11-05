import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import React, { useContext } from "react";
import { AppContext } from "../../../Configuration/Context/AppContext";
import { RequestRuntimeState } from "../../../Shared/Http/RequestRuntimeState";
import { MessageDeliveryStatus } from "../../../Shared/Models/MessageDeliveryStatus";
import { UserManagerGridVm } from "../Models/UserManagerGridVm";
import { AccountUserManagerService } from "../Services/UserManagerService";
import { BulkDisableModal } from "./BulkDisableModal";

const Permissions: React.FC<{ children: React.ReactNode; accountName?: string }> = ({
  children,
  accountName = "Test Survey",
}) => {
  const value = useContext(AppContext);
  return (
    <AppContext.Provider value={{
      ...value,
      currentAccountName: accountName,
      hasPermission: () => true,
    }}>
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

describe("BulkDisableModal", () => {
  let disable: jest.SpyInstance;
  const people = [member("person0001", true), member("person0002", false)];
  const defaultMessage =
    "An administrator has disabled your access to Test Survey on Planarian. An administrator can restore your access.";

  beforeEach(() => {
    RequestRuntimeState.setCurrentAccountId("account0001");
    disable = jest
      .spyOn(AccountUserManagerService, "DisableAccess")
      .mockResolvedValue({
        notificationEmailDeliveryStatus: MessageDeliveryStatus.Submitted,
      });
  });
  afterEach(() => {
    RequestRuntimeState.setCurrentAccountId(null);
    jest.restoreAllMocks();
  });
  const renderModal = (members = people) => {
    const onFinished = jest.fn().mockResolvedValue(undefined);
    render(
      <Permissions>
        <BulkDisableModal
          open
          selected={members}
          onClose={jest.fn()}
          onFinished={onFinished}
        />
      </Permissions>
    );
    return onFinished;
  };

  it("distinguishes pending cleanup from reversible disabling, with per-person results", async () => {
    const onFinished = renderModal();
    expect(screen.getByRole("dialog")).toHaveTextContent(
      "1 active member to disable"
    );
    expect(screen.getByRole("dialog")).toHaveTextContent(
      "1 pending invitation to remove"
    );
    expect(screen.getByRole("textbox", { name: "Message to users" }))
      .toBeRequired();
    expect(screen.getByRole("textbox", { name: "Message to users" }))
      .toHaveValue(defaultMessage);
    fireEvent.click(
      screen.getByRole("button", { name: "Confirm changes for 2" })
    );
    await waitFor(() => expect(disable).toHaveBeenCalledTimes(2));
    expect(disable).toHaveBeenCalledWith("person0001", "account0001", undefined, false);
    expect(disable).toHaveBeenCalledWith("person0002", "account0001", defaultMessage, true);
    expect(await screen.findByText("Invitation removed.")).toBeInTheDocument();
    expect(
      await screen.findByText("Access disabled. Notification email submitted.")
    ).toBeInTheDocument();
    expect(onFinished).toHaveBeenCalledTimes(1);
  });

  it("retries a single failed access change without repeating successful access changes", async () => {
    disable.mockRejectedValueOnce({
      statusCode: 409,
      message: "Last active administrator.",
    });
    const onFinished = renderModal();
    fireEvent.click(
      screen.getByRole("button", { name: "Confirm changes for 2" })
    );
    fireEvent.click(
      await screen.findByRole("button", {
        name: "Retry access change for Pending Person",
      })
    );
    await waitFor(() => expect(disable).toHaveBeenCalledTimes(3));
    expect(disable).toHaveBeenNthCalledWith(
      3,
      "person0001",
      "account0001",
      undefined,
      false
    );
    expect(await screen.findByText("Invitation removed.")).toBeInTheDocument();
    expect(onFinished).toHaveBeenCalledTimes(2);
  });

  it("includes one shared explanation for accepted members but notifies no pending invitees", async () => {
    const onFinished = renderModal();
    fireEvent.change(
      screen.getByRole("textbox", { name: "Message to users" }),
      {
        target: { value: "Membership has ended." },
      }
    );
    fireEvent.click(
      screen.getByRole("button", { name: "Confirm changes for 2" })
    );
    await waitFor(() => expect(disable).toHaveBeenCalledTimes(2));
    expect(disable).toHaveBeenCalledWith(
      "person0002",
      "account0001",
      "Membership has ended.",
      true
    );
    expect(disable).toHaveBeenCalledWith(
      "person0001",
      "account0001",
      undefined,
      false
    );
    expect(await screen.findByText("Invitation removed.")).toBeInTheDocument();
    expect(onFinished).toHaveBeenCalledTimes(1);
  });

  it("refreshes the default only until the administrator edits it", async () => {
    const onFinished = jest.fn().mockResolvedValue(undefined);
    const dialog = (accountName: string, open = true) => (
      <Permissions accountName={accountName}>
        <BulkDisableModal
          open={open}
          selected={people}
          onClose={jest.fn()}
          onFinished={onFinished}
        />
      </Permissions>
    );
    const { rerender } = render(dialog("Test Survey"));
    const messageField = screen.getByRole("textbox", { name: "Message to users" });
    expect(messageField).toHaveValue(defaultMessage);

    rerender(dialog("Renamed Survey"));
    expect(messageField).toHaveValue(
      "An administrator has disabled your access to Renamed Survey on Planarian. An administrator can restore your access."
    );

    fireEvent.change(messageField, { target: { value: "Membership review complete." } });
    rerender(dialog("Another Rename"));
    expect(messageField).toHaveValue("Membership review complete.");

    fireEvent.click(screen.getByRole("button", { name: "Confirm changes for 2" }));
    await waitFor(() => expect(disable).toHaveBeenCalledWith(
      "person0002", "account0001", "Membership review complete.", true
    ));
    expect(await screen.findByText("Access disabled. Notification email submitted."))
      .toBeInTheDocument();

    rerender(dialog("Another Rename", false));
    rerender(dialog("Another Rename"));
    expect(screen.getByRole("textbox", { name: "Message to users" })).toHaveValue(
      "An administrator has disabled your access to Another Rename on Planarian. An administrator can restore your access."
    );
  });

  it("requires a nonblank message when disabling an accepted member", () => {
    renderModal();
    fireEvent.change(screen.getByRole("textbox", { name: "Message to users" }), {
      target: { value: "   " },
    });
    expect(screen.getByRole("button", { name: "Confirm changes for 2" }))
      .toBeDisabled();
    expect(disable).not.toHaveBeenCalled();
  });

  it("allows disabling access silently, including retrying a failed operation", async () => {
    disable.mockRejectedValueOnce({ message: "Temporary failure" }).mockResolvedValue({});
    renderModal([people[1]]);
    fireEvent.change(screen.getByRole("textbox", { name: "Message to users" }), {
      target: { value: "   " },
    });
    expect(screen.getByRole("button", { name: "Confirm changes for 1" })).toBeDisabled();
    fireEvent.click(screen.getByRole("checkbox", {
      name: "Send notification email to disabled members",
    }));
    expect(screen.queryByRole("textbox", { name: "Message to users" })).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Confirm changes for 1" }));
    const retry = await screen.findByRole("button", {
      name: "Retry access change for Active Member",
    });
    expect(disable).toHaveBeenNthCalledWith(1, "person0002", "account0001", undefined, false);
    fireEvent.click(retry);
    await waitFor(() => expect(disable).toHaveBeenCalledTimes(2));
    expect(disable).toHaveBeenNthCalledWith(2, "person0002", "account0001", undefined, false);
    expect(await screen.findByText("Access disabled."))
      .toBeInTheDocument();
  });

  it("does not require a message when only canceling pending invitations", async () => {
    renderModal([people[0]]);
    expect(screen.queryByRole("textbox", { name: "Message to users" }))
      .not.toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Confirm changes for 1" }));
    await waitFor(() => expect(disable).toHaveBeenCalledWith(
      "person0001", "account0001", undefined, false
    ));
  });

  it("reports email failure separately from successful disabling", async () => {
    disable
      .mockResolvedValueOnce({})
      .mockResolvedValueOnce({
        notificationEmailDeliveryStatus: MessageDeliveryStatus.SendFailed,
      });
    renderModal();
    fireEvent.click(
      screen.getByRole("button", { name: "Confirm changes for 2" })
    );
    expect(
      await screen.findByText("Access disabled. Notification email failed.")
    ).toBeInTheDocument();
    expect(screen.getByText(/2 succeeded/)).toBeInTheDocument();
    expect(
      screen.queryByRole("button", {
        name: /Retry access change for Active Member/,
      })
    ).not.toBeInTheDocument();
  });

  it("refuses to act if the account selection changed while reviewing", () => {
    renderModal();
    RequestRuntimeState.setCurrentAccountId("other00001");
    fireEvent.click(
      screen.getByRole("button", { name: "Confirm changes for 2" })
    );
    expect(screen.getByRole("alert")).toHaveTextContent(
      "selected account changed"
    );
    expect(disable).not.toHaveBeenCalled();
  });

  it("surfaces last-administrator conflicts rather than reporting success", async () => {
    disable.mockRejectedValueOnce({
      statusCode: 409,
      message:
        "Cannot disable access for the account's last active administrator.",
    });
    const onFinished = renderModal();
    fireEvent.click(
      screen.getByRole("button", { name: "Confirm changes for 2" })
    );
    expect(
      await screen.findByText(
        /Cannot disable access for the account's last active administrator/
      )
    ).toBeInTheDocument();
    await waitFor(() => expect(disable).toHaveBeenCalledTimes(2));
    expect(onFinished).toHaveBeenCalledTimes(1);
    expect(screen.getByText(/1 failed/)).toBeInTheDocument();
  });
});
