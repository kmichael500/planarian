import React, { FC, useEffect, useMemo, useState } from "react";
import { Collapse, Grid, InputNumber, Segmented, Spin } from "antd";
import { Line } from "react-chartjs-2";
import {
  Chart as ChartJS,
  CategoryScale,
  LinearScale,
  PointElement,
  LineElement,
  Title,
  Tooltip,
  Legend,
  TimeScale,
} from "chart.js";
import "chartjs-adapter-date-fns";
import { useTheme } from "../../../ThemeProvider";
import "./GaugeList.scss";
import {
  MapService,
  NearbyStreamGage,
  StreamGageAnnualPeak,
  StreamGageHistoricalPeak,
  StreamGageParameter,
  StreamGagePeakSummary,
  StreamGagePoint,
} from "../Services/MapService";

ChartJS.register(
  CategoryScale,
  LinearScale,
  PointElement,
  LineElement,
  Title,
  Tooltip,
  Legend,
  TimeScale
);

const { Panel } = Collapse;
const SIX_HOURS_MS = 6 * 60 * 60 * 1000;
const TREND_TOLERANCE_MS = 90 * 60 * 1000;
const ONE_DAY_MS = 24 * 60 * 60 * 1000;

function formatUnit(unit: string) {
  return unit.replace("ft^3/s", "ft³/s");
}

function formatNumber(value: string | number, maximumFractionDigits = 2) {
  const numeric = typeof value === "number" ? value : Number(value);
  if (!Number.isFinite(numeric)) return String(value);
  return new Intl.NumberFormat(undefined, { maximumFractionDigits }).format(numeric);
}

function getNearestOriginLabel(name?: string | null) {
  if (!name) return "";
  const entranceName = name.match(/\(([^()]+)\)\s*\*?$/)?.[1];
  return entranceName ?? name.replace(/\s*\*$/, "");
}

function getParameter(parameters: StreamGageParameter[], parameterCode: string) {
  return parameters.find((parameter) => parameter.parameterCode === parameterCode);
}

function getLatestPoint(parameter?: StreamGageParameter) {
  return parameter?.points[parameter.points.length - 1];
}

function getLatestObservation(parameters: StreamGageParameter[]) {
  return parameters
    .map((parameter) => getLatestPoint(parameter))
    .filter((point): point is StreamGagePoint => Boolean(point))
    .sort((a, b) => new Date(b.dateTime).getTime() - new Date(a.dateTime).getTime())[0];
}

function formatObservationTime(point?: StreamGagePoint) {
  if (!point) return "No recent observation";
  const date = new Date(point.dateTime);
  const ageMs = Date.now() - date.getTime();
  if (ageMs >= 0 && ageMs < ONE_DAY_MS) {
    const minutes = Math.floor(ageMs / 60000);
    if (minutes < 1) return "Updated just now";
    if (minutes < 60) return `Updated ${minutes} min ago`;
    const hours = Math.floor(minutes / 60);
    return `Updated ${hours} hr${hours === 1 ? "" : "s"} ago`;
  }

  return `Observed ${date.toLocaleString(undefined, {
    month: "short",
    day: "numeric",
    year: "numeric",
    hour: "numeric",
    minute: "2-digit",
  })}`;
}

function getApprovalStatus(parameters: StreamGageParameter[]) {
  const statuses = parameters
    .map((parameter) => getLatestPoint(parameter)?.approvalStatus)
    .filter((status): status is string => Boolean(status));
  if (statuses.some((status) => status.toLowerCase() === "provisional")) return "Provisional";
  if (statuses.length > 0 && statuses.every((status) => status.toLowerCase() === "approved")) return "Approved";
  return null;
}

