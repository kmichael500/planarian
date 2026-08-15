import {
  act,
  fireEvent,
  render,
  screen,
  waitFor,
} from "@testing-library/react";
import { message } from "antd";
import React, { useContext } from "react";
import { MemoryRouter } from "react-router-dom";
import { AppContext } from "../../../Configuration/Context/AppContext";
import { AcceptInvitationVm } from "../../User/Models/AcceptInvitationVm";
import { UserService } from "../../User/UserService";
import { InvitationComponent } from "./InvitationComponent";

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
    <>
      <button type="button" disabled={disabled}>
        {children}
      </button>
      <button type="button" onClick={onConfirm} disabled={disabled}>
        Confirm {children}
      </button>
    </>
  ),
}));

jest.mock("./SwitchAccountComponent", () => ({
  SwitchAccountComponent: () => null,
}));

const invitation: AcceptInvitationVm = {
  invitationCode: "ABC123",
  firstName: "Test",
  lastName: "User",
  email: "user@example.com",
  regions: [],
  accountName: "Test Account",
  accountId: "account123",
};

let switchAccountMock: jest.Mock;
let refreshPendingInvitationsMock: jest.Mock;
let isAuthenticated = true;

const AppContextOverride: React.FC<{ children: React.ReactNode }> = ({
  children,
}) => {
  const defaults = useContext(AppContext);
  return (
    <AppContext.Provider
      value={{
        ...defaults,
        isAuthenticated,
        switchAccount: switchAccountMock,
        refreshPendingInvitations: refreshPendingInvitationsMock,
      }}
    >
      {children}
    </AppContext.Provider>
  );
};

const renderInvitation = () =>
  render(
    <MemoryRouter>
      <AppContextOverride>
        <InvitationComponent
          invitation={invitation}
          invitationCode={invitation.invitationCode}
          isLoading={false}
        />
      </AppContextOverride>
    </MemoryRouter>
  );

const deferred = () => {
  let resolve!: () => void;
  const promise = new Promise<void>((res) => {
    resolve = res;
  });
  return { promise, resolve };
};

describe("InvitationComponent", () => {
  beforeEach(() => {
    isAuthenticated = true;
    switchAccountMock = jest.fn();
    refreshPendingInvitationsMock = jest.fn().mockResolvedValue(undefined);
  });

  afterEach(() => {
    jest.restoreAllMocks();
  });

  it("prevents another invitation action while acceptance is in flight", async () => {
    const request = deferred();
    const acceptSpy = jest
      .spyOn(UserService, "AcceptInvitation")
      .mockReturnValue(request.promise);
    const declineSpy = jest
      .spyOn(UserService, "DeclineInvitation")
      .mockResolvedValue(undefined);
    jest.spyOn(message, "success").mockImplementation(() => undefined as any);

    renderInvitation();

    const acceptButton = screen.getByRole("button", { name: /Accept$/ });
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
    expect(switchAccountMock).toHaveBeenCalledWith("account123", "/caves");
  });

  it("keeps a committed decline successful when the badge refresh fails", async () => {
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

    renderInvitation();
    fireEvent.click(screen.getByRole("button", { name: "Decline" }));
    expect(declineSpy).not.toHaveBeenCalled();
    fireEvent.click(screen.getByRole("button", { name: "Confirm Decline" }));

    await waitFor(() => expect(declineSpy).toHaveBeenCalledWith("ABC123"));
    expect(await screen.findByText("Invitation Declined")).toBeInTheDocument();
    expect(refreshPendingInvitationsMock).toHaveBeenCalledTimes(1);
    expect(warningSpy).toHaveBeenCalledWith(
      "You have declined the invitation."
    );
    expect(errorSpy).not.toHaveBeenCalled();
  });

  it("lets signed-out invitation recipients decline after confirmation without exposing accept", async () => {
    isAuthenticated = false;
    const declineSpy = jest
      .spyOn(UserService, "DeclineInvitation")
      .mockResolvedValue(undefined);
    jest.spyOn(message, "warning").mockImplementation(() => undefined as any);

    renderInvitation();

    expect(
      screen.getByRole("button", { name: /Create a New Account$/ })
    ).toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: /I Have an Account$/ })
    ).toBeInTheDocument();
    expect(
      screen.queryByRole("button", { name: /Accept$/ })
    ).not.toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Decline" }));
    expect(declineSpy).not.toHaveBeenCalled();
    fireEvent.click(screen.getByRole("button", { name: "Confirm Decline" }));

    await waitFor(() => expect(declineSpy).toHaveBeenCalledWith("ABC123"));
    expect(await screen.findByText("Invitation Declined")).toBeInTheDocument();
  });
});
