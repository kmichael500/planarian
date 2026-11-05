import { fireEvent, render, screen } from "@testing-library/react";
import React, { useContext } from "react";
import userEvent from "@testing-library/user-event";
import { AppContext } from "../../../Configuration/Context/AppContext";
import { SwitchAccountComponent } from "./SwitchAccountComponent";

jest.mock("../../../Shared/Components/Buttons/CancelButtonComponent", () => ({
  CancelButtonComponent: ({ onClick }: { onClick?: () => void }) => (
    <button type="button" onClick={onClick}>
      Cancel
    </button>
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

it("shows disabled accounts but does not allow switching to them", () => {
  const switchAccount = jest.fn();

  const Wrapper = ({ children }: { children: React.ReactNode }) => {
    const defaults = useContext(AppContext);
    return (
      <AppContext.Provider
        value={{
          ...defaults,
          currentAccountId: "current001",
          currentAccountName: "Current account",
          accountIds: [
            {
              display: "Current account",
              value: "current001",
            },
            {
              display: "Other active account",
              value: "active0001",
            },
          ],
          disabledAccountIds: [
            {
              display: "Disabled account",
              value: "disabled001",
              disableMessage: "Annual review completed.",
            },
          ],
          switchAccount,
        }}
      >
        {children}
      </AppContext.Provider>
    );
  };

  render(<SwitchAccountComponent isVisible handleCancel={() => {}} />, {
    wrapper: Wrapper,
  });

  const disabledAccount = screen
    .getByText("Disabled account")
    .closest(".ant-list-item");
  expect(disabledAccount).toHaveAttribute("aria-disabled", "true");
  expect(screen.getByText("Access disabled")).toBeInTheDocument();
  expect(screen.getByText("Annual review completed.")).toBeInTheDocument();

  fireEvent.click(disabledAccount!);
  expect(switchAccount).not.toHaveBeenCalled();

  const activeAccountButton = screen.getByRole("button", {
    name: "Other active account",
  });
  expect(activeAccountButton.tagName).toBe("BUTTON");
  activeAccountButton.focus();
  userEvent.keyboard("{Enter}");
  expect(switchAccount).toHaveBeenCalledWith("active0001", undefined);

  expect(
    screen.queryByRole("button", { name: "Disabled account" })
  ).not.toBeInTheDocument();
});

it("opens the selected account when switching from the accountless Invitations page", () => {
  const switchAccount = jest.fn();
  const Wrapper = ({ children }: { children: React.ReactNode }) => {
    const defaults = useContext(AppContext);
    return (
      <AppContext.Provider value={{
        ...defaults,
        currentAccountId: null,
        accountIds: [{ display: "Other active account", value: "active0001" }],
        disabledAccountIds: [{ display: "Disabled account", value: "disabled001" }],
        switchAccount,
      }}>
        {children}
      </AppContext.Provider>
    );
  };

  render(<SwitchAccountComponent isVisible handleCancel={() => {}} />, {
    wrapper: Wrapper,
  });
  fireEvent.click(screen.getByRole("button", { name: "Other active account" }));
  expect(switchAccount).toHaveBeenCalledWith("active0001", "/caves");
});
