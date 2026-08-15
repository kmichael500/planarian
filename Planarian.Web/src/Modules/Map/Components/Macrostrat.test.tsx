import { render, screen } from "@testing-library/react";
import React from "react";
import { MapService } from "../Services/MapService";
import { Macrostrat } from "./Macrostrat";

jest.mock("../Services/MapService", () => ({
  MapService: {
    getGeologicMaps: jest.fn(),
  },
}));

jest.mock("antd", () => {
  const actual = jest.requireActual("antd");
  const React = jest.requireActual("react");
  const Panel = ({ children }: { children: React.ReactNode }) =>
    React.createElement("section", null, children);
  const Collapse = ({ children }: { children: React.ReactNode }) =>
    React.createElement("div", null, children);
  Collapse.Panel = Panel;

  return {
    ...actual,
    Collapse,
    Grid: { ...actual.Grid, useBreakpoint: () => ({ md: true }) },
  };
});

jest.mock("../../../Shared/Components/Display/PlanarianTag", () => {
  const React = jest.requireActual("react");
  return {
    PlanarianTag: ({ children }: { children: React.ReactNode }) =>
      React.createElement("span", null, children),
  };
});

const fetchMock = jest.fn();
const getGeologicMapsMock = MapService.getGeologicMaps as jest.Mock;

const javascriptUrl = "javascript:window.__planarianXss=true";
const dataUrl = "data:text/html,<script>window.__planarianXss=true</script>";

const macrostratResponse = {
  success: {
    data: {
      elevation: 0,
      hasColumns: false,
      mapData: [
        {
          name: "Test geologic unit",
          strat_name: "Test Formation",
          lith: "limestone",
          descrip: "",
          comments: "",
          liths: [],
          ref: {
            url: javascriptUrl,
            name: "Untrusted source",
            ref_source: "test",
            ref_year: 2026,
          },
          macrostrat: {
            strat_names: [],
            liths: [],
            environs: [],
          },
        },
      ],
      regions: [
        {
          name: "Untrusted region",
          boundary_group: "test",
          boundary_type: "test",
          boundary_class: "test",
          descrip: "test",
          wiki_link: dataUrl,
        },
      ],
    },
  },
};

const xddResponse = (highlight: string) => ({
  success: {
    data: [
      {
        title: "Test paper",
        pubname: "Test journal",
        authors: "Test Author",
        doi: null,
        highlight: [highlight],
      },
    ],
  },
});

const jsonResponse = (body: unknown) => ({
  ok: true,
  statusText: "OK",
  json: async () => body,
});

const queueApiResponses = (highlight: string) => {
  fetchMock
    .mockResolvedValueOnce(jsonResponse(macrostratResponse))
    .mockResolvedValueOnce(jsonResponse(xddResponse(highlight)));
};

beforeAll(() => {
  Object.defineProperty(window, "matchMedia", {
    writable: true,
    value: () => ({
      matches: false,
      addListener: () => {},
      removeListener: () => {},
      addEventListener: () => {},
      removeEventListener: () => {},
      dispatchEvent: () => false,
    }),
  });
});

beforeEach(() => {
  fetchMock.mockReset();
  getGeologicMapsMock.mockReset();
  getGeologicMapsMock.mockResolvedValue([]);
  global.fetch = fetchMock as typeof fetch;
  delete (window as any).__planarianXss;
});

describe("Macrostrat untrusted browser content", () => {
  it("does not preserve executable markup from xDD highlights", async () => {
    const highlight = [
      'A <mark data-xss-probe="mark" onclick="window.__planarianXss=true">matching phrase</mark>',
      '<img data-xss-probe="image" src="x" onerror="window.__planarianXss=true">',
      '<script data-xss-probe="script">window.__planarianXss=true</script>',
      `<a data-xss-probe="link" href="${javascriptUrl}">malicious link</a>`,
    ].join("");
    queueApiResponses(highlight);

    const { container } = render(<Macrostrat lat={35} lng={-86} />);

    const highlighted = await screen.findByText("matching phrase");
    expect(highlighted.tagName).toBe("MARK");
    expect(highlighted.attributes).toHaveLength(0);
    expect(container.querySelector("[onerror]")).toBeNull();
    expect(
      container.querySelector('script[data-xss-probe="script"]')
    ).toBeNull();
    expect(container.querySelector('a[data-xss-probe="link"]')).toBeNull();
  });

  it("does not expose executable protocols from third-party links", async () => {
    queueApiResponses("safe highlight");

    render(<Macrostrat lat={35} lng={-86} />);

    const sourceText = await screen.findByText(/Untrusted source/i);
    expect(sourceText.closest("a")).toBeNull();

    const wikiText = await screen.findByText(dataUrl);
    expect(wikiText.closest("a")).toBeNull();
  });
});
