import { act, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import React from "react";
import { MemoryRouter, Route, Routes, useNavigate } from "react-router-dom";
import { AppContext } from "../../../Configuration/Context/AppContext";
import { AppService } from "../../../Shared/Services/AppService";
import { CaveVm } from "../Models/CaveVm";
import { CaveService } from "../Service/CaveService";
import { CavePage } from "./CavePage";

const mockCaveCommits: Array<{
  pathname: string;
  caveId?: string;
  isLoading: boolean;
}> = [];

jest.mock("../Components/CaveComponent", () => {
  const React = jest.requireActual<typeof import("react")>("react");
  const { useLocation } =
    jest.requireActual<typeof import("react-router-dom")>("react-router-dom");

  return {
    CaveComponent: ({
      cave,
      isLoading,
    }: {
      cave?: CaveVm;
      isLoading: boolean;
    }) => {
      const location = useLocation();

      // Record the first committed cave props for each pathname, before passive effects run.
      React.useLayoutEffect(() => {
        mockCaveCommits.push({
          pathname: location.pathname,
          caveId: cave?.id,
          isLoading,
        });
      }, [cave?.id, isLoading, location.pathname]);

      return (
        <div>
          <div>Loading: {String(isLoading)}</div>
          <div>{cave?.id ?? "No cave"}</div>
        </div>
      );
    },
  };
});

jest.mock("../Components/FavoriteCave", () => ({
  __esModule: true,
  default: () => null,
}));

jest.mock("../../../Shared/Components/Buttons/BackButtonComponent", () => ({
  BackButtonComponent: () => null,
}));

jest.mock("../../../Shared/Components/Buttons/PlanarianButtton", () => ({
  PlanarianButton: ({ children }: { children: React.ReactNode }) => (
    <>{children}</>
  ),
}));

jest.mock("antd", () => {
  const actual = jest.requireActual("antd");
  return {
    ...actual,
    Grid: { ...actual.Grid, useBreakpoint: () => ({ lg: true }) },
  };
});

const deferred = <T,>() => {
  let resolve!: (value: T) => void;
  const promise = new Promise<T>((res) => {
    resolve = res;
  });
  return { promise, resolve };
};

const cave = (id: string): CaveVm =>
  ({
    id,
    displayId: id.toUpperCase(),
    name: `Cave ${id}`,
  } as CaveVm);

const RouteControls = () => {
  const navigate = useNavigate();
  return (
    <button type="button" onClick={() => navigate("/caves/b")}>
      Open cave B
    </button>
  );
};

describe("CavePage route identity", () => {
  beforeEach(() => {
    mockCaveCommits.length = 0;
    jest.restoreAllMocks();
  });

  test("does not commit the previous cave when the route changes", async () => {
    const caveBRequest = deferred<CaveVm>();
    const getCaveSpy = jest
      .spyOn(CaveService, "GetCave")
      .mockResolvedValueOnce(cave("a"))
      .mockImplementationOnce(() => caveBRequest.promise);
    jest.spyOn(AppService, "HasCavePermission").mockResolvedValue(true);

    const contextValue = {
      setHeaderTitle: jest.fn(),
      setHeaderButtons: jest.fn(),
    } as unknown as React.ContextType<typeof AppContext>;

    render(
      <MemoryRouter initialEntries={["/caves/a"]}>
        <AppContext.Provider value={contextValue}>
          <Routes>
            <Route
              path="/caves/:caveId"
              element={
                <>
                  <CavePage />
                  <RouteControls />
                </>
              }
            />
          </Routes>
        </AppContext.Provider>
      </MemoryRouter>
    );

    expect(await screen.findByText("a")).toBeInTheDocument();
    expect(screen.getByText("Loading: false")).toBeInTheDocument();

    userEvent.click(screen.getByRole("button", { name: "Open cave B" }));

    await waitFor(() => expect(getCaveSpy).toHaveBeenLastCalledWith("b"));

    const caveBCommits = mockCaveCommits.filter(
      (commit) => commit.pathname === "/caves/b"
    );
    expect(caveBCommits[0]).toEqual({
      pathname: "/caves/b",
      caveId: undefined,
      isLoading: true,
    });
    expect(screen.queryByText("a")).not.toBeInTheDocument();
    expect(screen.getByText("No cave")).toBeInTheDocument();

    await act(async () => {
      caveBRequest.resolve(cave("b"));
    });

    expect(await screen.findByText("b")).toBeInTheDocument();
    expect(screen.getByText("Loading: false")).toBeInTheDocument();
  });
});
