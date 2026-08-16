import {
  act,
  fireEvent,
  render,
  screen,
  waitFor,
} from "@testing-library/react";
import { message } from "antd";
import React, { StrictMode, useContext } from "react";
import { AppContext } from "../../../Configuration/Context/AppContext";
import { AcceptInvitationVm } from "../../User/Models/AcceptInvitationVm";
import { UserService } from "../../User/UserService";
import { AuthenticationService } from "../Services/AuthenticationService";
import { InvitationsPage } from "./InvitationsPage";

jest.mock("../../../Shared/Components/Buttons/PlanarianButtton", () => ({
  PlanarianButton: ({
    children,
    onClick,
    disabled,
  }: {
    children?: React.ReactNode;
    onClick?: () => void;
    disabled?: boolean;
  }) => (
    <button type="button" onClick={onClick} disabled={disabled}>
      {children}
    </button>
  ),
}));

jest.mock("../../../Shared/Components/Buttons/DeleteButtonComponent", () => ({
  DeleteButtonComponent: ({
    children,
    onConfirm,
    disabled,
  }: {
    children?: React.ReactNode;
    onConfirm?: () => void;
    disabled?: boolean;
  }) => (
    <button type="button" onClick={onConfirm} disabled={disabled}>
      {children}
    </button>
  ),
}));

jest.mock("../../../Shared/Components/Display/PlanarianTag", () => ({
  PlanarianTag: ({ children }: { children?: React.ReactNode }) => (
    <span>{children}</span>
  ),
}));

beforeAll(() => {
  Object.defineProperty(window, "matchMedia", {
    writable: true,
    value: (query: string) => ({
      matches: false,
      media: query,
      onchange: null,
      addListener: () => {},
      removeListener: () => {},
      addEventListener: () => {},
      removeEventListener: () => {},
      dispatchEvent: () => false,
    }),
  });
});

const deferred = <T,>() => {
  let resolve!: (value: T) => void;
  const promise = new Promise<T>((res) => {
    resolve = res;
  });
  return { promise, resolve };
};

const invitation = (accountName: string): AcceptInvitationVm => ({
  invitationCode: accountName === "Current Account" ? "CURRENT" : "STALE",
  firstName: "Test",
  lastName: "User",
  email: "user@example.com",
  regions: [],
  accountName,
  accountId: "account123",
});

let refreshPendingInvitationsMock: jest.Mock;

const AppContextOverride: React.FC<{ children: React.ReactNode }> = ({
  children,
}) => {
  const defaults = useContext(AppContext);
  return (
    <AppContext.Provider
      value={{
        ...defaults,
        refreshPendingInvitations: refreshPendingInvitationsMock,
      }}
    >
      {children}
    </AppContext.Provider>
  );
};

const renderPage = (strictMode = false) => {
  const page = (
    <AppContextOverride>
      <InvitationsPage />
    </AppContextOverride>
  );

  return render(strictMode ? <StrictMode>{page}</StrictMode> : page);
};

