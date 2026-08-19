import { render, screen } from "@testing-library/react";
import React, { useContext } from "react";
import { MemoryRouter } from "react-router-dom";
import { AppContext } from "../Context/AppContext";
import { PermissionKey } from "../../Modules/Authentication/Models/PermissionKey";
import { useSideBarMenuItems } from "./SidebarMenuItems";

const MenuLabels = () => <>{useSideBarMenuItems().map(item =>
  item.label ? <span key={item.key ?? String(item.label)}>{item.label}</span> : null)}</>;

const ContextOverride = ({ manager }: { manager: boolean }) => {
  const defaults = useContext(AppContext);
  return <AppContext.Provider value={{ ...defaults, currentAccountId: "account",
    hasPermission: permission => manager && permission === PermissionKey.Manager }}>
    <MenuLabels />
  </AppContext.Provider>;
};

const renderMenu = (manager: boolean) => render(
  <MemoryRouter><ContextOverride manager={manager} /></MemoryRouter>
);

it("labels Cave Changes as Submissions for users who cannot review", () => {
  renderMenu(false);
  expect(screen.getByText("Submissions")).toBeInTheDocument();
  expect(screen.queryByText("Cave Changes")).not.toBeInTheDocument();
});

it("keeps Cave Changes for managers who can review", () => {
  renderMenu(true);
  expect(screen.getByText("Cave Changes")).toBeInTheDocument();
  expect(screen.queryByText("Submissions")).not.toBeInTheDocument();
});
