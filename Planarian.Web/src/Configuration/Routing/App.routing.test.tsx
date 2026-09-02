import React, { ReactElement, ReactNode } from "react";
import { PermissionKey } from "../../Modules/Authentication/Models/PermissionKey";
import { ProtectedRoutesComponent } from "../../Modules/Authentication/Components/ProtectedRoutesComponent";
import { AppRouting } from "./App.routing";

jest.mock("../../Modules/Caves/Pages/CavePage", () => ({
  CavePage: () => null,
}));

jest.mock("../../Modules/Map/Pages/MapPage", () => ({
  MapPage: () => null,
}));

jest.mock("chartjs-adapter-date-fns", () => ({}));

jest.mock("react-map-gl/maplibre", () => ({}));

const routePolicies = () => {
  const policies = new Map<string, (PermissionKey | undefined)[]>();

  const visit = (
    node: ReactNode,
    inherited: (PermissionKey | undefined)[] = []
  ) => {
    if (!React.isValidElement(node)) return;
    const route = node as ReactElement<any>;
    const guard = route.props.element;
    const permissions =
      React.isValidElement<{ permissionKey?: PermissionKey }>(guard) &&
      guard.type === ProtectedRoutesComponent
        ? [...inherited, guard.props.permissionKey]
        : inherited;

    if (typeof route.props.path === "string") {
      policies.set(route.props.path, permissions);
    }
    React.Children.forEach(route.props.children, (child) =>
      visit(child, permissions)
    );
  };

  visit(AppRouting({}));
  return policies;
};

test("keeps platform administration scoped to Planarian Settings", () => {
  const policies = routePolicies();

  expect(policies.get("/planarian-settings")).toContain(
    PermissionKey.PlanarianAdmin
  );
  expect(policies.get("/projects")).not.toContain(PermissionKey.PlanarianAdmin);
  expect(policies.get("/projects/:projectId")).not.toContain(
    PermissionKey.PlanarianAdmin
  );
  expect(policies.get("/projects/:projectId/trip/:tripId")).not.toContain(
    PermissionKey.PlanarianAdmin
  );
});