describe("InvitationsPage", () => {
  beforeEach(() => {
    refreshPendingInvitationsMock = jest.fn().mockResolvedValue(undefined);
  });

  afterEach(() => {
    jest.restoreAllMocks();
  });

  it("ignores a stale StrictMode invitation list after the current request succeeds", async () => {
    const staleRequest = deferred<AcceptInvitationVm[]>();
    const currentRequest = deferred<AcceptInvitationVm[]>();
    const getInvitationsSpy = jest
      .spyOn(UserService, "GetPendingInvitations")
      .mockImplementationOnce(() => staleRequest.promise)
      .mockImplementationOnce(() => currentRequest.promise);

    renderPage(true);

    await waitFor(() => expect(getInvitationsSpy).toHaveBeenCalledTimes(2));

    await act(async () => {
      currentRequest.resolve([invitation("Current Account")]);
    });
    expect(await screen.findByText("Current Account")).toBeInTheDocument();

    await act(async () => {
      staleRequest.resolve([invitation("Stale Account")]);
      await Promise.resolve();
    });

    expect(screen.getByText("Current Account")).toBeInTheDocument();
    expect(screen.queryByText("Stale Account")).not.toBeInTheDocument();
  });

  it("switches accounts after a committed acceptance without depending on a badge refresh", async () => {
    jest
      .spyOn(UserService, "GetPendingInvitations")
      .mockResolvedValue([invitation("Current Account")]);
    const acceptSpy = jest
      .spyOn(UserService, "AcceptInvitation")
      .mockResolvedValue(undefined);
    const switchAccountSpy = jest
      .spyOn(AuthenticationService, "SwitchAccount")
      .mockImplementation(() => undefined);
    const successSpy = jest
      .spyOn(message, "success")
      .mockImplementation(() => undefined as any);
    const errorSpy = jest
      .spyOn(message, "error")
      .mockImplementation(() => undefined as any);
    refreshPendingInvitationsMock.mockRejectedValue(
      new Error("refresh failed")
    );

    renderPage();
    await screen.findByText("Current Account");

    fireEvent.click(screen.getByRole("button", { name: "Accept" }));

    await waitFor(() => expect(acceptSpy).toHaveBeenCalledWith("CURRENT"));
    await waitFor(() =>
      expect(switchAccountSpy).toHaveBeenCalledWith("account123", "/caves")
    );
    expect(successSpy).toHaveBeenCalledWith(
      "You have accepted the invitation."
    );
    expect(errorSpy).not.toHaveBeenCalled();
  });

  it("removes a committed decline even if refreshing the pending badge fails", async () => {
    jest
      .spyOn(UserService, "GetPendingInvitations")
      .mockResolvedValue([invitation("Current Account")]);
    const declineSpy = jest
      .spyOn(UserService, "DeclineInvitation")
      .mockResolvedValue(undefined);
    const warningSpy = jest
      .spyOn(message, "warning")
      .mockImplementation(() => undefined as any);
    const errorSpy = jest
      .spyOn(message, "error")
      .mockImplementation(() => undefined as any);
    refreshPendingInvitationsMock.mockRejectedValue(
      new Error("refresh failed")
    );

    renderPage();
    await screen.findByText("Current Account");

    fireEvent.click(screen.getByRole("button", { name: "Decline" }));

    await waitFor(() => expect(declineSpy).toHaveBeenCalledWith("CURRENT"));
    await waitFor(() =>
      expect(screen.queryByText("Current Account")).not.toBeInTheDocument()
    );
    expect(refreshPendingInvitationsMock).toHaveBeenCalledTimes(1);
    expect(warningSpy).toHaveBeenCalledWith(
      "You have declined the invitation."
    );
    expect(errorSpy).not.toHaveBeenCalled();
  });

  it("prevents another invitation action while acceptance is in flight", async () => {
    const request = deferred<void>();
    jest
      .spyOn(UserService, "GetPendingInvitations")
      .mockResolvedValue([invitation("Current Account")]);
    const acceptSpy = jest
      .spyOn(UserService, "AcceptInvitation")
      .mockReturnValue(request.promise);
    const declineSpy = jest
      .spyOn(UserService, "DeclineInvitation")
      .mockResolvedValue(undefined);
    const switchAccountSpy = jest
      .spyOn(AuthenticationService, "SwitchAccount")
      .mockImplementation(() => undefined);
    jest.spyOn(message, "success").mockImplementation(() => undefined as any);

    renderPage();
    await screen.findByText("Current Account");

    const acceptButton = screen.getByRole("button", { name: "Accept" });
    const declineButton = screen.getByRole("button", { name: "Decline" });
    act(() => {
      acceptButton.dispatchEvent(new MouseEvent("click", { bubbles: true }));
      declineButton.dispatchEvent(new MouseEvent("click", { bubbles: true }));
      acceptButton.dispatchEvent(new MouseEvent("click", { bubbles: true }));
    });

    expect(acceptSpy).toHaveBeenCalledTimes(1);
    expect(declineSpy).not.toHaveBeenCalled();

    await act(async () => {
      request.resolve();
    });
    expect(switchAccountSpy).toHaveBeenCalledWith("account123", "/caves");
  });

  it("keeps an invitation visible when decline fails", async () => {
    jest
      .spyOn(UserService, "GetPendingInvitations")
      .mockResolvedValue([invitation("Current Account")]);
    jest
      .spyOn(UserService, "DeclineInvitation")
      .mockRejectedValue({ message: "Decline failed" });
    const errorSpy = jest
      .spyOn(message, "error")
      .mockImplementation(() => undefined as any);

    renderPage();
    await screen.findByText("Current Account");
    fireEvent.click(screen.getByRole("button", { name: "Decline" }));

    await waitFor(() =>
      expect(errorSpy).toHaveBeenCalledWith("Decline failed")
    );
    expect(screen.getByText("Current Account")).toBeInTheDocument();
    expect(refreshPendingInvitationsMock).not.toHaveBeenCalled();
  });
});
