import React, { FC, useEffect, useState } from "react";
import { Collapse, Grid, Spin } from "antd";
import dayjs, { Dayjs } from "dayjs"; // use dayjs instead of moment
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

// Register Chart.js components
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

interface USGSValue {
  value: string; // e.g. "9370"
  dateTime: string; // e.g. "2025-03-28T11:00:00.000-05:00"
}

interface ParameterData {
  variableName: string;
  points: USGSValue[];
}

interface StationGroup {
  siteCode: string;
  siteName: string;
  distanceMiles: number;
  parameters: ParameterData[];
}

// Helper: convert degrees to radians
function toRadians(deg: number) {
  return (deg * Math.PI) / 180;
}

// Helper: Haversine distance
function getDistanceMiles(
  lat1: number,
  lon1: number,
  lat2: number,
  lon2: number
) {
  const R = 3959; // Earth radius in miles
  const dLat = toRadians(lat2 - lat1);
  const dLon = toRadians(lon2 - lon1);
  const a =
    Math.sin(dLat / 2) ** 2 +
    Math.cos(toRadians(lat1)) *
      Math.cos(toRadians(lat2)) *
      Math.sin(dLon / 2) ** 2;
  const c = 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a));
  return R * c;
}

// Helper: bounding box for ±(distanceMiles/2) around (lat, lon)
function getBoundingBox(
  centerLat: number,
  centerLon: number,
  distanceMiles: number
) {
  const latMilesPerDegree = 69;
  const halfSide = distanceMiles;
  const deltaLat = halfSide / latMilesPerDegree;
  const cosLat = Math.cos((centerLat * Math.PI) / 180);
  const lonMilesPerDegree = cosLat === 0 ? 999999 : 69 * cosLat;
  const deltaLon = halfSide / lonMilesPerDegree;
  const minLat = centerLat - deltaLat;
  const maxLat = centerLat + deltaLat;
  const minLon = centerLon - deltaLon;
  const maxLon = centerLon + deltaLon;
  const round7 = (val: number) => parseFloat(val.toFixed(7));
  return [round7(minLon), round7(minLat), round7(maxLon), round7(maxLat)];
}

/** Single parameter line chart. */
function ParameterChart({
  paramName,
  dataPoints,
}: {
  paramName: string;
  dataPoints: USGSValue[];
}) {
  const screens = Grid.useBreakpoint();

  if (!dataPoints || dataPoints.length === 0) {
    return <div>No data available</div>;
  }

  // For mobile, optionally reduce number of points for readability
  let displayPoints = dataPoints;
  if (!screens.md && dataPoints.length > 50) {
    const sampleRate = Math.ceil(dataPoints.length / 50);
    displayPoints = dataPoints.filter((_, idx) => idx % sampleRate === 0);
  }

  const chartData = {
    datasets: [
      {
        label: paramName,
        data: displayPoints.map((dp) => ({
          x: dp.dateTime,
          y: parseFloat(dp.value),
        })),
        fill: false,
        tension: 0.4,
        pointRadius: 0,
      },
    ],
  };

  const options = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: {
      legend: {
        display: screens.sm,
        position: "top" as const,
        labels: {
          boxWidth: screens.sm ? 40 : 20,
          font: {
            size: screens.sm ? 12 : 10,
          },
        },
      },
      title: { display: false },
      tooltip: {
        titleFont: {
          size: screens.sm ? 12 : 10,
        },
        bodyFont: {
          size: screens.sm ? 12 : 10,
        },
      },
    },
    scales: {
      x: {
        type: "time" as const,
        ticks: {
          maxRotation: screens.sm ? 45 : 0,
          font: {
            size: screens.sm ? 10 : 8,
          },
          maxTicksLimit: screens.sm ? 10 : 5,
        },
        time: {
          unit: screens.md ? ("day" as const) : ("month" as const),
        },
      },
      y: {
        type: "linear" as const,
        title: {
          display: screens.sm,
          text: paramName,
          font: {
            size: screens.sm ? 12 : 10,
          },
        },
        ticks: {
          font: {
            size: screens.sm ? 10 : 8,
          },
        },
      },
    },
  };

  return (
    <div style={{ height: screens.md ? "300px" : "200px", width: "100%" }}>
      <Line data={chartData} options={options} />
    </div>
  );
}

/** Props for GageList – gets lat/lng plus user-chosen distance and date range. */
interface GageListProps {
  lat: number;
  lng: number;
  distanceMiles: number;
  dateRange: [Dayjs | null, Dayjs | null];
}

