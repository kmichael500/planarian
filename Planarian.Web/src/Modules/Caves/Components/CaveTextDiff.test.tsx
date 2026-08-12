import { fireEvent, render, screen } from "@testing-library/react";
import { CaveTextDiff, computeCaveTextDiff } from "./CaveTextDiff";

beforeAll(() => {
  Object.defineProperty(window, "matchMedia", {
    writable: true,
    value: () => ({ matches: false, addListener: () => undefined, removeListener: () => undefined }),
  });
});

it.each([
  ["old word", "new word"],
  ["before", "before after"],
  ["before after", "before"],
  ["Hello, cave!", "Hello cave?"],
  ["one  two\nthree", "one two\nfour"],
  ["", "added"],
  ["removed", ""],
])("computes a bounded whitespace-aware prose diff", (previous, proposed) => {
  const result = computeCaveTextDiff(previous, proposed);
  expect(result).toBeDefined();
  expect(result!.filter(change => !change.removed).map(change => change.value).join("")).toBe(proposed);
  expect(result!.filter(change => !change.added).map(change => change.value).join("")).toBe(previous);
});

it("returns the abort signal from the pure wrapper", () => {
  expect(computeCaveTextDiff("old", "new", () => undefined)).toBeUndefined();
});

it("falls back visibly after an aborted diff while retaining both complete texts", () => {
  render(<CaveTextDiff name="aborted" previous="complete old text" proposed="complete new text" differ={() => undefined} />);
  expect(document.body).toHaveTextContent("Inline changes were too complex to display.");
  expect(document.body).toHaveTextContent("complete old text");
  expect(document.body).toHaveTextContent("complete new text");
});

it("defaults to Changes and exposes complete Previous and Proposed views", () => {
  render(<CaveTextDiff name="narrative-view" previous="complete old text" proposed="complete new text" />);
  expect(screen.getByText("− Removed")).toBeInTheDocument();
  fireEvent.click(screen.getByText("Previous"));
  expect(screen.getByText("complete old text")).toBeInTheDocument();
  fireEvent.click(screen.getByText("Proposed"));
  expect(screen.getByText("complete new text")).toBeInTheDocument();
  expect(document.querySelector('input[name="narrative-view"]')).not.toBeNull();
});

it("renders hostile text as text rather than HTML", () => {
  render(<CaveTextDiff name="safe" previous="safe" proposed={'<img src=x onerror="alert(1)">'} />);
  expect(document.querySelector("img")).toBeNull();
  expect(document.body).toHaveTextContent("<img src=x");
});
