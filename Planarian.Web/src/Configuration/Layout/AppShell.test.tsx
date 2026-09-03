import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import React from "react";
import { MemoryRouter } from "react-router-dom";
import { AppShell } from "../../App";
import { AppContext } from "../Context/AppContext";

const mockRenderedLayouts: Array<{
  pathname: string;
  margin?: string | number;
  padding?: string | number;
  display?: string;
  overflow?: string;
}> = [];

jest.mock("../Header/HeaderComponent", () => ({
  HeaderComponent: () => <div>Header</div>,
}));

jest.mock("../Sidebar/SidebarComponent", () => ({
  SideBarComponent: () => <div>Sidebar</div>,
}));

jest.mock("antd", () => {
  const actual = jest.requireActual<typeof import("antd")>("antd");
  const React = jest.requireActual<typeof import("react")>("react");
  const { useLocation } =
    jest.requireActual<typeof import("react-router-dom")>("react-router-dom");

  const MockContent = ({
    children,
    style,
    className,
  }: React.PropsWithChildren<{
    style?: React.CSSProperties;
    className?: string;
  }>) => {
    const location = useLocation();
    mockRenderedLayouts.push({
      pathname: location.pathname,
      margin: style?.margin,
      padding: style?.padding,
      display: style?.display,
      overflow: style?.overflow,
    });

    return <main className={className}>{children}</main>;
  };

  const MockLayout = ({ children }: React.PropsWithChildren) => (
    <div>{children}</div>
  );
  Object.assign(MockLayout, { Content: MockContent });

  return { ...actual, Layout: MockLayout };
});

jest.mock("../Routing/App.routing", () => {
  const { useLocation, useNavigate } =
    jest.requireActual<typeof import("react-router-dom")>("react-router-dom");

  return {
    AppRouting: () => {
      const location = useLocation();
      const navigate = useNavigate();

      return location.pathname === "/caves" ? (
        <button type="button" onClick={() => navigate("/caves/cave-123")}>
          Open cave
        </button>
      ) : (
        <div>Cave detail</div>
      );
    },
  };
});

describe("AppShell route layout", () => {
  beforeEach(() => {
    mockRenderedLayouts.length = 0;
  });

  test("renders the cave detail layout on the first render after navigation", () => {
    const contextValue = {
      isInitialized: true,
      isLoading: false,
      initializedError: null,
      logout: jest.fn(),
    } as unknown as React.ContextType<typeof AppContext>;

    render(
      <MemoryRouter initialEntries={["/caves"]}>
        <AppContext.Provider value={contextValue}>
          <AppShell />
        </AppContext.Provider>
      </MemoryRouter>
    );

    expect(mockRenderedLayouts[0]).toMatchObject({
      pathname: "/caves",
      display: "flex",
      overflow: "hidden",
    });

    userEvent.click(screen.getByRole("button", { name: "Open cave" }));

    const firstCaveRender = mockRenderedLayouts.find(
      (layout) => layout.pathname === "/caves/cave-123"
    );

    expect(firstCaveRender).toEqual({
      pathname: "/caves/cave-123",
      margin: 0,
      padding: "16px",
      display: undefined,
      overflow: undefined,
    });
  });
});