function getSixHourTrend(parameter?: StreamGageParameter) {
  if (!parameter || parameter.points.length < 2) return null;
  const numericPoints = parameter.points
    .map((point) => ({ point, value: Number(point.value), time: new Date(point.dateTime).getTime() }))
    .filter((entry) => Number.isFinite(entry.value) && Number.isFinite(entry.time));
  if (numericPoints.length < 2) return null;

  const latest = numericPoints[numericPoints.length - 1];
  const target = latest.time - SIX_HOURS_MS;
  const baseline = numericPoints
    .slice(0, -1)
    .reduce<(typeof numericPoints)[number] | null>((closest, point) => {
      if (!closest) return point;
      return Math.abs(point.time - target) < Math.abs(closest.time - target) ? point : closest;
    }, null);
  if (!baseline || Math.abs(baseline.time - target) > TREND_TOLERANCE_MS) return null;

  if (parameter.parameterCode === "00060") {
    if (baseline.value === 0) return null;
    const percent = ((latest.value - baseline.value) / Math.abs(baseline.value)) * 100;
    if (Math.abs(percent) < 2) return "→ steady / 6 hr";
    return `${percent > 0 ? "↑" : "↓"} ${Math.abs(percent).toFixed(0)}% / 6 hr`;
  }

  const delta = latest.value - baseline.value;
  if (Math.abs(delta) < 0.01) return "→ steady / 6 hr";
  return `${delta > 0 ? "↑" : "↓"} ${Math.abs(delta).toFixed(2)} ${formatUnit(parameter.unit)} / 6 hr`;
}

function getRange(parameter: StreamGageParameter) {
  const values = parameter.points.map((point) => Number(point.value)).filter(Number.isFinite);
  if (values.length === 0) return null;
  return { min: Math.min(...values), max: Math.max(...values) };
}

function getTimeUnit(points: StreamGagePoint[]) {
  if (points.length < 2) return "hour" as const;
  const duration = new Date(points[points.length - 1].dateTime).getTime() - new Date(points[0].dateTime).getTime();
  if (duration <= 2 * ONE_DAY_MS) return "hour" as const;
  if (duration <= 14 * ONE_DAY_MS) return "day" as const;
  if (duration <= 90 * ONE_DAY_MS) return "week" as const;
  return "month" as const;
}

export function downsampleStreamGagePoints(
  points: StreamGagePoint[],
  maxPoints: number
): StreamGagePoint[] {
  if (points.length <= maxPoints || maxPoints < 4) return points;

  const first = points[0];
  const last = points[points.length - 1];
  const interior = points.slice(1, -1);
  const bucketCount = Math.max(1, Math.floor((maxPoints - 2) / 2));
  const bucketSize = interior.length / bucketCount;
  const sampled: StreamGagePoint[] = [first];

  for (let bucketIndex = 0; bucketIndex < bucketCount; bucketIndex += 1) {
    const start = Math.floor(bucketIndex * bucketSize);
    const end = Math.min(interior.length, Math.floor((bucketIndex + 1) * bucketSize));
    const bucket = interior.slice(start, Math.max(start + 1, end));
    if (bucket.length === 0) continue;

    let minimum = bucket[0];
    let maximum = bucket[0];
    for (const point of bucket.slice(1)) {
      if (Number(point.value) < Number(minimum.value)) minimum = point;
      if (Number(point.value) > Number(maximum.value)) maximum = point;
    }

    if (minimum === maximum) {
      sampled.push(minimum);
    } else if (new Date(minimum.dateTime).getTime() < new Date(maximum.dateTime).getTime()) {
      sampled.push(minimum, maximum);
    } else {
      sampled.push(maximum, minimum);
    }
  }

  sampled.push(last);
  return sampled;
}

function useChartColors() {
  const { effectiveMode } = useTheme();
  return {
    chartTextColor: effectiveMode === "dark" ? "#d8dde6" : "#5d6570",
    chartGridColor: effectiveMode === "dark" ? "rgba(255, 255, 255, 0.14)" : "rgba(0, 0, 0, 0.10)",
    chartLineColor: effectiveMode === "dark" ? "rgb(116, 147, 255)" : "rgb(104, 141, 248)",
  };
}

