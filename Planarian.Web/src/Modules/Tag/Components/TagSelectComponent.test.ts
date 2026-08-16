import { TagType } from "../Models/TagType";
import { resolveTagSelectMode } from "./TagSelectComponent";

it("enables Ant tags mode only through People custom-tag policy", () => {
  expect(resolveTagSelectMode(TagType.People, true)).toBe("tags");
  expect(resolveTagSelectMode(TagType.Biology, false, "multiple")).toBe("multiple");
  expect(() => resolveTagSelectMode(TagType.Biology, true)).toThrow(
    "only for People"
  );
  expect(() => resolveTagSelectMode(TagType.Archeology, true)).toThrow(
    "only for People"
  );
});
