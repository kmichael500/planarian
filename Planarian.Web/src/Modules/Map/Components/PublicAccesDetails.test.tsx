import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { PublicAccessDetails } from "./PublicAccesDetails";

jest.mock("../../../ThemeProvider", () => ({
  useTheme: () => ({ effectiveMode: "light" }),
}));

const fetchMock = jest.fn();

const protectedAreaResponse = {
  features: [
    {
      attributes: {
        Unit_Nm: "Test Conservation Area",
        MngNm_Desc: "Test Agency",
        Pub_Access: "OA",
        GAP_Sts: "1",
        IUCN_Cat: "II",
        DesTp_Desc: "Protected Area",
        GIS_Acres: 123,
      },
    },
  ],
};

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
});

beforeEach(() => {
  fetchMock.mockReset();
  fetchMock.mockResolvedValue({
    json: async () => protectedAreaResponse,
  });
  global.fetch = fetchMock as typeof fetch;
});

describe("PublicAccessDetails", () => {
  it("uses a semantic control to expand additional details", async () => {
    render(<PublicAccessDetails lat={35} lng={-86} />);

    const showMore = await screen.findByRole("button", {
      name: "Show more...",
    });
    expect(showMore).toHaveAttribute("aria-expanded", "false");

    userEvent.click(showMore);

    expect(screen.getByText("Designation Type:")).toBeInTheDocument();
    expect(screen.getByText("Protected Area")).toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: "Show less..." })
    ).toHaveAttribute("aria-expanded", "true");
  });
});