function ParameterChart({ parameter }: { parameter: StreamGageParameter }) {
  const screens = Grid.useBreakpoint();
  const { chartTextColor, chartGridColor, chartLineColor } = useChartColors();
  const dataPoints = parameter.points;
  if (dataPoints.length === 0) return <div>No data available</div>;

  const displayPoints = downsampleStreamGagePoints(dataPoints, screens.md ? 1200 : 300);

  const chartData = {
    datasets: [{
      label: `${parameter.variableName}${parameter.unit ? `, ${formatUnit(parameter.unit)}` : ""}`,
      data: displayPoints.map((point) => ({ x: point.dateTime, y: Number(point.value) })),
      fill: false,
      tension: 0,
      borderColor: chartLineColor,
      backgroundColor: chartLineColor,
      pointRadius: 0,
    }],
  };

  const options = {
    responsive: true,
    maintainAspectRatio: false,
    interaction: { intersect: false, mode: "index" as const },
    plugins: { legend: { display: false }, title: { display: false } },
    scales: {
      x: {
        type: "time" as const,
        ticks: { color: chartTextColor, maxRotation: screens.sm ? 45 : 0, font: { size: screens.sm ? 10 : 8 }, maxTicksLimit: screens.sm ? 10 : 5 },
        grid: { color: chartGridColor },
        border: { color: chartGridColor },
        time: { unit: getTimeUnit(dataPoints) },
      },
      y: {
        type: "linear" as const,
        title: { display: screens.sm, color: chartTextColor, text: `${parameter.variableName}${parameter.unit ? `, ${formatUnit(parameter.unit)}` : ""}`, font: { size: 12 } },
        ticks: { color: chartTextColor, font: { size: screens.sm ? 10 : 8 } },
        grid: { color: chartGridColor },
        border: { color: chartGridColor },
      },
    },
  };

  return <div style={{ height: screens.md ? 320 : 220, width: "100%" }}><Line data={chartData} options={options} /></div>;
}

const PEAK_QUALIFIER_LABELS: Record<string, string> = {
  MAXDAILYMEAN: "Discharge is a maximum daily average",
  ESTIMATED: "Estimated",
  DAMFAILURE: "Affected by dam failure",
  LESSTHAN: "Less than the indicated value",
  UNKNOWNREGULATION: "Affected to an unknown degree by regulation or diversion",
  REGULATED: "Affected by regulation or diversion",
  HISTORIC: "Historic peak",
  GREATERTHAN: "Actual value is greater than the indicated value",
  EVENT: "Affected by snowmelt, hurricane, ice jam, or debris-dam breakup",
  YEARUNKNOWN: "Year of occurrence is unknown or not exact",
  DAYUNKNOWN: "Day of occurrence is unknown or not exact",
  MONTHUNKNOWN: "Month of occurrence is unknown or not exact",
  URBAN: "Record affected by land-use or channel changes",
  OTHERAGENCY: "Peak supplied by another agency",
  OPPORTUNISTIC: "Opportunistic observation",
  REVISED: "Revised",
  BACKWATER: "Gage height affected by backwater",
  NOTMAXGH: "Gage height is not the maximum for the year",
  DIFFDATUM: "Gage height is from a different site or datum",
  BLWMIN: "Below minimum recordable elevation",
  DATUMCHANGE: "Gage datum changed during this year",
  DEBRIS: "Affected by debris, mud, or hyper-concentrated flow",
  TIDE: "Tidally affected",
  GHNOTASSCPKQ: "Maximum gage height was not associated with peak discharge",
};

function getPeakQualifiers(peak: StreamGageAnnualPeak): string[] {
  const qualifiers = Array.isArray(peak.qualifiers) ? peak.qualifiers : [];
  const legacy = peak.qualifier;
  if (Array.isArray(legacy)) return Array.from(new Set([...qualifiers, ...legacy]));
  if (typeof legacy !== "string" || !legacy.trim()) return qualifiers;

  const trimmed = legacy.trim();
  if (trimmed.startsWith("[") && trimmed.endsWith("]")) {
    try {
      const parsed = JSON.parse(trimmed);
      if (Array.isArray(parsed)) {
        return Array.from(new Set([
          ...qualifiers,
          ...parsed.filter((value): value is string => typeof value === "string"),
        ]));
      }
    } catch {
      // Fall through and preserve the legacy value as-is.
    }
  }

  return Array.from(new Set([...qualifiers, trimmed]));
}

