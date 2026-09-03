import { getAppContentStyle } from "./AppContentLayout";

describe("getAppContentStyle", () => {
  test("selects full-height layout for cave search", () => {
    expect(getAppContentStyle("/caves")).toMatchObject({
      margin: "16px",
      display: "flex",
      overflow: "hidden",
    });
  });

  test("selects the cave detail layout for an exact cave route", () => {
    expect(getAppContentStyle("/caves/cave-123")).toEqual({
      margin: 0,
      padding: "16px",
      background: "var(--background-color)",
    });
  });

  test("does not leak cave detail layout into edit or add routes", () => {
    expect(getAppContentStyle("/caves/cave-123/edit")).toEqual({
      margin: "16px",
    });
    expect(getAppContentStyle("/caves/add")).toEqual({ margin: "16px" });
  });
  test("preserves the other route-specific layouts", () => {
    expect(getAppContentStyle("/map")).toEqual({});
    expect(getAppContentStyle("/account/users")).toMatchObject({
      display: "flex",
      overflow: "hidden",
    });
    expect(getAppContentStyle("/account/import")).toMatchObject({
      display: "flex",
      flexDirection: "column",
      overflow: "hidden",
    });
    expect(getAppContentStyle("/user/invitations/code-123")).toMatchObject({
      margin: 0,
      display: "flex",
      overflow: "visible",
    });
  });

  test("uses the default layout for ordinary routes", () => {
    expect(getAppContentStyle("/projects")).toEqual({ margin: "16px" });
  });
});
