import { act, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { message } from "antd";
import React, { useContext } from "react";
import { MemoryRouter, Route, Routes, useLocation } from "react-router-dom";
import { AccountService } from "../../Modules/Account/Services/AccountService";
import { FeatureKey, FeatureSettingVm } from "../../Modules/Account/Models/FeatureSettingVm";
import { AuthenticationService } from "../../Modules/Authentication/Services/AuthenticationService";
import { ProtectedRoutesComponent } from "../../Modules/Authentication/Components/ProtectedRoutesComponent";
import { UserService } from "../../Modules/User/UserService";
import { RequestRuntimeState } from "../../Shared/Http/RequestRuntimeState";
import { ApiExceptionType } from "../../Shared/Models/ApiErrorResponse";
import { AppService, AppInitializeVm } from "../../Shared/Services/AppService";
import { AppRederect } from "../Routing/App.routing.redirect";
import { ClientRoutes } from "../Routing/ClientRoutes.generated";
import { AppContext, AppProvider } from "./AppContext";

const active = (id: string): AppInitializeVm => ({
  apiBaseUrl: "https://example.test",
  signalrBaseUrl: "https://example.test/hub",
  supportName: "",
  supportEmail: "",
  antiforgeryRequestToken: null,
  currentUser: { id: "testuser01", fullName: "Test User", currentAccountId: id },
  accountIds: [{ display: "Active account", value: id }],
  disabledAccountIds: [],
  permissions: [],
});

const availableWithDisabled = {
  ...active("active0001"),
  disabledAccountIds: [
    { display: "Disabled account", value: "disabled001", disableMessage: "Your annual membership expired." },
  ],
};

const NoAccount = {
  ...active("active0001"),
  currentUser: { id: "testuser01", fullName: "Test User", currentAccountId: null },
  accountIds: [],
  disabledAccountIds: [{ display: "Disabled account", value: "disabled001", disableMessage: "Your annual membership expired." }],
};

const deferred = <T,>() => {
  let resolve!: (value: T) => void;
  const promise = new Promise<T>((onResolve) => { resolve = onResolve; });
  return { promise, resolve };
};

const feature = (id: string): FeatureSettingVm => ({
  id,
  key: FeatureKey.EnabledFieldCaveName,
  isEnabled: true,
  isDefault: false,
});

const SessionProbe = () => {
  const { currentAccountId, accountIds, isAuthenticated, isInitialized, isLoading, initializedError,
    permissions, refreshSession, pendingInvitationCount } = useContext(AppContext);
  const location = useLocation();
  return (
    <>
      <span data-testid="account">{currentAccountId ?? "none"}</span>
      <span data-testid="available">{accountIds.map((account) => account.value).join(",")}</span>
      <span data-testid="authenticated">{String(isAuthenticated)}</span>
      <span data-testid="initialized">{String(isInitialized)}</span>
      <span data-testid="loading">{String(isLoading)}</span>
      <span data-testid="error">{initializedError?.message ?? ""}</span>
      <span data-testid="pathname">{location.pathname}</span>
      <span data-testid="features">{permissions.visibleFields.map((field) => field.id).join(",")}</span>
      <span data-testid="invitation-count">{pendingInvitationCount}</span>
      <button type="button" onClick={() => void refreshSession("alternate1")}>
        Select another account
      </button>
      <button type="button" onClick={() => AuthenticationService.HandleUnauthorized("invalid-account")}>
        Simulate unauthorized response
      </button>
    </>
  );
};

const AccountRoutes = () => {
  const { isInitialized } = useContext(AppContext);
  if (!isInitialized) return null;

  return (
    <Routes>
      <Route path="/" element={<AppRederect />} />
      <Route element={<ProtectedRoutesComponent requiresAccount />}>
        <Route path="/caves/:caveId" element={<span>Protected cave</span>} />
      </Route>
      <Route
        path={ClientRoutes.invitationList.path}
        element={<span>Account selection</span>}
      />
    </Routes>
  );
};

describe("AppProvider disabled account recovery", () => {
  beforeEach(() => {
    AuthenticationService.ClearRuntimeSession();
    AuthenticationService.ResetUnauthorizedHandling();
    window.localStorage.clear();
    window.sessionStorage.clear();
    jest.spyOn(AccountService, "GetFeatureSettings").mockResolvedValue([]);
    jest.spyOn(UserService, "GetPendingInvitations").mockResolvedValue([]);
    jest.spyOn(message, "warning").mockImplementation(() => undefined as never);
  });

  afterEach(() => {
    jest.restoreAllMocks();
    AuthenticationService.ClearRuntimeSession();
    AuthenticationService.ResetUnauthorizedHandling();
  });

  it("does not switch to another account after the selected membership is disabled", async () => {
    let requestAccountDuringRefresh: string | null = "not-cleared";
    const initialize = jest.spyOn(AppService, "InitializeApp")
      .mockResolvedValueOnce(active("disabled001"))
      .mockImplementationOnce(async () => {
        requestAccountDuringRefresh = RequestRuntimeState.getCurrentAccountId();
        return availableWithDisabled;
      });

    render(
      <MemoryRouter initialEntries={["/caves/old-cave"]}>
        <AppProvider><SessionProbe /></AppProvider>
      </MemoryRouter>
    );
    await waitFor(() => expect(screen.getByTestId("account")).toHaveTextContent("disabled001"));

    fireEvent.click(screen.getByRole("button", { name: "Simulate unauthorized response" }));
    await waitFor(() => expect(screen.getByTestId("loading")).toHaveTextContent("false"));
    expect(screen.getByTestId("account")).toHaveTextContent("none");
    expect(screen.getByTestId("authenticated")).toHaveTextContent("true");
    expect(screen.getByTestId("available")).toHaveTextContent("active0001");
    expect(screen.getByTestId("pathname")).toHaveTextContent("/");
    expect(screen.getByTestId("features")).toBeEmptyDOMElement();
    expect(requestAccountDuringRefresh).toBeNull();
    expect(RequestRuntimeState.getCurrentAccountId()).toBeNull();
    expect(AuthenticationService.GetStoredAccountId("testuser01")).toBe("disabled001");
    expect(initialize).toHaveBeenLastCalledWith(undefined);
  });

  it("finishes account recovery when the protected-route redirect occurs during bootstrap", async () => {
    const recovery = deferred<AppInitializeVm>();
    const initialize = jest.spyOn(AppService, "InitializeApp")
      .mockResolvedValueOnce(active("disabled001"))
      .mockImplementationOnce(() => recovery.promise);

    render(
      <MemoryRouter initialEntries={["/caves/old-cave"]}>
        <AppProvider>
          <SessionProbe />
          <AccountRoutes />
        </AppProvider>
      </MemoryRouter>
    );

    await waitFor(() => expect(screen.getByText("Protected cave")).toBeInTheDocument());
    fireEvent.click(screen.getByRole("button", { name: "Simulate unauthorized response" }));

    await waitFor(() => expect(initialize).toHaveBeenCalledTimes(2));
    await waitFor(() =>
      expect(screen.getByTestId("pathname")).toHaveTextContent(ClientRoutes.invitationList.path)
    );
    expect(screen.getByTestId("loading")).toHaveTextContent("true");

    await act(async () => {
      recovery.resolve(availableWithDisabled);
      await recovery.promise;
    });

    await waitFor(() => expect(screen.getByTestId("loading")).toHaveTextContent("false"));
    expect(screen.getByTestId("authenticated")).toHaveTextContent("true");
    expect(screen.getByTestId("initialized")).toHaveTextContent("true");
    expect(screen.getByTestId("account")).toHaveTextContent("none");
    expect(screen.getByTestId("available")).toHaveTextContent("active0001");
    expect(screen.getByText("Account selection")).toBeInTheDocument();
  });

  it("remains accountless on reload until the user selects an available account", async () => {
    AuthenticationService.SetStoredAccountId("testuser01", "disabled001");
    const initialize = jest.spyOn(AppService, "InitializeApp")
      .mockResolvedValueOnce(availableWithDisabled)
      .mockResolvedValueOnce(active("alternate1"));
    const featureSettings = jest.spyOn(AccountService, "GetFeatureSettings");

    render(
      <MemoryRouter initialEntries={["/"]}>
        <AppProvider><SessionProbe /></AppProvider>
      </MemoryRouter>
    );
    await waitFor(() => expect(screen.getByTestId("loading")).toHaveTextContent("false"));
    expect(screen.getByTestId("account")).toHaveTextContent("none");
    expect(screen.getByTestId("authenticated")).toHaveTextContent("true");
    expect(screen.getByTestId("available")).toHaveTextContent("active0001");
    expect(featureSettings).not.toHaveBeenCalled();
    expect(initialize).toHaveBeenCalledTimes(1);

    fireEvent.click(screen.getByRole("button", { name: "Select another account" }));
    await waitFor(() => expect(screen.getByTestId("account")).toHaveTextContent("alternate1"));
    expect(AuthenticationService.GetStoredAccountId("testuser01")).toBe("alternate1");
    expect(initialize).toHaveBeenLastCalledWith("alternate1");
  });

  it("keeps normal default account selection when no disabled account was selected", async () => {
    jest.spyOn(AppService, "InitializeApp").mockResolvedValue(availableWithDisabled);
    render(
      <MemoryRouter initialEntries={["/"]}>
        <AppProvider><SessionProbe /></AppProvider>
      </MemoryRouter>
    );
    await waitFor(() => expect(screen.getByTestId("loading")).toHaveTextContent("false"));
    expect(screen.getByTestId("account")).toHaveTextContent("active0001");
    expect(AuthenticationService.GetStoredAccountId("testuser01")).toBe("active0001");
  });

  it("keeps an authenticated user with no remaining account on the accountless route", async () => {
    const initialize = jest.spyOn(AppService, "InitializeApp")
      .mockResolvedValueOnce(active("disabled001"))
      .mockResolvedValueOnce(NoAccount);

    render(
      <MemoryRouter initialEntries={["/caves/old-cave"]}>
        <AppProvider><SessionProbe /></AppProvider>
      </MemoryRouter>
    );
    await waitFor(() => expect(screen.getByTestId("account")).toHaveTextContent("disabled001"));
    fireEvent.click(screen.getByRole("button", { name: "Simulate unauthorized response" }));
    await waitFor(() => expect(screen.getByTestId("account")).toHaveTextContent("none"));
    expect(screen.getByTestId("authenticated")).toHaveTextContent("true");
    expect(screen.getByTestId("pathname")).toHaveTextContent("/");
    expect(initialize).toHaveBeenLastCalledWith(undefined);
  });

  it("does not interrupt a different active account when another membership is disabled", async () => {
    AuthenticationService.SetStoredAccountId("testuser01", "active0001");
    const initialize = jest.spyOn(AppService, "InitializeApp")
      .mockResolvedValue(availableWithDisabled);

    render(
      <MemoryRouter initialEntries={["/caves"]}>
        <AppProvider><SessionProbe /></AppProvider>
      </MemoryRouter>
    );
    await waitFor(() => expect(screen.getByTestId("loading")).toHaveTextContent("false"));
    expect(screen.getByTestId("authenticated")).toHaveTextContent("true");
    expect(screen.getByTestId("account")).toHaveTextContent("active0001");
    expect(screen.getByTestId("pathname")).toHaveTextContent("/caves");
    expect(initialize).toHaveBeenCalledTimes(1);
    expect(message.warning).not.toHaveBeenCalled();
  });

  it("keeps a valid accountless session when invitation lookup fails", async () => {
    jest.spyOn(AppService, "InitializeApp")
      .mockResolvedValueOnce(active("disabled001"))
      .mockResolvedValueOnce(availableWithDisabled);
    jest.spyOn(UserService, "GetPendingInvitations")
      .mockResolvedValueOnce([])
      .mockRejectedValueOnce(new Error("Invitations unavailable"));

    render(
      <MemoryRouter initialEntries={["/caves/old-cave"]}>
        <AppProvider><SessionProbe /></AppProvider>
      </MemoryRouter>
    );
    await waitFor(() => expect(screen.getByTestId("loading")).toHaveTextContent("false"));
    fireEvent.click(screen.getByRole("button", { name: "Simulate unauthorized response" }));

    await waitFor(() => expect(screen.getByTestId("account")).toHaveTextContent("none"));
    await waitFor(() => expect(screen.getByTestId("loading")).toHaveTextContent("false"));
    expect(screen.getByTestId("authenticated")).toHaveTextContent("true");
    expect(screen.getByTestId("pathname")).toHaveTextContent("/");
    expect(message.warning).toHaveBeenCalledWith(expect.stringContaining("could not be loaded"));
  });

  it("does not turn a transient bootstrap outage into a forced login", async () => {
    jest.spyOn(AppService, "InitializeApp")
      .mockResolvedValueOnce(active("disabled001"))
      .mockRejectedValueOnce({
        statusCode: 503,
        errorCode: ApiExceptionType.InternalServerError,
        message: "Backend unavailable",
      });

    render(
      <MemoryRouter initialEntries={["/caves/old-cave"]}>
        <AppProvider><SessionProbe /></AppProvider>
      </MemoryRouter>
    );
    await waitFor(() => expect(screen.getByTestId("loading")).toHaveTextContent("false"));
    fireEvent.click(screen.getByRole("button", { name: "Simulate unauthorized response" }));

    await waitFor(() => expect(screen.getByTestId("initialized")).toHaveTextContent("false"));
    expect(screen.getByTestId("loading")).toHaveTextContent("false");
    expect(screen.getByTestId("error")).toHaveTextContent("Reload to try again");
    expect(screen.getByTestId("account")).toHaveTextContent("none");
    expect(screen.getByTestId("pathname")).toHaveTextContent("/caves/old-cave");
  });

  it("ignores a stale bootstrap after a newer account selection", async () => {
    const first = deferred<AppInitializeVm>();
    const initialize = jest.spyOn(AppService, "InitializeApp")
      .mockImplementationOnce(() => first.promise)
      .mockResolvedValueOnce({
        ...active("alternate1"),
        antiforgeryRequestToken: "new-session-token",
      });

    render(
      <MemoryRouter initialEntries={["/caves"]}>
        <AppProvider><SessionProbe /></AppProvider>
      </MemoryRouter>
    );
    await waitFor(() => expect(initialize).toHaveBeenCalledTimes(1));

    fireEvent.click(screen.getByRole("button", { name: "Select another account" }));
    await waitFor(() => expect(screen.getByTestId("account")).toHaveTextContent("alternate1"));
    expect(RequestRuntimeState.getAntiforgeryRequestToken()).toBe("new-session-token");

    const stale = { ...active("stale00001"), antiforgeryRequestToken: "stale-session-token" };
    await act(async () => {
      first.resolve(stale);
      await first.promise;
    });

    expect(screen.getByTestId("account")).toHaveTextContent("alternate1");
    expect(screen.getByTestId("initialized")).toHaveTextContent("true");
    expect(RequestRuntimeState.getAntiforgeryRequestToken()).toBe("new-session-token");
  });

  it("ignores account settings from a previous session after recovery", async () => {
    const oldSettings = deferred<FeatureSettingVm[]>();
    jest.spyOn(AppService, "InitializeApp")
      .mockResolvedValueOnce(active("disabled001"))
      .mockResolvedValueOnce(availableWithDisabled);
    jest.spyOn(AccountService, "GetFeatureSettings")
      .mockImplementationOnce(() => oldSettings.promise);

    render(
      <MemoryRouter initialEntries={["/caves"]}>
        <AppProvider><SessionProbe /></AppProvider>
      </MemoryRouter>
    );
    await waitFor(() => expect(screen.getByTestId("account")).toHaveTextContent("disabled001"));

    fireEvent.click(screen.getByRole("button", { name: "Simulate unauthorized response" }));
    await waitFor(() => expect(screen.getByTestId("account"))
      .toHaveTextContent("none"));
    expect(screen.getByTestId("features")).toBeEmptyDOMElement();

    await act(async () => {
      oldSettings.resolve([feature("stale-account-setting")]);
      await oldSettings.promise;
    });

    expect(screen.getByTestId("account")).toHaveTextContent("none");
    expect(screen.getByTestId("features")).toBeEmptyDOMElement();
  });
});
