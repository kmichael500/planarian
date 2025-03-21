import React, { useState, useEffect } from "react";
import { Select, Spin, Tag } from "antd";
import { SelectListItem } from "../../../Shared/Models/SelectListItem";
import { SettingsService } from "../../Setting/Services/SettingsService";
import { StateCountyValue } from "../../Account/Models/UserLocationPermissionsVm";
import { PermissionKey } from "../../Authentication/Models/PermissionKey";

const { Option } = Select;

interface StateCountySelectProps {
  value?: StateCountyValue;
  onChange?: (value: StateCountyValue) => void;
  labelStates?: string;
  labelCounties?: string;
  disabled?: boolean;
  permissionKey?: PermissionKey;
}

// For each state, we store both the permitted counties (for the dropdown)
// and the full list of counties (for tag rendering and comparisons)
interface CountyData {
  permitted: SelectListItem<string>[];
  all: SelectListItem<string>[];
}

export const StateCountySelect: React.FC<StateCountySelectProps> = ({
  value,
  onChange,
  labelStates = "Select States",
  labelCounties = "Select Counties",
  disabled = false,
  permissionKey,
}) => {
  const [internalValue, setInternalValue] = useState<StateCountyValue>({
    states: [],
    countiesByState: {},
  });

  // Update internal value when the prop changes
  useEffect(() => {
    if (value) {
      setInternalValue(value);
    }
  }, [value]);

  // Ensure counties are loaded for any selected state
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

  // For each state, store both permitted and full county lists.
  const [countiesByStateData, setCountiesByStateData] = useState<
    Record<string, CountyData>
  >({});

  const [countiesLoading, setCountiesLoading] = useState<
    Record<string, boolean>
  >({});

  useEffect(() => {
    (async () => {
      setStatesLoading(true);
      try {
        const states = await SettingsService.GetStates(permissionKey);
        setAllStates(states);
      } finally {
        setStatesLoading(false);
      }
    })();
  }, [permissionKey]);

  /**
   * Updated handleStatesChange:
   * - For each previously selected state that is no longer in the permitted selection:
   *    - If that state's counties include any non-permitted county, keep the state but clear out only the permitted counties.
   *    - Otherwise (if all counties are permitted), remove the state entirely.
   * - Also add any newly selected state.
   */
  const handleStatesChange = async (selectedStateIds: string[]) => {
    const oldValue = { ...internalValue };
    const newCountiesByState: Record<string, string[]> = {
      ...oldValue.countiesByState,
    };
    const newStateSelection: string[] = [];

    // Process states that were previously selected.
    for (const stateId of oldValue.states) {
      if (selectedStateIds.includes(stateId)) {
        // State is still selected from the permitted dropdown.
        newStateSelection.push(stateId);
      } else {
        // State was removed from the dropdown.
        const countyData = countiesByStateData[stateId];
        if (countyData) {
          const permittedCountyIds = countyData.permitted.map(
            (cty) => cty.value
          );
          const currentCounties = oldValue.countiesByState[stateId] || [];
          // Remove permitted counties from the current selection.
          const remainingCounties = currentCounties.filter(
            (countyId) => !permittedCountyIds.includes(countyId)
          );
          if (remainingCounties.length > 0) {
            // If there is at least one non-permitted county, keep the state
            // (with only the non-permitted counties).
            newStateSelection.push(stateId);
            newCountiesByState[stateId] = remainingCounties;
          } else {
            // Otherwise, remove the state entirely.
            delete newCountiesByState[stateId];
          }
        } else {
          // If no county data is available, remove the state.
          delete newCountiesByState[stateId];
        }
      }
    }

    // Process new states added via the dropdown.
    for (const stateId of selectedStateIds) {
      if (!oldValue.states.includes(stateId)) {
        newStateSelection.push(stateId);
        if (!newCountiesByState[stateId]) {
          newCountiesByState[stateId] = [];
        }
        if (!countiesByStateData[stateId]) {
          await loadCountiesForState(stateId);
        }
      }
    }

    // Remove duplicates if any.
    const finalStates = Array.from(new Set(newStateSelection));

    const newValue: StateCountyValue = {
      states: finalStates,
      countiesByState: newCountiesByState,
    };

    setInternalValue(newValue);
    onChange?.(newValue);
  };

  /**
   * For the counties select:
   * Merge the newly selected permitted counties with any non-permitted counties that were already selected.
   */
  const handleCountiesChange = (
    stateId: string,
    selectedCountyIds: string[]
  ) => {
    const countyData = countiesByStateData[stateId];
    const previousCounties = internalValue.countiesByState[stateId] || [];
    const nonPermitted = countyData
      ? previousCounties.filter(
          (id) => !countyData.permitted.some((cty) => cty.value === id)
        )
      : [];
    const finalSelected = [
      ...selectedCountyIds,
      ...nonPermitted.filter((id) => !selectedCountyIds.includes(id)),
    ];

    const newValue: StateCountyValue = {
      states: [...internalValue.states],
      countiesByState: {
        ...internalValue.countiesByState,
        [stateId]: finalSelected,
      },
    };
    setInternalValue(newValue);
    onChange?.(newValue);
  };

  /**
   * Load counties for a given state.
   * When a permissionKey is provided, we fetch both the permitted list (for the dropdown)
   * and the full list (for rendering tag labels and comparisons).
   */
  const loadCountiesForState = async (stateId: string) => {
    setCountiesLoading((prev) => ({ ...prev, [stateId]: true }));
    try {
      const permittedCounties = await SettingsService.GetCounties(
        stateId,
        permissionKey
      );
      const allCounties =
        permissionKey && typeof SettingsService.GetCounties === "function"
          ? await SettingsService.GetCounties(stateId, null)
          : permittedCounties;

      setCountiesByStateData((prev) => ({
        ...prev,
        [stateId]: {
          permitted: permittedCounties,
          all: allCounties,
        },
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
            filterOption={(input, option) =>
              option?.props.children.toLowerCase().includes(input.toLowerCase())
            }
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

      {/* For each selected state, render a counties select */}
      {internalValue.states.map((stateId) => {
        const countyData = countiesByStateData[stateId];
        const permittedCounties = countyData ? countyData.permitted : [];
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
                filterOption={(input, option) =>
                  option?.props.children
                    .toLowerCase()
                    .includes(input.toLowerCase())
                }
                tagRender={(props) => {
                  const { label, value, closable, onClose } = props;
                  const isPermitted = permittedCounties.some(
                    (cty) => cty.value === value
                  );
                  // Use the full list to render the proper display value.
                  const displayValue = countyData?.all.find(
                    (cty) => cty.value === value
                  )?.display;
                  return (
                    <Tag
                      closable={isPermitted ? closable : false}
                      onClose={isPermitted ? onClose : undefined}
                    >
                      {displayValue}
                    </Tag>
                  );
                }}
              >
                {permittedCounties.map((cty) => (
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
