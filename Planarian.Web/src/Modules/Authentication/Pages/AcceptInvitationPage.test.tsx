import {
  act,
  fireEvent,
  render,
  screen,
  waitFor,
} from "@testing-library/react";
import { message } from "antd";
import React, { StrictMode } from "react";
import { MemoryRouter, Route, Routes, useNavigate } from "react-router-dom";
import { AcceptInvitationVm } from "../../User/Models/AcceptInvitationVm";
import { UserService } from "../../User/UserService";
import { AcceptInvitationPage } from "./AcceptInvitationPage";

jest.mock("../Components/InvitationComponent", () => ({
  InvitationComponent: ({
    invitation,
    isLoading,
  }: {
    invitation?: AcceptInvitationVm;
    isLoading: boolean;
  }) => (
    <>
      <div>Loading: {String(isLoading)}</div>
      <div>{invitation?.accountName ?? "No invitation"}</div>
    </>
  ),
}));

const deferred = <T,>() => {
  let resolve!: (value: T) => void;
  let reject!: (reason?: unknown) => void;
  const promise = new Promise<T>((res, rej) => {
    resolve = res;
    reject = rej;
  });
  return { promise, resolve, reject };
};

const invitation: AcceptInvitationVm = {
  invitationCode: "ABC123",
  firstName: "Test",
  lastName: "User",
  email: "user@example.com",
  regions: [],
  accountName: "Current Account",
  accountId: "account123",
};

const RouteControls = () => {
  const navigate = useNavigate();
  return (
    <button type="button" onClick={() => navigate("/user/invitations/SECOND")}>
      Load second invitation
    </button>
  );
};

describe("AcceptInvitationPage", () => {
  afterEach(() => {
    jest.restoreAllMocks();
  });

  it("ignores a stale StrictMode invitation failure after the current request succeeds", async () => {
    const staleRequest = deferred<AcceptInvitationVm>();
    const currentRequest = deferred<AcceptInvitationVm>();
    const getInvitationSpy = jest
      .spyOn(UserService, "GetInvitation")
      .mockImplementationOnce(() => staleRequest.promise)
      .mockImplementationOnce(() => currentRequest.promise);
    const errorSpy = jest
      .spyOn(message, "error")
      .mockImplementation(() => undefined as any);

    render(
      <StrictMode>
        <MemoryRouter initialEntries={["/user/invitations/ABC123"]}>
          <Routes>
            <Route
              path="/user/invitations/:invitationCode"
              element={<AcceptInvitationPage />}
            />
          </Routes>
        </MemoryRouter>
      </StrictMode>
    );

    await waitFor(() => expect(getInvitationSpy).toHaveBeenCalledTimes(2));

    await act(async () => {
      currentRequest.resolve(invitation);
    });
    expect(await screen.findByText("Current Account")).toBeInTheDocument();
    expect(screen.getByText("Loading: false")).toBeInTheDocument();

    await act(async () => {
      staleRequest.reject({ message: "Invitation not found" });
      await Promise.resolve();
    });

    expect(errorSpy).not.toHaveBeenCalled();
    expect(screen.getByText("Current Account")).toBeInTheDocument();
  });

  it("clears the previous invitation while a different route code is loading", async () => {
    const replacementRequest = deferred<AcceptInvitationVm>();
    const getInvitationSpy = jest
      .spyOn(UserService, "GetInvitation")
      .mockResolvedValueOnce(invitation)
      .mockImplementationOnce(() => replacementRequest.promise);
    jest.spyOn(message, "error").mockImplementation(() => undefined as any);

    render(
      <MemoryRouter initialEntries={["/user/invitations/ABC123"]}>
        <Routes>
          <Route
            path="/user/invitations/:invitationCode"
            element={
              <>
                <AcceptInvitationPage />
                <RouteControls />
              </>
            }
          />
        </Routes>
      </MemoryRouter>
    );

    expect(await screen.findByText("Current Account")).toBeInTheDocument();

    fireEvent.click(
      screen.getByRole("button", { name: "Load second invitation" })
    );

    await waitFor(() =>
      expect(getInvitationSpy).toHaveBeenLastCalledWith("SECOND")
    );
    expect(screen.getByText("Loading: true")).toBeInTheDocument();
    expect(screen.getByText("No invitation")).toBeInTheDocument();
    expect(screen.queryByText("Current Account")).not.toBeInTheDocument();

    await act(async () => {
      replacementRequest.resolve({
        ...invitation,
        invitationCode: "SECOND",
        accountName: "Second Account",
        accountId: "account456",
      });
    });
    expect(await screen.findByText("Second Account")).toBeInTheDocument();
  });
});
