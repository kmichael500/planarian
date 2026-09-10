import React, { useCallback, useEffect, useMemo, useState } from "react";
import { ClearOutlined, SlidersOutlined } from "@ant-design/icons";
import { Form, message } from "antd";
import styled from "styled-components";
import { CaveAdvancedSearchDrawer } from "../../Caves/Components/CaveAdvancedSearchDrawer";
import type { CaveSearchParamsVm } from "../../Caves/Models/CaveSearchParamsVm";
import type { AdvancedSearchInlineControlsContext } from "../../Search/Components/AdvancedSearchDrawerComponent";
import {
  applyEntranceLocationFilterToQuery,
  EntranceLocationFilter,
  parseEntranceLocationFilter,
} from "../../Search/Helpers/EntranceLocationFilterHelpers";
import { QueryBuilder } from "../../Search/Services/QueryBuilder";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
import type { NestedKeyOf } from "../../../Shared/Helpers/StringHelpers";

interface ExploreMapFiltersProps {
  onQueryChange: (query: string) => void;
}

export const ExploreMapFilters: React.FC<ExploreMapFiltersProps> = ({ onQueryChange }) => {
  const queryBuilder = useMemo(
    () => new QueryBuilder<CaveSearchParamsVm>(window.location.search.substring(1), false),
    []
  );
  const [form] = Form.useForm<CaveSearchParamsVm>();
  const [appliedQuery, setAppliedQuery] = useState(() =>
    queryBuilder.hasFilters() ? queryBuilder.buildAsQueryString() : ""
  );
  const [hasAppliedFilters, setHasAppliedFilters] = useState(queryBuilder.hasFilters());
  const [filterClearSignal, setFilterClearSignal] = useState(0);
  const [polygonResetSignal, setPolygonResetSignal] = useState(0);
  const [isFetchingLocation, setIsFetchingLocation] = useState(false);
  const [entranceLocationFilter, setEntranceLocationFilter] = useState<EntranceLocationFilter>(() =>
    parseEntranceLocationFilter(queryBuilder.getFieldValue("entranceLocation") as string | undefined)
  );

  useEffect(() => onQueryChange(appliedQuery), [appliedQuery, onQueryChange]);

  const applyLocationFilter = useCallback(
    (next: EntranceLocationFilter) => {
      setEntranceLocationFilter(next);
      applyEntranceLocationFilterToQuery(
        queryBuilder,
        "entranceLocation" as NestedKeyOf<CaveSearchParamsVm>,
        next
      );
    },
    [queryBuilder]
  );

  const handleLocationChange = useCallback(
    (field: keyof EntranceLocationFilter, rawValue: number | string | null) => {
      const numericValue = rawValue === null || rawValue === "" ? undefined : Number(rawValue);
      applyLocationFilter({
        ...entranceLocationFilter,
        [field]: Number.isFinite(numericValue) ? numericValue : undefined,
      });
    },
    [applyLocationFilter, entranceLocationFilter]
  );

  const useCurrentLocation = useCallback(() => {
    if (!navigator.geolocation) {
      message.error("Geolocation is not supported in this browser.");
      return;
    }
    setIsFetchingLocation(true);
    navigator.geolocation.getCurrentPosition(
      ({ coords }) => {
        applyLocationFilter({
          latitude: coords.latitude,
          longitude: coords.longitude,
          radius: entranceLocationFilter.radius ?? 5,
        });
        setIsFetchingLocation(false);
      },
      (error) => {
        message.error(error.message || "Unable to fetch current location.");
        setIsFetchingLocation(false);
      },
      { enableHighAccuracy: true, timeout: 15000, maximumAge: 0 }
    );
  }, [applyLocationFilter, entranceLocationFilter.radius]);

  const clearFilters = useCallback(() => {
    applyLocationFilter({});
    setFilterClearSignal((value) => value + 1);
    setPolygonResetSignal((value) => value + 1);
  }, [applyLocationFilter]);

  const runSearch = useCallback(async () => {
    setEntranceLocationFilter(
      parseEntranceLocationFilter(queryBuilder.getFieldValue("entranceLocation") as string | undefined)
    );
    const nextQuery = queryBuilder.hasFilters() ? queryBuilder.buildAsQueryString() : "";
    const urlParams = new URLSearchParams(window.location.search);

    new URLSearchParams(appliedQuery).forEach((_, key) => urlParams.delete(key));
    new URLSearchParams(nextQuery).forEach((value, key) => urlParams.set(key, value));

    const updatedSearch = urlParams.toString();
    window.history.replaceState({}, "", `${window.location.pathname}${updatedSearch ? `?${updatedSearch}` : ""}`);
    setHasAppliedFilters(queryBuilder.hasFilters());
    setAppliedQuery(nextQuery);
  }, [appliedQuery, queryBuilder]);

  const renderInlineControls = useCallback(
    (context: AdvancedSearchInlineControlsContext<CaveSearchParamsVm>) => (
      <FloatingPanel className="planarian-map-control">
        <PlanarianButton
          icon={<SlidersOutlined />}
          onClick={() => context.openDrawer()}
          tooltip="Advanced search"
          neverShowChildren
          type={hasAppliedFilters ? "primary" : "default"}
        />
        {hasAppliedFilters && (
          <PlanarianButton
            icon={<ClearOutlined />}
            onClick={(event) => {
              event.preventDefault();
              void context.clearFilters();
            }}
            tooltip="Clear filters"
            neverShowChildren
          />
        )}
      </FloatingPanel>
    ),
    [hasAppliedFilters]
  );

  return (
    <div className="planarian-map-control">
      <CaveAdvancedSearchDrawer
        onSearch={runSearch}
        queryBuilder={queryBuilder}
        form={form}
        onFiltersCleared={clearFilters}
        inlineControls={renderInlineControls}
        entranceLocationFilter={entranceLocationFilter}
        isFetchingEntranceLocation={isFetchingLocation}
        onEntranceLocationChange={handleLocationChange}
        onUseCurrentLocation={useCurrentLocation}
        onClearEntranceLocation={() => applyLocationFilter({})}
        polygonResetSignal={polygonResetSignal}
        filterClearSignal={filterClearSignal}
      />
    </div>
  );
};

const FloatingPanel = styled.div`
  position: absolute;
  top: 100px;
  right: 0;
  z-index: 100;
  margin: 20px;
  padding: 8px;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 8px;
  background: var(--surface-color);
  color: var(--text-color);
  border: 1px solid var(--border-color);
  border-radius: 8px;
  box-shadow: 0 2px 4px rgba(0, 0, 0, 0.3);
`;
