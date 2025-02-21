import { Select, Spin } from "antd";
import { useState, useEffect } from "react";
import { SelectListItem } from "../../../Shared/Models/SelectListItem";
import { SettingsService } from "../../Setting/Services/SettingsService";

const { Option } = Select;

interface StateCountySelectProps {
  /** Current value, for controlled usage (Ant Form integration) */
  value?: StateCountyValue;
  /** Fires whenever the user updates state or county selections */
  onChange?: (value: StateCountyValue) => void;

  /** Optional placeholders/labels/etc. */
  labelStates?: string;
  labelCounties?: string;
  disabled?: boolean;
}

// StateCountyValue.ts
export interface StateCountyValue {
  /** List of state IDs the user selected */
  states: string[];
  /**
   * For each state ID, an array of county IDs
   * the user selected
   */
  countiesByState: Record<string, string[]>;
}

/**
 * Allows multiple selection of States,
 * and for each selected State, multiple Counties.
 */
export const StateCountySelect: React.FC<StateCountySelectProps> = ({
  value,
  onChange,
  labelStates = "Select States",
  labelCounties = "Select Counties",
  disabled = false,
}) => {
  // Keep an internal state, which can be either
  // controlled or uncontrolled depending on the props.
  const [internalValue, setInternalValue] = useState<StateCountyValue>({
    states: [],
    countiesByState: {},
  });

  // Merge in the "value" prop if using as a controlled component.
  useEffect(() => {
    if (value) {
      setInternalValue(value);
    }
  }, [value]);

  // All states for the top-level Select
  const [allStates, setAllStates] = useState<SelectListItem<string>[]>([]);
  const [statesLoading, setStatesLoading] = useState<boolean>(false);

  // A dictionary of stateId -> array of counties
  const [countiesByStateData, setCountiesByStateData] = useState<
    Record<string, SelectListItem<string>[]>
  >({});

  // Loading flags for counties
  const [countiesLoading, setCountiesLoading] = useState<
    Record<string, boolean>
  >({});

  /** Fetch states on mount */
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

  /**
   * Called when user changes the states selection.
   * We fetch county data for any newly added states,
   * and remove county data for any unselected states.
   */
  const handleStatesChange = async (selectedStateIds: string[]) => {
    const oldValue = { ...internalValue };
    const newValue: StateCountyValue = {
      states: selectedStateIds,
      countiesByState: { ...oldValue.countiesByState },
    };

    // Remove county selections for states no longer selected
    for (const oldStateId of Object.keys(newValue.countiesByState)) {
      if (!selectedStateIds.includes(oldStateId)) {
        delete newValue.countiesByState[oldStateId];
      }
    }

    // For newly selected states that we haven't fetched yet, fetch counties
    for (const newStateId of selectedStateIds) {
      if (!countiesByStateData[newStateId]) {
        // fetch once
        await loadCountiesForState(newStateId);
      }
      // Make sure we have a record for this state in countiesByState
      if (!newValue.countiesByState[newStateId]) {
        newValue.countiesByState[newStateId] = [];
      }
    }

    setInternalValue(newValue);
    onChange?.(newValue);
  };

  /**
   * Called when user changes the counties for a specific state.
   * We just update that one state's counties in the overall data.
   */
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

  /**
   * Helper to fetch counties for a single state, store in state
   */
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
      {/* States Select */}
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
                placeholder="Choose County(ies)"
                value={internalValue.countiesByState[stateId] || []}
                onChange={(countyIds) =>
                  handleCountiesChange(stateId, countyIds)
                }
                allowClear
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
