import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import React, { useContext } from "react";
import { AppContext } from "../../../Configuration/Context/AppContext";
import { RequestRuntimeState } from "../../../Shared/Http/RequestRuntimeState";
import { UserManagerGridVm } from "../Models/UserManagerGridVm";
import { AccountUserManagerService } from "../Services/UserManagerService";
import { BulkRevokeModal } from "./BulkRevokeModal";

const Permissions: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const value = useContext(AppContext);
  return (
    <AppContext.Provider value={{ ...value, hasPermission: () => true }}>
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
  accessRevokedOn: null,
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

describe("BulkRevokeModal", () => {
  let revoke: jest.SpyInstance;
  const people = [member("person0001", true), member("person0002", false)];

  beforeEach(() => {
    RequestRuntimeState.setCurrentAccountId("account0001");
    revoke = jest
      .spyOn(AccountUserManagerService, "RevokeAccess")
      .mockResolvedValue();
  });
  afterEach(() => {
    RequestRuntimeState.setCurrentAccountId(null);
    jest.restoreAllMocks();
  });
  const renderModal = () => {
    const onFinished = jest.fn().mockResolvedValue(undefined);
    render(
      <Permissions>
        <BulkRevokeModal
          open
          selected={people}
          onClose={jest.fn()}
          onFinished={onFinished}
        />
      </Permissions>
    );
    return onFinished;
  };

  it("distinguishes pending cleanup from reversible revocation, with per-person results", async () => {
    const onFinished = renderModal();
    expect(screen.getByRole("dialog")).toHaveTextContent(
      "1 active member to revoke"
    );
    expect(screen.getByRole("dialog")).toHaveTextContent(
      "1 pending invitation to remove"
    );
    fireEvent.click(
      screen.getByRole("button", { name: "Confirm changes for 2" })
    );
    await waitFor(() => expect(revoke).toHaveBeenCalledTimes(2));
    expect(revoke).toHaveBeenCalledWith("person0001", "account0001");
    expect(revoke).toHaveBeenCalledWith("person0002", "account0001");
    expect(await screen.findByText("Invitation removed.")).toBeInTheDocument();
    expect(await screen.findByText("Access revoked.")).toBeInTheDocument();
    expect(onFinished).toHaveBeenCalledTimes(1);
  });

  it("retries a single failed access change without repeating successful revocations", async () => {
    revoke.mockRejectedValueOnce({
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
    await waitFor(() => expect(revoke).toHaveBeenCalledTimes(3));
    expect(revoke).toHaveBeenNthCalledWith(3, "person0001", "account0001");
    expect(await screen.findByText("Invitation removed.")).toBeInTheDocument();
    expect(onFinished).toHaveBeenCalledTimes(2);
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
    expect(revoke).not.toHaveBeenCalled();
  });

  it("surfaces last-administrator conflicts rather than reporting success", async () => {
    revoke.mockRejectedValueOnce({
      statusCode: 409,
      message:
        "Cannot revoke access for the account's last active administrator.",
    });
    const onFinished = renderModal();
    fireEvent.click(
      screen.getByRole("button", { name: "Confirm changes for 2" })
    );
    expect(
      await screen.findByText(
        /Cannot revoke access for the account's last active administrator/
      )
    ).toBeInTheDocument();
    await waitFor(() => expect(revoke).toHaveBeenCalledTimes(2));
    expect(onFinished).toHaveBeenCalledTimes(1);
    expect(screen.getByText(/1 failed/)).toBeInTheDocument();
  });
});
