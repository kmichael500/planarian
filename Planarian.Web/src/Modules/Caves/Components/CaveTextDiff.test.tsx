import { fireEvent, render, screen, within } from "@testing-library/react";
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

  expect(screen.getByText("Inline changes were too complex to display.")).toBeInTheDocument();
  expect(within(screen.getByText("complete old text").parentElement!).getByText("Before")).toBeInTheDocument();
  expect(within(screen.getByText("complete new text").parentElement!).getByText("After")).toBeInTheDocument();
});

it("shows only the change-operation legend entries that are actually present", () => {
  const view = render(<CaveTextDiff name="addition" previous="" proposed="added text" />);

  expect(screen.getByText("+ Added")).toBeInTheDocument();
  expect(screen.queryByText("− Removed")).not.toBeInTheDocument();

  view.rerender(<CaveTextDiff name="removal" previous="removed text" proposed="" />);
  expect(screen.getByText("− Removed")).toBeInTheDocument();
  expect(screen.queryByText("+ Added")).not.toBeInTheDocument();
});

it("defaults to Changes and exposes complete Before and After views", () => {
  render(<CaveTextDiff name="narrative-view" previous="complete old text" proposed="complete new text" />);

  expect(screen.getByRole("radio", { name: "Changes" })).toBeChecked();
  expect(screen.queryByRole("button", { name: "Show full context" })).not.toBeInTheDocument();
  fireEvent.click(screen.getByRole("radio", { name: "Before" }));
  expect(screen.getByText("complete old text")).toBeInTheDocument();
  fireEvent.click(screen.getByRole("radio", { name: "After" }));
  expect(screen.getByText("complete new text")).toBeInTheDocument();
  expect(screen.getByRole("radio", { name: "After" })).toHaveAttribute("name", "narrative-view");
});

it("focuses long Changes views on edited context and lets the reviewer reveal the full diff", () => {
  const beforeChange = `EARLY_UNCHANGED_SENTINEL ${"unchanged context ".repeat(45)}`;
  const afterChange = `${"later unchanged context ".repeat(45)} LATE_UNCHANGED_SENTINEL`;
  render(<CaveTextDiff name="focused" previous={`${beforeChange}old phrase${afterChange}`}
    proposed={`${beforeChange}new phrase${afterChange}`} />);

  expect(screen.getByText("old")).toBeInTheDocument();
  expect(screen.getByText("new")).toBeInTheDocument();
  expect(screen.queryByText(/EARLY_UNCHANGED_SENTINEL/)).not.toBeInTheDocument();
  expect(screen.queryByText(/LATE_UNCHANGED_SENTINEL/)).not.toBeInTheDocument();
  expect(screen.getAllByLabelText("Unchanged text omitted").length).toBeGreaterThan(0);

  const reveal = screen.getByRole("button", { name: "Show full context" });
  expect(reveal).toHaveAttribute("aria-expanded", "false");
  fireEvent.click(reveal);
  expect(screen.getByText(/EARLY_UNCHANGED_SENTINEL/)).toBeInTheDocument();
  expect(screen.getByText(/LATE_UNCHANGED_SENTINEL/)).toBeInTheDocument();
  expect(screen.getByRole("button", { name: "Show less context" })).toHaveAttribute("aria-expanded", "true");
});

it("renders hostile text as text rather than HTML", () => {
  render(<CaveTextDiff name="safe" previous="safe" proposed={'<img src=x onerror="alert(1)">'} />);
  expect(screen.queryByRole("img")).not.toBeInTheDocument();
  expect(document.body).toHaveTextContent("<img src=x");
});
