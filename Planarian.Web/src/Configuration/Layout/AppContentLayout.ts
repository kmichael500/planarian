import { CSSProperties } from "react";
import { matchPath } from "react-router-dom";
import { ClientRoutes } from "../Routing/ClientRoutes.generated";

export const defaultAppContentStyle: CSSProperties = {
  margin: "16px",
};

const fullHeightContentStyle: CSSProperties = {
  ...defaultAppContentStyle,
  display: "flex",
  overflow: "hidden",
};

const caveDetailContentStyle: CSSProperties = {
  margin: 0,
  padding: defaultAppContentStyle.margin,
  background: "var(--background-color)",
};

const importContentStyle: CSSProperties = {
  ...defaultAppContentStyle,
  overflow: "hidden",
  display: "flex",
  flexDirection: "column",
};
const invitationContentStyle: CSSProperties = {
  margin: 0,
  display: "flex",
  background: "var(--background-color)",
};

const matches = (path: string, pathname: string) =>
  matchPath({ path, end: true }, pathname) != null;

export const getAppContentStyle = (pathname: string): CSSProperties => {
  if (matches("/map", pathname)) return {};
  if (matches("/caves", pathname)) return fullHeightContentStyle;
  if (matches("/caves/add", pathname)) return defaultAppContentStyle;
  if (matches("/caves/:caveId", pathname)) return caveDetailContentStyle;
  if (matches("/account/users", pathname)) return fullHeightContentStyle;
  if (matches("/account/import", pathname)) return importContentStyle;
  if (matches(ClientRoutes.invitation.path, pathname)) {
    return invitationContentStyle;
  }

  return defaultAppContentStyle;
};