function formatPeakDate(peak: StreamGageAnnualPeak) {
  if (!peak.date) return null;
  const qualifiers = getPeakQualifiers(peak);
  const date = new Date(`${peak.date}T00:00:00`);
  if (qualifiers.includes("YEARUNKNOWN")) return "Date unknown or approximate";
  if (qualifiers.includes("MONTHUNKNOWN")) return date.toLocaleDateString(undefined, { year: "numeric" });
  if (qualifiers.includes("DAYUNKNOWN")) return date.toLocaleDateString(undefined, { month: "short", year: "numeric" });
  return date.toLocaleDateString(undefined, { month: "short", day: "numeric", year: "numeric" });
}

function HistoricalPeak({ peak }: { peak: StreamGageHistoricalPeak }) {
  const date = formatPeakDate(peak);
  const qualifierLabels = getPeakQualifiers(peak).map((qualifier) => PEAK_QUALIFIER_LABELS[qualifier] ?? qualifier);
  return (
    <div style={{ minWidth: 220 }}>
      <div style={{ fontWeight: 600 }}>{peak.variableName}</div>
      <div>{formatNumber(peak.value)}{peak.unit ? ` ${formatUnit(peak.unit)}` : ""}</div>
      <div className="stream-gage-muted" style={{ fontSize: 12 }}>
        {[date, `Water year ${peak.waterYear}`].filter(Boolean).join(" · ")}
      </div>
      <div className="stream-gage-muted" style={{ fontSize: 12 }}>
        Annual peaks {peak.firstWaterYear}–{peak.lastWaterYear} · {peak.annualPeakCount} water year{peak.annualPeakCount === 1 ? "" : "s"}
      </div>
      {qualifierLabels.length > 0 && (
        <div className="stream-gage-muted" style={{ fontSize: 12 }}>
          USGS note: {qualifierLabels.join("; ")}
        </div>
      )}
    </div>
  );
}

type PeakRangePreset = "all" | "25y" | "10y" | "5y" | "since";

function filterPeakHistory(
  history: StreamGageAnnualPeak[],
  preset: PeakRangePreset,
  latestWaterYear: number | null,
  sinceWaterYear: number | null
) {
  if (preset === "all" || latestWaterYear === null) return history;
  const years = preset === "25y" ? 25 : preset === "10y" ? 10 : preset === "5y" ? 5 : null;
  const firstWaterYear = years !== null
    ? latestWaterYear - years + 1
    : sinceWaterYear;
  return firstWaterYear === null
    ? history
    : history.filter((peak) => peak.waterYear >= firstWaterYear);
}

function summarizePeakHistory(history: StreamGageAnnualPeak[]): StreamGageHistoricalPeak | null {
  const valid = history.filter((peak) => Number.isFinite(Number(peak.value)));
  if (valid.length === 0) return null;
  const highest = valid.reduce((current, peak) => Number(peak.value) > Number(current.value) ? peak : current);
  const waterYears = valid.map((peak) => peak.waterYear);
  return {
    ...highest,
    firstWaterYear: Math.min(...waterYears),
    lastWaterYear: Math.max(...waterYears),
    annualPeakCount: new Set(waterYears).size,
  };
}

