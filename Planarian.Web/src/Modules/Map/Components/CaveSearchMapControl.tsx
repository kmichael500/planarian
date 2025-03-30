import { useState, useCallback } from "react";
import { AutoComplete, Input, message } from "antd";
import { useMap } from "react-map-gl";
import styled from "styled-components";
import { CaveService } from "../../Caves/Service/CaveService";
import {
  QueryBuilder,
  QueryOperator,
} from "../../Search/Services/QueryBuilder";
import { CaveSearchParamsVm } from "../../Caves/Models/CaveSearchParamsVm";
import { ApiErrorResponse } from "../../../Shared/Models/ApiErrorResponse";
import { CaveSearchVm } from "../../Caves/Models/CaveSearchVm";

let queryBuilder = new QueryBuilder<CaveSearchParamsVm>("", false);

export const CaveSearchMapControl = () => {
  const [options, setOptions] = useState<
    { value: string; label: string; cave: CaveSearchVm | null }[]
  >([]);
  const [inputValue, setInputValue] = useState("");
  const [isLoading, setIsLoading] = useState(false);
  // State to store the candidate coordinate (if any)
  const [coordinateCandidate, setCoordinateCandidate] = useState<{
    lat: number;
    lon: number;
  } | null>(null);
  const map = useMap();

  const handleSearch = useCallback(async (value: string) => {
    setInputValue(value);

    // Check if input is in the format "lat, lon"
    if (value.includes(",")) {
      const parts = value.split(",");
      if (parts.length === 2) {
        const lat = parseFloat(parts[0].trim());
        const lon = parseFloat(parts[1].trim());
        if (!isNaN(lat) && !isNaN(lon)) {
          // Store the coordinate candidate and update options
          setCoordinateCandidate({ lat, lon });
          setOptions([
            {
              value: `coordinate:${lat},${lon}`,
              label: `Go to [${lat}, ${lon}]`,
              cave: null,
            },
          ]);
          return;
        }
      }
    }

    // Clear any coordinate candidate if the input isn't a valid coordinate
    setCoordinateCandidate(null);

    if (value) {
      try {
        // Reset the query builder to avoid stacking filters
        queryBuilder = new QueryBuilder<CaveSearchParamsVm>("", false);
        queryBuilder = queryBuilder.filterBy(
          "name",
          QueryOperator.Contains,
          value
        );
        setIsLoading(true);
        const caves = await CaveService.GetCaves(queryBuilder);
        const formattedOptions = caves.results.map((cave) => ({
          value: cave.id,
          label: `${cave.displayId} ${cave.name}`,
          cave: cave,
        }));
        setIsLoading(false);

        if (formattedOptions.length === 0) {
          setOptions([
            { value: "no_results", label: "No results", cave: null },
          ]);
        } else {
          setOptions(formattedOptions);
        }
      } catch (e) {
        const error = e as ApiErrorResponse;
        message.error(error.message);
      }
    } else {
      setOptions([]);
    }
  }, []);

  const handleSelect = (
    value: string,
    option: { value: string; label: string; cave: CaveSearchVm | null }
  ) => {
    // Check if the selected option is the coordinate candidate
    if (value.startsWith("coordinate:")) {
      const coords = value.split(":")[1];
      const [lat, lon] = coords.split(",").map((v) => parseFloat(v.trim()));
      if (!isNaN(lat) && !isNaN(lon)) {
        map.current?.flyTo({
          center: [lon, lat],
          zoom: 16,
        });
        message.success(`Navigating to [${lat}, ${lon}]`);
      }
    } else if (option.cave) {
      const { cave } = option;
      if (cave.primaryEntranceLatitude && cave.primaryEntranceLongitude) {
        map.current?.flyTo({
          center: [cave.primaryEntranceLongitude, cave.primaryEntranceLatitude],
          zoom: 16,
        });
      }
    }
    // Clear the search input and drop-down options after selection
    setInputValue("");
    setOptions([]);
    setCoordinateCandidate(null);
  };

  // Handler for when the user presses Enter
  const handlePressEnter = () => {
    if (coordinateCandidate) {
      map.current?.flyTo({
        center: [coordinateCandidate.lon, coordinateCandidate.lat],
        zoom: 16,
      });
      message.success(
        `Navigating to [${coordinateCandidate.lat}, ${coordinateCandidate.lon}]`
      );
      setInputValue("");
      setOptions([]);
      setCoordinateCandidate(null);
    }
  };

  return (
    <ControlPanel>
      <AutoComplete
        options={options}
        onSelect={handleSelect}
        onSearch={handleSearch}
        value={inputValue}
        onChange={setInputValue}
      >
        <Input.Search
          loading={isLoading}
          size="large"
          placeholder="Search for a cave or enter coordinates (lat, lon)"
          onPressEnter={handlePressEnter}
        />
      </AutoComplete>
    </ControlPanel>
  );
};

const ControlPanel = styled.div`
  position: absolute;
  top: 20px;
  right: 10px;
  width: 250px;
`;
