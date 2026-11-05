import { act, render, screen, waitFor } from "@testing-library/react";
import { message } from "antd";
import React, { useContext } from "react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { AppContext } from "../../../Configuration/Context/AppContext";
import { ApiExceptionType } from "../../../Shared/Models/ApiErrorResponse";
import { UserManagerGridVm } from "../Models/UserManagerGridVm";
import { AccountUserManagerService } from "../Services/UserManagerService";
import { UserPage } from "./UserPage";

jest.mock("../Components/UserPermissionManagementList", () => ({
  UserPermissionManagementList: () => <div data-testid="user-permissions" />,
}));
jest.mock("../Components/CavePermissionManagementList", () => ({
  CavePermissionManagementList: () => <div data-testid="cave-permissions" />,
}));

const user: UserManagerGridVm = {
  userId: "user123456",
  fullName: "Jane Surveyor",
  emailAddress: "jane@example.com",
  invitationAcceptedOn: null,
  invitationSentOn: null,
  lastActiveOn: null,
  accessDisabledOn: null,
  hasActiveInvitation: false,
  invitationEmailAttemptCount: 0,
};

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

let setHeaderTitle: jest.Mock;
let setHeaderButtons: jest.Mock;

const TestContext: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const defaults = useContext(AppContext);
  return (
    <AppContext.Provider
      value={{
        ...defaults,
        currentAccountId: "account0001",
        hasPermission: () => true,
        setHeaderTitle,
        setHeaderButtons,
      }}
    >
      {children}
    </AppContext.Provider>
  );
};

const renderPage = () =>
  render(
    <MemoryRouter initialEntries={["/account/users/user123456"]}>
      <TestContext>
        <Routes>
          <Route path="/account/users/:userId" element={<UserPage />} />
        </Routes>
      </TestContext>
    </MemoryRouter>
  );

describe("UserPage", () => {
  beforeEach(() => {
    setHeaderTitle = jest.fn();
    setHeaderButtons = jest.fn();
  });

  afterEach(() => jest.restoreAllMocks());

  it("does not load permission sections until the user has been verified", async () => {
    let resolveUser!: (value: UserManagerGridVm) => void;
    jest.spyOn(AccountUserManagerService, "GetUserById").mockImplementation(
      () =>
        new Promise((resolve) => {
          resolveUser = resolve;
        })
    );

    renderPage();
    expect(screen.getByRole("status")).toHaveTextContent("Loading user");
    expect(screen.queryByTestId("user-permissions")).not.toBeInTheDocument();
    expect(screen.queryByTestId("cave-permissions")).not.toBeInTheDocument();

    await act(async () => resolveUser(user));

    expect(screen.getByTestId("user-permissions")).toBeInTheDocument();
    expect(screen.getByTestId("cave-permissions")).toBeInTheDocument();
    expect(setHeaderTitle).toHaveBeenLastCalledWith(["Jane Surveyor"]);
  });

  it("shows one inline not-found state without loading permissions or opening error toasts", async () => {
    jest.spyOn(AccountUserManagerService, "GetUserById").mockRejectedValue({
      statusCode: 404,
      errorCode: ApiExceptionType.NotFound,
      message: "User not found",
    });
    const errorToast = jest
      .spyOn(message, "error")
      .mockImplementation(() => undefined as never);

    renderPage();

    expect(await screen.findByText("User not found")).toBeInTheDocument();
    expect(
      screen.getByText("This user is not available in this account.")
    ).toBeInTheDocument();
    expect(screen.getByRole("link", { name: /Back/ })).toHaveAttribute(
      "href",
      "/account/users/"
    );
    expect(screen.queryByTestId("user-permissions")).not.toBeInTheDocument();
    expect(screen.queryByTestId("cave-permissions")).not.toBeInTheDocument();
    expect(errorToast).not.toHaveBeenCalled();
    expect(AccountUserManagerService.GetUserById).toHaveBeenCalledTimes(1);
  });

  it("shows a recoverable page error for other failures without rendering stale permissions", async () => {
    jest.spyOn(AccountUserManagerService, "GetUserById").mockRejectedValue({
      statusCode: 503,
      errorCode: ApiExceptionType.InternalServerError,
      message: "Server temporarily unavailable",
    });
    renderPage();
    await waitFor(() =>
      expect(screen.getByText("Unable to load user")).toBeInTheDocument()
    );
    expect(screen.getByText("Please try again.")).toBeInTheDocument();
    expect(screen.queryByTestId("user-permissions")).not.toBeInTheDocument();
    expect(screen.queryByTestId("cave-permissions")).not.toBeInTheDocument();
  });
});
