import { render, screen } from "@testing-library/react";
import React from "react";
import { CaveService } from "../Service/CaveService";
import { CavesComponent } from "./CavesComponent";

jest.mock("../Service/CaveService", () => ({
  CaveService: {
    GetCaves: jest.fn(),
  },
}));

jest.mock("../../../Shared/Permissioning/Components/ShouldDisplay", () => ({
  useFeatureEnabled: () => ({ isFeatureEnabled: () => false }),
}));

jest.mock("./CaveAdvancedSearchDrawer", () => ({
  CaveAdvancedSearchDrawer: () => null,
}));

jest.mock("./FeatureCheckboxGroup", () => ({
  FeatureCheckboxGroup: () => null,
}));

jest.mock("./FavoriteCave", () => ({
  __esModule: true,
  default: () => null,
}));

jest.mock("../../../Shared/Components/SpinnerCard/SpinnerCard", () => {
  const React = jest.requireActual("react");
  return {
    SpinnerCardComponent: ({ children }: { children: React.ReactNode }) =>
      React.createElement(React.Fragment, null, children),
  };
});

jest.mock("../../../Shared/Components/CardGrid/GridCard", () => {
  const React = jest.requireActual("react");
  return {
    GridCard: ({ header, children }: any) =>
      React.createElement("article", null, header, children),
  };
});

jest.mock("../../../Shared/Components/CardGrid/CardGridComponent", () => {
  const React = jest.requireActual("react");
  return {
    CardGridComponent: ({ pagedItems, renderItem }: any) =>
      React.createElement(
        React.Fragment,
        null,
        ...(pagedItems?.results ?? []).map((item: any) =>
          React.createElement(
            React.Fragment,
            { key: item.id },
            renderItem(item)
          )
        )
      ),
  };
});

jest.mock("../../../Shared/Scroll/useScrollRevealVisibility", () => ({
  useScrollRevealVisibility: () => ({
    contentRef: { current: null },
    isVisible: true,
    handleScrollStateChange: () => {},
  }),
}));

const getCavesMock = CaveService.GetCaves as jest.Mock;

const caveResult = (narrativeSnippet: string) => ({
  pageNumber: 1,
  pageSize: 10,
  totalCount: 1,
  totalPages: 1,
  results: [
    {
      id: "0123456789",
      name: "Security Test Cave",
      narrativeSnippet,
      reportedOn: null,
      isArchived: false,
      depthFeet: null,
      lengthFeet: null,
      maxPitDepthFeet: null,
      numberOfPits: null,
      county: { display: "Test County", value: "county" },
      countyDisplayId: "TST",
      countyNumber: 1,
      displayId: "TST1",
      primaryEntranceLatitude: null,
      primaryEntranceLongitude: null,
      primaryEntranceElevationFeet: null,
      distanceMiles: null,
      archaeologyTags: [],
      biologyTags: [],
      cartographerNameTags: [],
      geologicAgeTags: [],
      geologyTags: [],
      mapStatusTags: [],
      otherTags: [],
      physiographicProvinceTags: [],
      reportedByTags: [],
      isFavorite: false,
    },
  ],
});

beforeEach(() => {
  localStorage.clear();
  localStorage.setItem("selectedFeatures", "[]");
  getCavesMock.mockReset();
});

describe("CavesComponent narrative HTML", () => {
  it("removes executable markup while preserving allowed highlighting", async () => {
    const narrative = [
      'Safe narrative with <mark data-xss-probe="mark" onclick="window.__planarianXss=true">matching phrase</mark>.',
      '<img data-xss-probe="image" src="x" onerror="window.__planarianXss=true">',
      '<script data-xss-probe="script">window.__planarianXss=true</script>',
      '<a data-xss-probe="link" href="javascript:window.__planarianXss=true">malicious link</a>',
    ].join("");
    getCavesMock.mockResolvedValue(caveResult(narrative));

    const { container } = render(<CavesComponent />);
    const highlighted = await screen.findByText("matching phrase");
    expect(highlighted.tagName).toBe("MARK");
    expect(highlighted.attributes).toHaveLength(0);

    expect(
      container.querySelector('script[data-xss-probe="script"]')
    ).toBeNull();
    expect(container.querySelector("[onerror]")).toBeNull();

    const injectedLink = container.querySelector('a[data-xss-probe="link"]');
    if (injectedLink) {
      expect(injectedLink.getAttribute("href") ?? "").not.toMatch(
        /^\s*javascript:/i
      );
    }
  });
});
