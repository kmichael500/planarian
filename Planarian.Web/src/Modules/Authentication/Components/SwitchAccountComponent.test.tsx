import { fireEvent, render, screen } from "@testing-library/react";
import React, { useContext } from "react";
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

it("shows revoked accounts but does not allow switching to them", () => {
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
          revokedAccountIds: [
            {
              display: "Revoked account",
              value: "revoked001",
            },
          ],
          switchAccount,
        }}
      >
        {children}
      </AppContext.Provider>
    );
  };

  render(
    <SwitchAccountComponent isVisible handleCancel={() => {}} />,
    { wrapper: Wrapper }
  );

  const revokedAccount = screen.getByText("Revoked account").closest(".ant-list-item");
  expect(revokedAccount).toHaveAttribute("aria-disabled", "true");
  expect(screen.getByText("Access revoked")).toBeInTheDocument();

  fireEvent.click(revokedAccount!);
  expect(switchAccount).not.toHaveBeenCalled();

  fireEvent.click(screen.getByText("Other active account"));
  expect(switchAccount).toHaveBeenCalledWith("active0001");
});
