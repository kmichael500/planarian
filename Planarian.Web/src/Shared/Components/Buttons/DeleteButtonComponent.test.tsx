import { render, screen } from "@testing-library/react";
import React, { useContext } from "react";
import { AppContext } from "../../../Configuration/Context/AppContext";
import { DeleteButtonComponent } from "./DeleteButtonComponent";

beforeAll(() => {
  Object.defineProperty(window, "matchMedia", {
    writable: true,
    value: () => ({
      matches: false,
      addListener: () => undefined,
      removeListener: () => undefined,
      addEventListener: () => undefined,
      removeEventListener: () => undefined,
      dispatchEvent: () => false,
    }),
  });
});

const AppContextWithPermissions: React.FC<{ children: React.ReactNode }> = ({
  children,
}) => {
  const defaults = useContext(AppContext);
  return (
    <AppContext.Provider value={{ ...defaults, hasPermission: () => true }}>
      {children}
    </AppContext.Provider>
  );
};

describe("DeleteButtonComponent", () => {
  it("keeps Popconfirm-only props off the underlying button", () => {
    render(
      <AppContextWithPermissions>
        <DeleteButtonComponent
          alwaysShowChildren
          title="Delete item?"
          onConfirm={() => undefined}
          okText="Yes"
          cancelText="No"
        >
          Remove
        </DeleteButtonComponent>
      </AppContextWithPermissions>
    );

    const button = screen.getByRole("button", { name: /Remove$/ });
    expect(button).not.toHaveAttribute("onconfirm");
    expect(button).not.toHaveAttribute("oktext");
    expect(button).not.toHaveAttribute("canceltext");
  });
});
