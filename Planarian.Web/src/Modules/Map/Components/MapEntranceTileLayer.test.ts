import { formatEntranceDisplayLabel } from "./MapEntranceHelpers";

describe("formatEntranceDisplayLabel", () => {
  test("matches the normal cave-map entrance label format", () => {
    expect(formatEntranceDisplayLabel("Echo Cave", "Main Entrance", true)).toBe(
      "Echo Cave (Main Entrance) *"
    );
    expect(formatEntranceDisplayLabel("Echo Cave", null, false)).toBe(
      "Echo Cave"
    );
    expect(formatEntranceDisplayLabel("Echo Cave", "Echo Cave", false)).toBe(
      "Echo Cave"
    );
  });
});