function AnnualPeakChart({ history }: { history: StreamGageAnnualPeak[] }) {
  const { chartTextColor, chartGridColor, chartLineColor } = useChartColors();
  if (history.length < 2) return null;
  const ordered = [...history].sort((a, b) => a.waterYear - b.waterYear);
  const firstYear = ordered[0].waterYear;
  const lastYear = ordered[ordered.length - 1].waterYear;
  const byYear = new Map(ordered.map((peak) => [peak.waterYear, Number(peak.value)]));
  const years = Array.from({ length: lastYear - firstYear + 1 }, (_, index) => firstYear + index);
  const unit = ordered[0].unit;
  const variableName = ordered[0].variableName;
  const chartData = {
    labels: years.map(String),
    datasets: [{
      label: `${variableName}${unit ? `, ${formatUnit(unit)}` : ""}`,
      data: years.map((year) => byYear.get(year) ?? null),
      fill: false,
      spanGaps: false,
      tension: 0,
      borderColor: chartLineColor,
      backgroundColor: chartLineColor,
      pointBackgroundColor: chartLineColor,
      pointRadius: history.length > 80 ? 1 : 2,
      pointHoverRadius: 4,
    }],
  };
  const options = {
    responsive: true,
    maintainAspectRatio: false,
    interaction: { intersect: false, mode: "nearest" as const },
    plugins: { legend: { display: false }, title: { display: false } },
    scales: {
      x: {
        type: "category" as const,
        ticks: { color: chartTextColor, autoSkip: true, maxTicksLimit: 8, font: { size: 9 } },
        grid: { color: chartGridColor },
        border: { color: chartGridColor },
      },
      y: {
        type: "linear" as const,
        title: { display: true, color: chartTextColor, text: `${variableName}${unit ? `, ${formatUnit(unit)}` : ""}`, font: { size: 11 } },
        ticks: { color: chartTextColor, font: { size: 9 } },
        grid: { color: chartGridColor },
        border: { color: chartGridColor },
      },
    },
  };
  return <div style={{ height: 200, width: "100%", marginTop: 12 }}><Line data={chartData} options={options} /></div>;
}

interface StationDetailsProps {
  station: NearbyStreamGage;
  active: boolean;
  startDate: string | null;
  endDate: string | null;
}

