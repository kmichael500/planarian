import React from "react";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { GageList, downsampleStreamGagePoints } from "./GaugeList";
import { MapService, NearbyStreamGage, StreamGagePoint } from "../Services/MapService";

jest.mock("chartjs-adapter-date-fns", () => ({}));
jest.mock("../../../ThemeProvider", () => ({ useTheme: () => ({ effectiveMode: "light" }) }));
jest.mock("react-chartjs-2", () => ({ Line: () => <div data-testid="gage-chart" /> }));

jest.mock("../Services/MapService", () => ({
  MapService: {
    getStreamGageObservations: jest.fn(),
    getStreamGagePeakSummary: jest.fn(),
  },
}));

const mockedMapService = MapService as jest.Mocked<typeof MapService>;

beforeAll(() => {
  Object.defineProperty(window, "matchMedia", {
    writable: true,
    value: jest.fn().mockImplementation((query: string) => ({
      matches: false,
      media: query,
      onchange: null,
      addListener: jest.fn(),
      removeListener: jest.fn(),
      addEventListener: jest.fn(),
      removeEventListener: jest.fn(),
      dispatchEvent: jest.fn(),
    })),
  });
});

const gage: NearbyStreamGage = {
  id: "USGS-03419530",
  siteCode: "03419530",
  siteName: "CALFKILLER RIVER AT HWY 70 AT SPARTA, TN",
  latitude: 35.93,
  longitude: -85.47,
  distanceMiles: 5.19,
  nearestOriginName: "BLUE SPRING CAVE (SPRING ENTRANCE)",
  drainageAreaSquareMiles: 157,
  parameters: [
    {
      parameterCode: "00065",
      variableName: "Gage height",
      unit: "ft",
      points: [
        { value: "2.70", dateTime: "2026-09-11T10:00:00Z", approvalStatus: "Provisional" },
        { value: "2.93", dateTime: "2026-09-11T16:00:00Z", approvalStatus: "Provisional" },
      ],
    },
  ],
};

