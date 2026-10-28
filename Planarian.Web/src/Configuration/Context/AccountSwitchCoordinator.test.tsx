import React, { useCallback, useState } from "react";
import { act, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter, useLocation } from "react-router-dom";
import { message } from "antd";
import { AppContext } from "./AppContext";
import { AccountSwitchCoordinator } from "./AccountSwitchCoordinator";
import { ACCOUNT_SWITCH_EVENT } from "../../Modules/Authentication/Services/AuthenticationService";

const deferred = () => {
  let resolve!: () => void;
  let reject!: (error: Error) => void;
  const promise = new Promise<void>((res, rej) => {
    resolve = res;
    reject = rej;
  });
  return { promise, resolve, reject };
};

const emitSwitch = (accountId: string, redirectPath = "/caves") => {
  window.dispatchEvent(
    new CustomEvent(ACCOUNT_SWITCH_EVENT, {
      detail: { accountId, redirectPath },
    })
  );
};

const LocationProbe = ({
  accountId,
  onMount,
}: {
  accountId: string;
  onMount?: (accountId: string) => void;
}) => {
  const location = useLocation();
  const [mountedAccountId] = useState(() => {
    onMount?.(accountId);
    return accountId;
  });

  return (
    <div data-mounted-account={mountedAccountId}>
      {accountId}:{location.pathname}
    </div>
  );
};

const Harness = ({
  refresh,
  onMount,
}: {
  refresh: (accountId: string) => Promise<void>;
  onMount?: (accountId: string) => void;
}) => {
  const [accountId, setAccountId] = useState("account-a");
  const refreshSession = useCallback(
    async (nextAccountId?: string | null) => {
      if (!nextAccountId) return;
      await refresh(nextAccountId);
      setAccountId(nextAccountId);
    },
    [refresh]
  );
  const value = {
    currentAccountId: accountId,
    refreshSession,
  } as unknown as React.ContextType<typeof AppContext>;

  return (
    <MemoryRouter initialEntries={["/caves/cave-a"]}>
      <AppContext.Provider value={value}>
        <AccountSwitchCoordinator>
          <LocationProbe accountId={accountId} onMount={onMount} />
        </AccountSwitchCoordinator>
      </AppContext.Provider>
    </MemoryRouter>
  );
};

describe("AccountSwitchCoordinator", () => {
  test("hides account-specific UI until a switch is fully initialized", async () => {
    const pending = deferred();
    const refresh = jest.fn(() => pending.promise);
    render(<Harness refresh={refresh} />);

    act(() => emitSwitch("account-b", "/caves"));
    expect(screen.getByRole("status", { name: "Switching accounts" })).toBeInTheDocument();

    pending.resolve();

    expect(await screen.findByText("account-b:/caves")).toBeInTheDocument();
    expect(refresh).toHaveBeenCalledWith("account-b");
  });

  test("serializes rapid A to B to A switches and only exposes the final account", async () => {
    const accountB = deferred();
    const accountA = deferred();
    const refresh = jest.fn((accountId: string) =>
      accountId === "account-b" ? accountB.promise : accountA.promise
    );
    render(<Harness refresh={refresh} />);

    act(() => emitSwitch("account-b", "/caves/b"));
    await waitFor(() => expect(refresh).toHaveBeenCalledWith("account-b"));

    act(() => emitSwitch("account-a", "/caves/a"));
    accountB.resolve();

    await waitFor(() => expect(refresh).toHaveBeenCalledWith("account-a"));
    expect(screen.getByRole("status", { name: "Switching accounts" })).toBeInTheDocument();
    expect(screen.queryByText(/^account-b:/)).not.toBeInTheDocument();

    accountA.resolve();

    expect(await screen.findByText("account-a:/caves/a")).toBeInTheDocument();
  });

  test("remounts account-specific children after a successful switch", async () => {
    const onMount = jest.fn();
    render(<Harness refresh={async () => undefined} onMount={onMount} />);

    expect(screen.getByText("account-a:/caves/cave-a")).toHaveAttribute(
      "data-mounted-account",
      "account-a"
    );

    act(() => emitSwitch("account-b", "/caves/b"));

    const switched = await screen.findByText("account-b:/caves/b");
    expect(switched).toHaveAttribute("data-mounted-account", "account-b");
    expect(onMount.mock.calls.map(([accountId]) => accountId)).toEqual([
      "account-a",
      "account-b",
    ]);
  });

  test("keeps the prior account visible and reports a failed switch", async () => {
    const error = jest.spyOn(message, "error").mockImplementation(() => undefined as never);
    const refresh = jest.fn(async () => {
      throw new Error("switch failed");
    });
    render(<Harness refresh={refresh} />);

    act(() => emitSwitch("account-b", "/caves/b"));

    expect(await screen.findByText("account-a:/caves/cave-a")).toBeInTheDocument();
    expect(error).toHaveBeenCalledWith(
      "The account could not be switched. Your previous account is still active."
    );
  });
});