export const GageList: FC<GageListProps> = ({
  lat,
  lng,
  distanceMiles,
  dateRange,
}) => {
  const [gages, setGages] = useState<StationGroup[]>([]);
  const [loadingGages, setLoadingGages] = useState(false);
  const [errorGages, setErrorGages] = useState<string | null>(null);
  const screens = Grid.useBreakpoint();

  // Helper for extracting latest value of a parameter from station data
  function getLatestValue(parameters: ParameterData[], searchTerm: string) {
    const param = parameters.find((p) =>
      p.variableName.toLowerCase().includes(searchTerm)
    );
    if (!param || param.points.length === 0)
      return { value: "No data", unit: "" };
    const lastVal = param.points[param.points.length - 1].value;
    // Extract unit from variable name (e.g., "Streamflow, ft³/s" -> "ft³/s")
    const unitMatch = param.variableName.match(/,\s*([^,]+)$/);
    const unit = unitMatch ? unitMatch[1].trim() : "";
    return { value: lastVal || "No data", unit };
  }

  useEffect(() => {
    // Skip if distanceMiles <= 0 or no lat/lng
    if (!lat || !lng || distanceMiles <= 0) return;

    const fetchGages = async () => {
      setLoadingGages(true);
      setErrorGages(null);

      try {
        // Build bounding box from lat/lng
        const [minLon, minLat, maxLon, maxLat] = getBoundingBox(
          lat,
          lng,
          distanceMiles
        );

        // Construct NWIS URL
        let url = `https://waterservices.usgs.gov/nwis/iv?format=json`;
        url += `&bBox=${minLon},${minLat},${maxLon},${maxLat}`;
        url += `&parameterCd=00060,00065`; // flow + gage height

        // If user selected date range, use startDT/endDT; else default to last 7 days
        const [start, end] = dateRange ?? [null, null];
        if (start && end) {
          const startStr = start.format("YYYY-MM-DDT00:00:00");
          const endStr = end.format("YYYY-MM-DDT23:59:59");
          url += `&startDT=${startStr}&endDT=${endStr}`;
        } else {
          url += `&period=P7D`;
        }

        const resp = await fetch(url);
        if (!resp.ok) {
          throw new Error(`Gage fetch failed: ${resp.statusText}`);
        }
        const data = await resp.json();

        if (!data?.value?.timeSeries?.length) {
          setGages([]);
          setErrorGages("No gages found in this area.");
          setLoadingGages(false);
          return;
        }

        // Convert each timeSeries item, compute distance
        const timeSeriesWithDistance = data.value.timeSeries.map((ts: any) => {
          // Convert "ft&#179;" => "ft³"
          const fixedVarName = ts.variable.variableName.replace(/&#179;/g, "³");
          const stationLat = ts.sourceInfo.geoLocation.geogLocation.latitude;
          const stationLon = ts.sourceInfo.geoLocation.geogLocation.longitude;
          const dist = getDistanceMiles(lat, lng, stationLat, stationLon);
          return {
            ...ts,
            distanceMiles: dist,
            variable: { ...ts.variable, variableName: fixedVarName },
          };
        });

        // Group by siteCode
        const stationMap: Record<string, StationGroup> = {};
        timeSeriesWithDistance.forEach((ts: any) => {
          const siteCode = ts.sourceInfo.siteCode[0].value;
          const siteName = ts.sourceInfo.siteName;
          const dist = ts.distanceMiles ?? Infinity;

          if (!stationMap[siteCode]) {
            stationMap[siteCode] = {
              siteCode,
              siteName,
              distanceMiles: dist,
              parameters: [],
            };
          } else {
            // If multiple timeSeries for one site, keep the minimal distance
            if (dist < stationMap[siteCode].distanceMiles) {
              stationMap[siteCode].distanceMiles = dist;
            }
          }

          const points = ts.values?.[0]?.value || [];
          stationMap[siteCode].parameters.push({
            variableName: ts.variable.variableName,
            points,
          });
        });

        // Sort by nearest first
        const stationGroups = Object.values(stationMap);
        stationGroups.sort((a, b) => a.distanceMiles - b.distanceMiles);
        setGages(stationGroups);
      } catch (err) {
        console.error(err);
        setErrorGages("Error fetching USGS gage data.");
      }
      setLoadingGages(false);
    };

    fetchGages();
  }, [lat, lng, distanceMiles, dateRange]);

  if (loadingGages) {
    return <Spin size="large" style={{ margin: "1em" }} />;
  } else if (errorGages) {
    return <div>{errorGages}</div>;
  } else if (gages.length === 0) {
    return <div>No gages found in this area.</div>;
  }

  return (
    <Collapse accordion>
      {gages.map((station) => {
        // Grab the latest flow + gage height for the collapse header
        const flow = getLatestValue(station.parameters, "streamflow");
        const height = getLatestValue(station.parameters, "gage height");

        const header = `${station.siteName} — ${station.distanceMiles.toFixed(
          2
        )} mi | Flow: ${flow.value} ${flow.unit} | Gage: ${height.value} ${
          height.unit
        }`;
        const siteCode = station.siteCode;
        const usgsLink = `https://waterdata.usgs.gov/monitoring-location/${siteCode}`;

        return (
          <Panel header={header} key={station.siteCode}>
            <div style={{ marginBottom: "1em" }}>
              <a href={usgsLink} target="_blank" rel="noopener noreferrer">
                View station details on USGS site
              </a>
            </div>
            <div
              style={{
                display: "flex",
                flexWrap: "wrap",
                gap: "20px",
                width: "100%",
                overflow: "hidden",
              }}
            >
              {station.parameters.map((param, idx) => {
                // Get latest value for this parameter
                const latestPoint =
                  param.points.length > 0
                    ? param.points[param.points.length - 1]
                    : null;
                const latestValue = latestPoint ? latestPoint.value : "No data";

                return (
                  <div
                    key={idx}
                    style={{
                      marginBottom: "1.5em",
                      flex: screens.md ? "1 1 45%" : "1 1 100%",
                      maxWidth: screens.md ? "calc(50% - 10px)" : "100%",
                    }}
                  >
                    <div style={{ fontWeight: "bold", marginBottom: "0.3em" }}>
                      {param.variableName}: {latestValue}
                    </div>
                    <ParameterChart
                      paramName={param.variableName}
                      dataPoints={param.points}
                    />
                  </div>
                );
              })}
            </div>
          </Panel>
        );
      })}
    </Collapse>
  );
};
