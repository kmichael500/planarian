import { Select, Spin } from "antd";
import { useState, useEffect } from "react";
import { SelectListItem } from "../../../Shared/Models/SelectListItem";
import { SettingsService } from "../../Setting/Services/SettingsService";
import { StateCountyValue } from "../../Account/Models/UserLocationPermissionsVm";

const { Option } = Select;

interface StateCountySelectProps {
  value?: StateCountyValue;
  onChange?: (value: StateCountyValue) => void;

  labelStates?: string;
  labelCounties?: string;
  disabled?: boolean;
}

export const StateCountySelect: React.FC<StateCountySelectProps> = ({
  value,
  onChange,
  labelStates = "Select States",
  labelCounties = "Select Counties",
  disabled = false,
}) => {
  const [internalValue, setInternalValue] = useState<StateCountyValue>({
    states: [],
    countiesByState: {},
  });

  useEffect(() => {
    if (value) {
      setInternalValue(value);
    }
  }, [value]);

  useEffect(() => {
    if (value?.states) {
      value.states.forEach((stateId) => {
        if (!countiesByStateData[stateId]) {
          loadCountiesForState(stateId);
        }
      });
    }
  }, [value]);

  const [allStates, setAllStates] = useState<SelectListItem<string>[]>([]);
  const [statesLoading, setStatesLoading] = useState<boolean>(false);

  const [countiesByStateData, setCountiesByStateData] = useState<
    Record<string, SelectListItem<string>[]>
  >({});

  const [countiesLoading, setCountiesLoading] = useState<
    Record<string, boolean>
  >({});

  useEffect(() => {
    (async () => {
      setStatesLoading(true);
      try {
        const states = await SettingsService.GetStates();
        setAllStates(states);
      } finally {
        setStatesLoading(false);
      }
    })();
  }, []);

  const handleStatesChange = async (selectedStateIds: string[]) => {
    const oldValue = { ...internalValue };
    const newValue: StateCountyValue = {
      states: selectedStateIds,
      countiesByState: { ...oldValue.countiesByState },
    };

    for (const oldStateId of Object.keys(newValue.countiesByState)) {
      if (!selectedStateIds.includes(oldStateId)) {
        delete newValue.countiesByState[oldStateId];
      }
    }

    for (const newStateId of selectedStateIds) {
      if (!countiesByStateData[newStateId]) {
        await loadCountiesForState(newStateId);
      }
      if (!newValue.countiesByState[newStateId]) {
        newValue.countiesByState[newStateId] = [];
      }
    }

    setInternalValue(newValue);
    onChange?.(newValue);
  };

  const handleCountiesChange = (
    stateId: string,
    selectedCountyIds: string[]
  ) => {
    const newValue: StateCountyValue = {
      states: [...internalValue.states],
      countiesByState: { ...internalValue.countiesByState },
    };

    newValue.countiesByState[stateId] = selectedCountyIds;
    setInternalValue(newValue);
    onChange?.(newValue);
  };

  const loadCountiesForState = async (stateId: string) => {
    setCountiesLoading((prev) => ({ ...prev, [stateId]: true }));
    try {
      const counties = await SettingsService.GetCounties(stateId);
      setCountiesByStateData((prev) => ({
        ...prev,
        [stateId]: counties,
      }));
    } finally {
      setCountiesLoading((prev) => ({ ...prev, [stateId]: false }));
    }
  };

  return (
    <div>
      <div style={{ marginBottom: 8 }}>
        <label style={{ display: "block", fontWeight: 500 }}>
          {labelStates}
        </label>
        {statesLoading ? (
          <Spin />
        ) : (
          <Select
            mode="multiple"
            style={{ width: "100%" }}
            disabled={disabled}
            placeholder="Choose State(s)"
            value={internalValue.states}
            showSearch
            filterOption={(input, option) => {
              return option?.props.children
                .toLowerCase()
                .includes(input.toLowerCase());
            }}
            onChange={handleStatesChange}
            allowClear
          >
            {allStates.map((st) => (
              <Option key={st.value} value={st.value}>
                {st.display}
              </Option>
            ))}
          </Select>
        )}
      </div>

      {/* For each selected state, show a Counties Select */}
      {internalValue.states.map((stateId) => {
        const counties = countiesByStateData[stateId] || [];
        const loading = countiesLoading[stateId] || false;

        return (
          <div key={stateId} style={{ marginBottom: 8 }}>
            <label style={{ display: "block", fontWeight: 500 }}>
              {labelCounties} for{" "}
              {allStates.find((s) => s.value === stateId)?.display}
            </label>
            {loading ? (
              <Spin />
            ) : (
              <Select
                mode="multiple"
                style={{ width: "100%" }}
                disabled={disabled}
                placeholder="Choose Counties"
                value={internalValue.countiesByState[stateId] || []}
                onChange={(countyIds) =>
                  handleCountiesChange(stateId, countyIds)
                }
                allowClear
                showSearch
                filterOption={(input, option) => {
                  return option?.props.children
                    .toLowerCase()
                    .includes(input.toLowerCase());
                }}
              >
                {counties.map((cty) => (
                  <Option key={cty.value} value={cty.value}>
                    {cty.display}
                  </Option>
                ))}
              </Select>
            )}
          </div>
        );
      })}
    </div>
  );
};
