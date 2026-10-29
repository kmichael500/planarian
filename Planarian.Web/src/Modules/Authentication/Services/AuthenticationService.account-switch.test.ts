import { AuthenticationService, ACCOUNT_SWITCH_EVENT } from "./AuthenticationService";

describe("AuthenticationService account switching", () => {
  beforeEach(() => {
    window.localStorage.clear();
    window.sessionStorage.clear();
    AuthenticationService.ClearRuntimeSession();
    jest.restoreAllMocks();
  });

  test("requests an in-app account switch without committing the stored account first", () => {
    AuthenticationService.SyncSession({ id: "user-1", currentAccountId: "account-a" });
    const listener = jest.fn();
    window.addEventListener(ACCOUNT_SWITCH_EVENT, listener);

    AuthenticationService.SwitchAccount("account-b", "/caves");

    expect(listener).toHaveBeenCalledTimes(1);
    expect((listener.mock.calls[0][0] as CustomEvent).detail).toEqual({
      accountId: "account-b",
      redirectPath: "/caves",
    });
    expect(AuthenticationService.GetStoredAccountId("user-1")).toBe("account-a");

    window.removeEventListener(ACCOUNT_SWITCH_EVENT, listener);
  });

  test("uses the current route as the default switch destination", () => {
    window.history.replaceState({}, "", "/caves/cave123?tab=files#map");
    const listener = jest.fn();
    window.addEventListener(ACCOUNT_SWITCH_EVENT, listener);

    AuthenticationService.SwitchAccount("account-b");

    expect((listener.mock.calls[0][0] as CustomEvent).detail.redirectPath).toBe(
      "/caves/cave123?tab=files#map"
    );
    window.removeEventListener(ACCOUNT_SWITCH_EVENT, listener);
  });

  test("does not switch when an unsaved-change listener blocks and the user cancels", () => {
    const blockUnload = (event: Event) => event.preventDefault();
    window.addEventListener("beforeunload", blockUnload);
    jest.spyOn(window, "confirm").mockReturnValue(false);
    const listener = jest.fn();
    window.addEventListener(ACCOUNT_SWITCH_EVENT, listener);

    AuthenticationService.SwitchAccount("account-b", "/caves");

    expect(window.confirm).toHaveBeenCalledWith(
      "Discard unsaved changes and switch accounts?"
    );
    expect(listener).not.toHaveBeenCalled();

    window.removeEventListener("beforeunload", blockUnload);
    window.removeEventListener(ACCOUNT_SWITCH_EVENT, listener);
  });

  test("allows an explicitly confirmed switch away from dirty state", () => {
    const blockUnload = (event: Event) => event.preventDefault();
    window.addEventListener("beforeunload", blockUnload);
    jest.spyOn(window, "confirm").mockReturnValue(true);
    const listener = jest.fn();
    window.addEventListener(ACCOUNT_SWITCH_EVENT, listener);

    AuthenticationService.SwitchAccount("account-b", "/caves");

    expect(listener).toHaveBeenCalledTimes(1);

    window.removeEventListener("beforeunload", blockUnload);
    window.removeEventListener(ACCOUNT_SWITCH_EVENT, listener);
  });
});
