import { render, screen } from "@testing-library/react";
import React, { useContext } from "react";
import { AppContext } from "../../../Configuration/Context/AppContext";
import { PlanarianButton } from "./PlanarianButtton";

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

describe("PlanarianButton", () => {
  it("keeps text actions visible when the caller did not request responsive collapsing", () => {
    render(
      <AppContextWithPermissions>
        <PlanarianButton icon={undefined}>Approve and publish</PlanarianButton>
      </AppContextWithPermissions>
    );

    expect(
      screen.getByRole("button", { name: "Approve and publish" })
    ).toBeVisible();
  });

  it("still allows callers to explicitly hide children", () => {
    render(
      <AppContextWithPermissions>
        <PlanarianButton
          icon={<span aria-hidden="true">icon</span>}
          neverShowChildren
          aria-label="Open map"
        >
          Map
        </PlanarianButton>
      </AppContextWithPermissions>
    );

    expect(screen.getByRole("button", { name: "Open map" })).not.toHaveTextContent("Map");
  });
});
