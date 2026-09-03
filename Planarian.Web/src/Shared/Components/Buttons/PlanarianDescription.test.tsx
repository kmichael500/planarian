import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { PlanarianTag } from "../Display/PlanarianTag";
import { defaultIfEmpty } from "../../Helpers/StringHelpers";
import { PlanarianDescription } from "./PlanarianDescription";

jest.mock("../../../ThemeProvider", () => ({
  useTheme: () => ({ effectiveMode: "light" }),
}));

const writeText = jest.fn().mockResolvedValue(undefined);

beforeAll(() => {
  Object.defineProperty(window, "matchMedia", {
    configurable: true,
    value: () => ({
      matches: false,
      addListener: () => undefined,
      removeListener: () => undefined,
      addEventListener: () => undefined,
      removeEventListener: () => undefined,
      dispatchEvent: () => false,
    }),
  });

  Object.defineProperty(navigator, "clipboard", {
    configurable: true,
    value: { writeText },
  });
});

beforeEach(() => {
  writeText.mockClear();
});

describe("PlanarianDescription", () => {
  it("copies when any non-interactive area of the value cell is clicked", async () => {
    render(
      <PlanarianDescription
        items={[
          {
            key: "status",
            label: "Status",
            children: (
              <>
                <span>Locked/Gated</span>
                <span>Seasonal</span>
              </>
            ),
          },
        ]}
      />
    );

    const cell = screen.getByRole("cell", { name: /Locked\/Gated/ });
    userEvent.click(cell);

    await waitFor(() =>
      expect(writeText).toHaveBeenCalledWith("Locked/Gated Seasonal")
    );
  });

  it("copies tag-only values as a comma-separated list", async () => {
    render(
      <PlanarianDescription
        items={[
          {
            key: "status",
            label: "Status",
            children: (
              <>
                <PlanarianTag>Locked/Gated</PlanarianTag>
                <PlanarianTag>Seasonal</PlanarianTag>
              </>
            ),
          },
        ]}
      />
    );

    userEvent.click(screen.getByRole("cell", { name: /Locked\/Gated/ }));

    await waitFor(() =>
      expect(writeText).toHaveBeenCalledWith("Locked/Gated, Seasonal")
    );
  });

  it("uses explicit copy text when the value cell is clicked", async () => {
    render(
      <PlanarianDescription
        items={[
          {
            key: "coordinates",
            copyText: "35.123456789, -85.987654321",
            label: "Coordinates",
            children: "35.123457, -85.987654",
          },
        ]}
      />
    );

    userEvent.click(
      screen.getByRole("cell", { name: /35\.123457, -85\.987654/ })
    );

    await waitFor(() =>
      expect(writeText).toHaveBeenCalledWith("35.123456789, -85.987654321")
    );
  });

  it("supports keyboard copying through the copy control", async () => {
    render(
      <PlanarianDescription
        items={[
          {
            key: "coordinates",
            label: "Coordinates",
            children: "35.123457, -85.987654",
          },
        ]}
      />
    );

    const copyButton = screen.getByRole("button", { name: "Copy Coordinates" });
    userEvent.tab();
    expect(copyButton).toHaveFocus();
    userEvent.keyboard("{enter}");

    await waitFor(() =>
      expect(writeText).toHaveBeenCalledWith("35.123457, -85.987654")
    );
  });

  it("lets nested controls handle their action without copying the cell", () => {
    const onShowMore = jest.fn();
    render(
      <PlanarianDescription
        items={[
          {
            key: "land-access",
            label: "Land Access",
            children: (
              <button type="button" onClick={onShowMore}>
                Show more
              </button>
            ),
          },
        ]}
      />
    );

    userEvent.click(screen.getByRole("button", { name: "Show more" }));

    expect(onShowMore).toHaveBeenCalledTimes(1);
    expect(writeText).not.toHaveBeenCalled();
  });

  it("does not add copy affordances for empty placeholder values", () => {
    render(
      <PlanarianDescription
        items={[
          {
            key: "reported-on",
            label: "Reported On",
            children: defaultIfEmpty(null),
          },
        ]}
      />
    );

    expect(
      screen.queryByRole("button", { name: "Copy Reported On" })
    ).not.toBeInTheDocument();
  });

  it("can disable copying for specialized description layouts", () => {
    render(
      <PlanarianDescription
        copyable={false}
        items={[
          {
            key: "description",
            label: "Description",
            children: "Long specialized content",
          },
        ]}
      />
    );

    userEvent.click(screen.getByText("Long specialized content"));
    expect(
      screen.queryByRole("button", { name: "Copy Description" })
    ).not.toBeInTheDocument();
    expect(writeText).not.toHaveBeenCalled();
  });
});
