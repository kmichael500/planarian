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

var queryBuilder = new QueryBuilder<CaveSearchParamsVm>("", false);
export const CaveSearchMapControl = () => {
  const [options, setOptions] = useState<
    { value: string; label: string; cave: CaveSearchVm | null }[]
  >([]);
  const map = useMap();

  const [isLoading, setIsLoading] = useState(false);

  const handleSearch = useCallback(async (value: string) => {
    if (value) {
      try {
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

        // Check if there are results, if not, set a "No results" option
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
    if (option.cave) {
      const { cave } = option;
      const entrance = cave.primaryEntranceLatitude;
      if (cave.primaryEntranceLatitude && cave.primaryEntranceLongitude) {
        map.current?.flyTo({
          center: [cave.primaryEntranceLongitude, cave.primaryEntranceLatitude],
          zoom: 16,
        });
      }
    }
  };

  return (
    <ControlPanel>
      <AutoComplete
        options={options}
        onSelect={handleSelect}
        onSearch={handleSearch}
      >
        <Input.Search
          loading={isLoading}
          size="large"
          placeholder="Search for a cave"
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
