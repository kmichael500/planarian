import React, { useState, useEffect } from "react";
import { Select, Spin } from "antd";
import { SelectListItem } from "../../../Shared/Models/SelectListItem";
import { SettingsService } from "../../Setting/Services/SettingsService";
import { StateCountyValue } from "../../Account/Models/UserLocationPermissionsVm";
import { PermissionKey } from "../../Authentication/Models/PermissionKey";
import { PlanarianTag } from "../../../Shared/Components/Display/PlanarianTag";

const { Option } = Select;

interface StateCountySelectProps {
  value?: StateCountyValue;
  onChange?: (value: StateCountyValue) => void;
  labelStates?: string;
  labelCounties?: string;
  disabled?: boolean;
  permissionKey?: PermissionKey;
}

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

  // Preserve selected counties outside the user's permitted set when changing states.
  const handleStatesChange = async (selectedStateIds: string[]) => {
    const oldValue = { ...internalValue };
    const newCountiesByState: Record<string, string[]> = {
      ...oldValue.countiesByState,
    };
    const newStateSelection: string[] = [];

    for (const stateId of oldValue.states) {
      if (selectedStateIds.includes(stateId)) {
        newStateSelection.push(stateId);
      } else {
        const countyData = countiesByStateData[stateId];
        if (countyData) {
          const permittedCountyIds = countyData.permitted.map(
            (cty) => cty.value
          );
          const currentCounties = oldValue.countiesByState[stateId] || [];
          const remainingCounties = currentCounties.filter(
            (countyId) => !permittedCountyIds.includes(countyId)
          );
          if (remainingCounties.length > 0) {
            newStateSelection.push(stateId);
            newCountiesByState[stateId] = remainingCounties;
          } else {
            delete newCountiesByState[stateId];
          }
        } else {
          delete newCountiesByState[stateId];
        }
      }
    }

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

    const finalStates = Array.from(new Set(newStateSelection));

    const newValue: StateCountyValue = {
      states: finalStates,
      countiesByState: newCountiesByState,
    };

    setInternalValue(newValue);
    onChange?.(newValue);
  };

  // Preserve selected counties the current user cannot modify.
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

  // Keep the full county list for labels while restricting editable options by permission.
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
                  const { value, closable, onClose } = props;
                  const isPermitted = permittedCounties.some(
                    (cty) => cty.value === value
                  );
                  const displayValue = countyData?.all.find(
                    (cty) => cty.value === value
                  )?.display;
                  return (
                    <PlanarianTag
                      closable={isPermitted ? closable : false}
                      onClose={isPermitted ? onClose : undefined}
                    >
                      {displayValue}
                    </PlanarianTag>
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