function StationDetails({ station, active, startDate, endDate }: StationDetailsProps) {
  const [detailParameters, setDetailParameters] = useState<StreamGageParameter[] | null>(null);
  const [detailLoading, setDetailLoading] = useState(false);
  const [detailError, setDetailError] = useState(false);
  const [selectedCode, setSelectedCode] = useState<string | undefined>(undefined);
  const [peakSummary, setPeakSummary] = useState<StreamGagePeakSummary | null>(null);
  const [peakLoading, setPeakLoading] = useState(false);
  const [peakError, setPeakError] = useState(false);
  const [peakRangePreset, setPeakRangePreset] = useState<PeakRangePreset>("all");
  const [peakSinceWaterYear, setPeakSinceWaterYear] = useState<number | null>(null);
  const [selectedPeakCode, setSelectedPeakCode] = useState("00060");
  const availableParameters = useMemo(
    () => (detailParameters ?? []).filter((parameter) => parameter.points.length > 0),
    [detailParameters]
  );

  useEffect(() => {
    if (!active) {
      setDetailParameters(null);
      setDetailLoading(false);
      setDetailError(false);
      return;
    }
    if (!startDate || !endDate) {
      setDetailParameters([]);
      setDetailError(false);
      return;
    }

    let cancelled = false;
    setDetailParameters(null);
    setDetailLoading(true);
    setDetailError(false);
    MapService.getStreamGageObservations(station.siteCode, startDate, endDate)
      .then((parameters) => {
        if (cancelled) return;
        setDetailParameters(parameters);
        setSelectedCode((current) => {
          if (current && parameters.some((parameter) => parameter.parameterCode === current && parameter.points.length > 0)) {
            return current;
          }
          return parameters.some((parameter) => parameter.parameterCode === "00065" && parameter.points.length > 0)
            ? "00065"
            : parameters.find((parameter) => parameter.points.length > 0)?.parameterCode;
        });
      })
      .catch((error) => {
        console.error("Unable to load USGS stream gage observations", error);
        if (!cancelled) {
          setDetailParameters([]);
          setDetailError(true);
        }
      })
      .finally(() => {
        if (!cancelled) setDetailLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [active, endDate, startDate, station.siteCode]);

  useEffect(() => {
    if (!active || peakSummary || peakLoading || peakError) return;
    setPeakLoading(true);
    MapService.getStreamGagePeakSummary(station.siteCode)
      .then(setPeakSummary)
      .catch((error) => {
        console.error("Unable to load USGS annual peaks", error);
        setPeakError(true);
      })
      .finally(() => setPeakLoading(false));
  }, [active, peakError, peakLoading, peakSummary, station.siteCode]);

  const selectedParameter = availableParameters.find((parameter) => parameter.parameterCode === selectedCode) ?? availableParameters[0];
  const latest = getLatestPoint(selectedParameter);
  const range = selectedParameter ? getRange(selectedParameter) : null;
  const trend = getSixHourTrend(selectedParameter);
  const observationParameters = detailParameters ?? station.parameters;
  const approvalStatus = getApprovalStatus(observationParameters);
  const latestObservation = getLatestObservation(observationParameters);
  const drainageArea = station.contributingDrainageAreaSquareMiles ?? station.drainageAreaSquareMiles;
  const drainageLabel = station.contributingDrainageAreaSquareMiles != null ? "Contributing drainage area" : "Drainage area";
  const usgsLink = `https://waterdata.usgs.gov/monitoring-location/${station.siteCode}`;
  const supportsPeakHistory = peakSummary?.streamflowHistory !== undefined || peakSummary?.gageHeightHistory !== undefined;
  const streamflowPeakHistory = peakSummary?.streamflowHistory?.length
    ? peakSummary.streamflowHistory
    : peakSummary?.streamflow ? [peakSummary.streamflow] : [];
  const gageHeightPeakHistory = peakSummary?.gageHeightHistory?.length
    ? peakSummary.gageHeightHistory
    : peakSummary?.gageHeight ? [peakSummary.gageHeight] : [];
  const allPeakWaterYears = [...streamflowPeakHistory, ...gageHeightPeakHistory].map((peak) => peak.waterYear);
  const latestPeakWaterYear = allPeakWaterYears.length > 0 ? Math.max(...allPeakWaterYears) : null;
  const earliestPeakWaterYear = allPeakWaterYears.length > 0 ? Math.min(...allPeakWaterYears) : null;
  const defaultSinceWaterYear = latestPeakWaterYear !== null && earliestPeakWaterYear !== null
    ? Math.max(earliestPeakWaterYear, latestPeakWaterYear - 24)
    : null;
  const effectiveSinceWaterYear = peakSinceWaterYear ?? defaultSinceWaterYear;
  const filteredStreamflowPeaks = filterPeakHistory(
    streamflowPeakHistory,
    peakRangePreset,
    latestPeakWaterYear,
    effectiveSinceWaterYear
  );
  const filteredGageHeightPeaks = filterPeakHistory(
    gageHeightPeakHistory,
    peakRangePreset,
    latestPeakWaterYear,
    effectiveSinceWaterYear
  );
  const displayedStreamflowPeak = supportsPeakHistory
    ? summarizePeakHistory(filteredStreamflowPeaks)
    : peakSummary?.streamflow ?? null;
  const displayedGageHeightPeak = supportsPeakHistory
    ? summarizePeakHistory(filteredGageHeightPeaks)
    : peakSummary?.gageHeight ?? null;
  const effectivePeakCode = selectedPeakCode === "00065" && filteredGageHeightPeaks.length > 0
    ? "00065"
    : filteredStreamflowPeaks.length > 0 ? "00060" : "00065";
  const selectedPeakHistory = effectivePeakCode === "00065" ? filteredGageHeightPeaks : filteredStreamflowPeaks;

  return (
    <div className="stream-gage-details">
      <div style={{ display: "flex", flexWrap: "wrap", gap: "8px 20px", marginBottom: 14, fontSize: 13 }}>
        {drainageArea != null && <span><strong>{drainageLabel}:</strong> {formatNumber(drainageArea, 1)} mi²</span>}
        {approvalStatus && <span><strong>Status:</strong> {approvalStatus}</span>}
        <span><strong>{formatObservationTime(latestObservation)}</strong></span>
      </div>

      {!startDate || !endDate ? (
        <div>Select a custom date range to load station observations.</div>
      ) : detailLoading ? (
        <Spin size="small" />
      ) : detailError ? (
        <div>Stream gage observations are unavailable for this range.</div>
      ) : detailParameters && availableParameters.length === 0 ? (
        <div>No observations were returned for the selected range.</div>
      ) : null}

      {!detailLoading && !detailError && availableParameters.length > 1 && (
        <Segmented
          className="stream-gage-segmented"
          value={selectedParameter?.parameterCode}
          onChange={(value) => setSelectedCode(String(value))}
          options={availableParameters.map((parameter) => ({ label: parameter.parameterCode === "00065" ? "Stage" : "Flow", value: parameter.parameterCode }))}
          style={{ marginBottom: 12 }}
        />
      )}

      {selectedParameter && (
        <>
          <div style={{ display: "flex", flexWrap: "wrap", gap: "8px 20px", marginBottom: 10 }}>
            <span><strong>{selectedParameter.variableName}:</strong> {latest ? formatNumber(latest.value) : "No data"}{selectedParameter.unit ? ` ${formatUnit(selectedParameter.unit)}` : ""}</span>
            {trend && <span><strong>6-hour trend:</strong> {trend}</span>}
            {range && <span><strong>Selected range:</strong> {formatNumber(range.min)}–{formatNumber(range.max)} {formatUnit(selectedParameter.unit)}</span>}
          </div>
          <ParameterChart parameter={selectedParameter} />
        </>
      )}

      <div style={{ marginTop: 18, paddingTop: 14, borderTop: "1px solid var(--border-color)" }}>
        <div style={{ fontWeight: 600, marginBottom: 8 }}>Annual Peak History</div>
        {peakLoading && <Spin size="small" />}
        {peakError && <div>Historical annual peak data is unavailable.</div>}
        {peakSummary && supportsPeakHistory && allPeakWaterYears.length > 0 && (
          <>
            <div style={{ display: "flex", alignItems: "center", gap: 8, flexWrap: "wrap", marginBottom: 10 }}>
              <span className="stream-gage-muted" style={{ fontSize: 12 }}>Range</span>
              <Segmented
                className="stream-gage-segmented"
                aria-label="Historical peak range"
                value={peakRangePreset}
                options={[
                  { label: "All time", value: "all" },
                  { label: "25 yr", value: "25y" },
                  { label: "10 yr", value: "10y" },
                  { label: "5 yr", value: "5y" },
                  { label: "Since…", value: "since" },
                ]}
                onChange={(value) => {
                  const preset = value as PeakRangePreset;
                  setPeakRangePreset(preset);
                  if (preset === "since" && peakSinceWaterYear === null && defaultSinceWaterYear !== null) {
                    setPeakSinceWaterYear(defaultSinceWaterYear);
                  }
                }}
              />
              {peakRangePreset === "since" && (
                <InputNumber
                  aria-label="Peak history starting water year"
                  addonBefore="Water year"
                  min={earliestPeakWaterYear ?? undefined}
                  max={latestPeakWaterYear ?? undefined}
                  value={effectiveSinceWaterYear ?? undefined}
                  onChange={(value) => setPeakSinceWaterYear(typeof value === "number" ? value : null)}
                />
              )}
            </div>
          </>
        )}
        {peakSummary && !displayedStreamflowPeak && !displayedGageHeightPeak && (
          <div>No annual peaks were returned for this range.</div>
        )}
        {peakSummary && (displayedStreamflowPeak || displayedGageHeightPeak) && (
          <div style={{ display: "flex", flexWrap: "wrap", gap: "16px 32px" }}>
            {displayedStreamflowPeak && <HistoricalPeak peak={displayedStreamflowPeak} />}
            {displayedGageHeightPeak && <HistoricalPeak peak={displayedGageHeightPeak} />}
          </div>
        )}
        {peakSummary && supportsPeakHistory && selectedPeakHistory.length > 1 && (
          <div style={{ marginTop: 16 }}>
            <div style={{ display: "flex", alignItems: "center", gap: 8, flexWrap: "wrap" }}>
              {filteredStreamflowPeaks.length > 0 && filteredGageHeightPeaks.length > 0 && (
                <Segmented
                  className="stream-gage-segmented"
                  size="small"
                  aria-label="Historical peak parameter"
                  value={effectivePeakCode}
                  options={[
                    { label: "Flow", value: "00060" },
                    { label: "Stage", value: "00065" },
                  ]}
                  onChange={(value) => setSelectedPeakCode(String(value))}
                />
              )}
            </div>
            <AnnualPeakChart history={selectedPeakHistory} />
          </div>
        )}
      </div>

      <div style={{ marginTop: 14 }}>
        <a href={usgsLink} target="_blank" rel="noopener noreferrer">View station details on USGS</a>
      </div>
    </div>
  );
}

interface GageListProps {
  gages: NearbyStreamGage[];
  loading?: boolean;
  error?: string | null;
  startDate: string | null;
  endDate: string | null;
}

export const GageList: FC<GageListProps> = ({
  gages,
  loading = false,
  error = null,
  startDate,
  endDate,
}) => {
  const [activeKey, setActiveKey] = useState<string | string[]>("");
  if (loading) return <Spin size="large" style={{ margin: "1em" }} />;
  if (error) return <div>{error}</div>;
  if (gages.length === 0) return <div>No gages found in this area.</div>;

  return (
    <Collapse
      className="stream-gage-list"
      accordion
      destroyInactivePanel
      activeKey={activeKey}
      onChange={(key) => setActiveKey(key)}
    >
      {gages.map((station) => {
        const flow = getParameter(station.parameters, "00060");
        const stage = getParameter(station.parameters, "00065");
        const latestFlow = getLatestPoint(flow);
        const latestStage = getLatestPoint(stage);
        const flowTrend = getSixHourTrend(flow);
        const stageTrend = getSixHourTrend(stage);
        const nearestOrigin = getNearestOriginLabel(station.nearestOriginName);
        const latestObservation = getLatestObservation(station.parameters);
        const approvalStatus = getApprovalStatus(station.parameters);
        const active = Array.isArray(activeKey) ? activeKey.includes(station.id) : activeKey === station.id;

        const details = [
          `${station.distanceMiles.toFixed(2)} mi${nearestOrigin ? ` from ${nearestOrigin}` : ""}`,
          latestFlow ? `Flow ${formatNumber(latestFlow.value)}${flow?.unit ? ` ${formatUnit(flow.unit)}` : ""}${flowTrend ? ` ${flowTrend}` : ""}` : null,
          latestStage ? `Stage ${formatNumber(latestStage.value)}${stage?.unit ? ` ${formatUnit(stage.unit)}` : ""}${stageTrend ? ` ${stageTrend}` : ""}` : null,
        ].filter(Boolean).join(" · ");

        const context = [formatObservationTime(latestObservation), approvalStatus].filter(Boolean).join(" · ");
        const header = (
          <div className="stream-gage-header" style={{ lineHeight: 1.35, padding: "2px 0" }}>
            <div style={{ fontWeight: 600 }}>{station.siteName}</div>
            <div className="stream-gage-header__details" style={{ fontSize: 12, marginTop: 2 }}>{details}</div>
            <div className="stream-gage-header__context" style={{ fontSize: 11, marginTop: 2 }}>{context}</div>
          </div>
        );

        return (
          <Panel header={header} key={station.id}>
            <StationDetails
              station={station}
              active={active}
              startDate={startDate}
              endDate={endDate}
            />
          </Panel>
        );
      })}
    </Collapse>
  );
};
