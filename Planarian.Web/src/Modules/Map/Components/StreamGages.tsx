import React, { FC, useEffect, useMemo, useState } from "react";
import { Alert, InputNumber, Segmented } from "antd";
import dayjs, { Dayjs } from "dayjs";
import { PlanarianDateRange } from "../../../Shared/Components/Buttons/PlanarianDateRange";
import { PlanarianDividerComponent } from "../../../Shared/Components/PlanarianDivider/PlanarianDividerComponent";
import type { EntranceVm } from "../../Caves/Models/EntranceVm";
import {
  StreamGageSearchOrigin,
  MapService,
  NearbyStreamGage,
} from "../Services/MapService";
import { GageList } from "./GaugeList";
import { formatEntranceDisplayLabel } from "./MapEntranceHelpers";

interface StreamGagesProps {
  lat?: number;
  lng?: number;
  caveName?: string;
  entrances?: EntranceVm[];
  showDivider?: boolean;
  originLabel?: string;
}

type StreamGageRangePreset = "24h" | "7d" | "30d" | "custom";
const MAX_CUSTOM_OBSERVATION_DAYS = 90;

export const StreamGages: FC<StreamGagesProps> = ({
  lat,
  lng,
  caveName,
  entrances,
  showDivider = true,
  originLabel = "Location",
}) => {
  const [pendingDistanceMiles, setPendingDistanceMiles] = useState(25);
  const [distanceMiles, setDistanceMiles] = useState(25);
  const [rangePreset, setRangePreset] = useState<StreamGageRangePreset>("7d");
  const [dateRange, setDateRange] = useState<[Dayjs | null, Dayjs | null]>([
    dayjs().subtract(7, "day"),
    dayjs(),
  ]);
  const [gages, setGages] = useState<NearbyStreamGage[]>([]);
  const [loadingGages, setLoadingGages] = useState(false);
  const [gageError, setGageError] = useState<string | null>(null);

  const searchOrigins = useMemo<StreamGageSearchOrigin[]>(() => {
    if (caveName && entrances?.length) {
      return entrances
        .filter(
          (entrance) =>
            Number.isFinite(entrance.latitude) && Number.isFinite(entrance.longitude)
        )
        .map((entrance, index) => ({
          id: entrance.id || `entrance-${index}`,
          name: formatEntranceDisplayLabel(
            caveName,
            entrance.name,
            entrance.isPrimary
          ),
          latitude: entrance.latitude,
          longitude: entrance.longitude,
        }));
    }

    if (Number.isFinite(lat) && Number.isFinite(lng)) {
      return [
        {
          id: "location",
          name: originLabel,
          latitude: lat as number,
          longitude: lng as number,
        },
      ];
    }

    return [];
  }, [caveName, entrances, lat, lng, originLabel]);

  const selectRangePreset = (preset: StreamGageRangePreset) => {
    setRangePreset(preset);
    if (preset === "custom") return;

    const now = dayjs();
    const days = preset === "24h" ? 1 : preset === "7d" ? 7 : 30;
    setDateRange([now.subtract(days, "day"), now]);
  };

  const customRangeTooLong = rangePreset === "custom" && Boolean(
    dateRange[0] &&
    dateRange[1] &&
    dateRange[1].endOf("day").diff(dateRange[0].startOf("day"), "day", true) > MAX_CUSTOM_OBSERVATION_DAYS
  );

  const selectedRange = useMemo(() => ({
    startDate: customRangeTooLong
      ? null
      : rangePreset === "custom"
      ? dateRange[0]?.startOf("day").toISOString() ?? null
      : dateRange[0]?.toISOString() ?? null,
    endDate: customRangeTooLong
      ? null
      : rangePreset === "custom"
      ? dateRange[1]?.endOf("day").toISOString() ?? null
      : dateRange[1]?.toISOString() ?? null,
  }), [customRangeTooLong, dateRange, rangePreset]);

  useEffect(() => {
    const timer = setTimeout(() => setDistanceMiles(pendingDistanceMiles), 500);
    return () => clearTimeout(timer);
  }, [pendingDistanceMiles]);

  useEffect(() => {
    if (searchOrigins.length === 0 || distanceMiles <= 0) {
      setGages([]);
      return;
    }

    let cancelled = false;
    setLoadingGages(true);
    setGageError(null);

    MapService.getNearbyStreamGages(
      searchOrigins,
      distanceMiles
    )
      .then((result) => {
        if (!cancelled) setGages(result);
      })
      .catch((error) => {
        console.error("Unable to load nearby stream gages", error);
        if (!cancelled) {
          setGages([]);
          setGageError("Unable to load nearby USGS stream gages.");
        }
      })
      .finally(() => {
        if (!cancelled) setLoadingGages(false);
      });

    return () => {
      cancelled = true;
    };
  }, [searchOrigins, distanceMiles]);

  if (searchOrigins.length === 0) {
    return (
      <Alert
        type="warning"
        showIcon
        message="No valid location is available for hydrology search."
      />
    );
  }

  return (
    <>
      {showDivider && (
        <PlanarianDividerComponent
          title="Stream Gages"
          secondaryTitle="from USGS NWIS"
        />
      )}
      <div
        style={{
          display: "flex",
          alignItems: "center",
          gap: 8,
          flexWrap: "wrap",
          marginTop: 10,
          marginBottom: 12,
        }}
      >
        <InputNumber
          aria-label="Stream gage search radius"
          addonBefore="Within"
          addonAfter="Miles"
          min={1}
          max={50}
          value={pendingDistanceMiles}
          onChange={(value) => {
            if (typeof value === "number") setPendingDistanceMiles(value);
          }}
        />
        <span style={{ fontSize: 12, color: "var(--muted-text-color)" }}>Observation history</span>
        <Segmented
          aria-label="Stream gage time range"
          value={rangePreset}
          options={[
            { label: "24h", value: "24h" },
            { label: "7d", value: "7d" },
            { label: "30d", value: "30d" },
            { label: "Custom", value: "custom" },
          ]}
          onChange={(value) => selectRangePreset(value as StreamGageRangePreset)}
        />
        {rangePreset === "custom" && (
          <PlanarianDateRange
            value={dateRange}
            onChange={(range) => setDateRange(range || [null, null])}
          />
        )}
      </div>
      {customRangeTooLong && (
        <Alert
          type="warning"
          message={`Custom observation ranges are limited to ${MAX_CUSTOM_OBSERVATION_DAYS} days.`}
          showIcon
          style={{ marginBottom: 8 }}
        />
      )}
      {gageError && (
        <Alert
          type="error"
          message={gageError}
          showIcon
          style={{ marginBottom: 8 }}
        />
      )}
      <GageList
        gages={gages}
        loading={loadingGages}
        startDate={selectedRange.startDate}
        endDate={selectedRange.endDate}
      />
    </>
  );
};
