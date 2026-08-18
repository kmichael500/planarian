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
  it("collapses children by default below the xl breakpoint", () => {
    render(
      <AppContextWithPermissions>
        <PlanarianButton
          icon={<span aria-hidden="true">icon</span>}
          aria-label="Approve and publish"
        >
          Approve and publish
        </PlanarianButton>
      </AppContextWithPermissions>
    );

    expect(screen.getByRole("button", { name: "Approve and publish" }))
      .not.toHaveTextContent("Approve and publish");
  });

  it("allows callers to keep children visible regardless of the breakpoint", () => {
    render(
      <AppContextWithPermissions>
        <PlanarianButton icon={undefined} alwaysShowChildren>
          Approve and publish
        </PlanarianButton>
      </AppContextWithPermissions>
    );

    expect(screen.getByRole("button", { name: "Approve and publish" })).toBeVisible();
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