describe("GageList", () => {
  beforeEach(() => {
    jest.clearAllMocks();
    mockedMapService.getStreamGageObservations.mockResolvedValue([]);
    mockedMapService.getStreamGagePeakSummary.mockResolvedValue({
      siteCode: gage.siteCode,
      gageHeight: {
        parameterCode: "00065",
        variableName: "Gage height",
        unit: "ft",
        value: "58.5",
        date: "1793-01-01",
        waterYear: 1793,
        qualifiers: ["MONTHUNKNOWN"],
        firstWaterYear: 1793,
        lastWaterYear: 2025,
        annualPeakCount: 169,
      },
    });
  });

  test("loads observations and peaks only after a station is expanded", async () => {
    render(
      <GageList
        gages={[gage]}
        startDate="2026-09-04T00:00:00Z"
        endDate="2026-09-11T00:00:00Z"
      />
    );

    expect(mockedMapService.getStreamGageObservations).not.toHaveBeenCalled();
    expect(mockedMapService.getStreamGagePeakSummary).not.toHaveBeenCalled();

    fireEvent.click(screen.getByText(gage.siteName));

    await waitFor(() => expect(mockedMapService.getStreamGageObservations).toHaveBeenCalledTimes(1));
    await waitFor(() => expect(mockedMapService.getStreamGagePeakSummary).toHaveBeenCalledTimes(1));
    expect(await screen.findByText("USGS note: Month of occurrence is unknown or not exact")).toBeInTheDocument();
    expect(screen.queryByText(/Jan 1, 1793/)).not.toBeInTheDocument();
  });

  test("filters annual peak history locally with all time as the default", async () => {
    mockedMapService.getStreamGagePeakSummary.mockResolvedValue({
      siteCode: gage.siteCode,
      streamflow: {
        parameterCode: "00060",
        variableName: "Streamflow",
        unit: "ft^3/s",
        value: "1000",
        date: "2000-03-01",
        waterYear: 2000,
        qualifiers: [],
        firstWaterYear: 2000,
        lastWaterYear: 2025,
        annualPeakCount: 6,
      },
      streamflowHistory: [
        { parameterCode: "00060", variableName: "Streamflow", unit: "ft^3/s", value: "1000", date: "2000-03-01", waterYear: 2000, qualifiers: [] },
        { parameterCode: "00060", variableName: "Streamflow", unit: "ft^3/s", value: "10", date: "2021-03-01", waterYear: 2021, qualifiers: [] },
        { parameterCode: "00060", variableName: "Streamflow", unit: "ft^3/s", value: "20", date: "2022-03-01", waterYear: 2022, qualifiers: [] },
        { parameterCode: "00060", variableName: "Streamflow", unit: "ft^3/s", value: "30", date: "2023-03-01", waterYear: 2023, qualifiers: [] },
        { parameterCode: "00060", variableName: "Streamflow", unit: "ft^3/s", value: "25", date: "2024-03-01", waterYear: 2024, qualifiers: [] },
        { parameterCode: "00060", variableName: "Streamflow", unit: "ft^3/s", value: "20", date: "2025-03-01", waterYear: 2025, qualifiers: [] },
      ],
      gageHeightHistory: [],
    });

    render(<GageList gages={[gage]} startDate="2026-09-04T00:00:00Z" endDate="2026-09-11T00:00:00Z" />);
    fireEvent.click(screen.getByText(gage.siteName));

    expect(await screen.findByText("1,000 ft³/s")).toBeInTheDocument();
    fireEvent.click(screen.getByText("5 yr"));
    await waitFor(() => expect(screen.getByText("30 ft³/s")).toBeInTheDocument());
    expect(screen.queryByText("1,000 ft³/s")).not.toBeInTheDocument();

    fireEvent.click(screen.getByText("Since…"));
    fireEvent.change(screen.getByLabelText("Peak history starting water year"), { target: { value: "2024" } });
    await waitFor(() => expect(screen.getByText(/Annual peaks 2024–2025 · 2 water years/)).toBeInTheDocument());
    expect(screen.getByText("25 ft³/s")).toBeInTheDocument();
    expect(mockedMapService.getStreamGagePeakSummary).toHaveBeenCalledTimes(1);
  });

  test("accepts the legacy peak qualifier response without crashing", async () => {
    mockedMapService.getStreamGagePeakSummary.mockResolvedValue({
      siteCode: gage.siteCode,
      gageHeight: {
        parameterCode: "00065",
        variableName: "Gage height",
        unit: "ft",
        value: "58.5",
        date: "1793-01-01",
        waterYear: 1793,
        qualifier: '["MONTHUNKNOWN"]',
        firstWaterYear: 1793,
        lastWaterYear: 2025,
        annualPeakCount: 169,
      },
    });

    render(
      <GageList
        gages={[gage]}
        startDate="2026-09-04T00:00:00Z"
        endDate="2026-09-11T00:00:00Z"
      />
    );

    fireEvent.click(screen.getByText(gage.siteName));

    expect(await screen.findByText("USGS note: Month of occurrence is unknown or not exact")).toBeInTheDocument();
    expect(screen.queryByText(/Jan 1, 1793/)).not.toBeInTheDocument();
  });

  test("downsampling preserves first, last, minimum, and maximum observations", () => {
    const points: StreamGagePoint[] = Array.from({ length: 1000 }, (_, index) => ({
      value: String(index === 400 ? -50 : index === 700 ? 5000 : index % 100),
      dateTime: new Date(Date.UTC(2026, 0, 1, 0, index)).toISOString(),
    }));

    const sampled = downsampleStreamGagePoints(points, 100);
    expect(sampled.length).toBeLessThanOrEqual(102);
    expect(sampled[0]).toBe(points[0]);
    expect(sampled[sampled.length - 1]).toBe(points[points.length - 1]);
    expect(sampled.some((point) => point.value === "-50")).toBe(true);
    expect(sampled.some((point) => point.value === "5000")).toBe(true);
  });
});
