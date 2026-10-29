import React, { useContext } from "react";
import { act, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { AccountService } from "../../Modules/Account/Services/AccountService";
import { AuthenticationService } from "../../Modules/Authentication/Services/AuthenticationService";
import { UserService } from "../../Modules/User/UserService";
import { AppInitializeVm, AppService } from "../../Shared/Services/AppService";
import { AppContext, AppProvider } from "./AppContext";

const session = (accountId: string): AppInitializeVm => ({
  apiBaseUrl: "https://api.planarian.local",
  signalrBaseUrl: "https://api.planarian.local",
  supportName: "Support",
  supportEmail: "support@example.test",
  accountIds: [
    { value: "account-a", display: "Account A" },
    { value: "account-b", display: "Account B" },
  ],
  permissions: [],
  currentUser: {
    id: "user-1",
    fullName: "Test User",
    currentAccountId: accountId,
  },
  antiforgeryRequestToken: "token-" + accountId,
});

let latestContext: React.ContextType<typeof AppContext>;

const Probe = () => {
  latestContext = useContext(AppContext);
  return (
    <div>
      <span data-testid="account">{latestContext.currentAccountId ?? "none"}</span>
      <span data-testid="loading">{latestContext.isLoading ? "loading" : "ready"}</span>
    </div>
  );
};

describe("AppProvider account refresh atomicity", () => {
  beforeEach(() => {
    window.localStorage.clear();
    window.sessionStorage.clear();
    AuthenticationService.ClearRuntimeSession();
    jest.restoreAllMocks();
  });

  const renderInitializedAccountA = async () => {
    jest.spyOn(AppService, "InitializeApp").mockResolvedValue(session("account-a"));
    jest.spyOn(AccountService, "GetFeatureSettings").mockResolvedValue([]);
    jest.spyOn(UserService, "GetPendingInvitations").mockResolvedValue([]);

    render(
      <MemoryRouter initialEntries={["/caves"]}>
        <AppProvider>
          <Probe />
        </AppProvider>
      </MemoryRouter>
    );

    await waitFor(() => expect(screen.getByTestId("account")).toHaveTextContent("account-a"));
    await waitFor(() => expect(screen.getByTestId("loading")).toHaveTextContent("ready"));
    expect(AuthenticationService.GetAccountId()).toBe("account-a");
    expect(AuthenticationService.GetStoredAccountId("user-1")).toBe("account-a");
  };

  test.each(["feature-settings", "pending-invitations"] as const)(
    "a failed %s load cannot partially commit a target account",
    async (failurePoint) => {
      await renderInitializedAccountA();

      (AppService.InitializeApp as jest.Mock).mockResolvedValue(session("account-b"));
      if (failurePoint === "feature-settings") {
        (AccountService.GetFeatureSettings as jest.Mock).mockRejectedValueOnce(
          new Error("feature load failed")
        );
      } else {
        (AccountService.GetFeatureSettings as jest.Mock).mockResolvedValueOnce([]);
        (UserService.GetPendingInvitations as jest.Mock).mockRejectedValueOnce(
          new Error("invitation load failed")
        );
      }

      await act(async () => {
        await expect(latestContext.refreshSession("account-b")).rejects.toThrow();
      });

      expect(screen.getByTestId("account")).toHaveTextContent("account-a");
      expect(AuthenticationService.GetAccountId()).toBe("account-a");
      expect(AuthenticationService.GetStoredAccountId("user-1")).toBe("account-a");
    }
  );

  test("commits the target account only after its dependent state is ready", async () => {
    await renderInitializedAccountA();

    (AppService.InitializeApp as jest.Mock).mockResolvedValue(session("account-b"));
    (AccountService.GetFeatureSettings as jest.Mock).mockResolvedValueOnce([]);
    (UserService.GetPendingInvitations as jest.Mock).mockResolvedValueOnce([]);

    await act(async () => {
      await latestContext.refreshSession("account-b");
    });

    expect(screen.getByTestId("account")).toHaveTextContent("account-b");
    expect(AuthenticationService.GetAccountId()).toBe("account-b");
    expect(AuthenticationService.GetStoredAccountId("user-1")).toBe("account-b");
  });
});
