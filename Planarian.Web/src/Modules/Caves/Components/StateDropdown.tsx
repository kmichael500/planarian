import React, { useState, useEffect } from "react";
import { Select, Spin } from "antd";
import { SelectProps } from "antd/lib/select";
import { SelectListItem } from "../../../Shared/Models/SelectListItem";
import { SettingsService } from "../../Setting/Services/SettingsService";
import { isNullOrWhiteSpace } from "../../../Shared/Helpers/StringHelpers";
const { Option } = Select;

export interface StateDropdownProps extends SelectProps<string> {
  onChange?: (value: string) => void;
  autoSelectFirst?: boolean;
}

const StateDropdown = ({
  onChange,
  autoSelectFirst,
  ...rest
}: StateDropdownProps) => {
  const [states, setStates] = useState<SelectListItem<string>[]>([]);
  const [defaultValue, setDefaultValue] = useState<string>("");

  const [isLoading, setIsLoading] = useState(true);

  if (autoSelectFirst === undefined) {
    autoSelectFirst = false;
  }

  useEffect(() => {
    SettingsService.GetStates().then((data) => {
      setIsLoading(true);
      setStates(data);
      if (data.length > 0 && autoSelectFirst) {
        setDefaultValue(data[0].value);

        if (onChange) {
          onChange(data[0].value);
        }
      }
      setIsLoading(false);
    });
  }, []);

  const selectProps = {
    loading: isLoading,
    placeholder: "Select state",
    onChange: (e) => {
      if (onChange) {
        onChange(e);
      }
    },
    ...rest,
  } as SelectProps<string>;

  if (!isNullOrWhiteSpace(defaultValue)) {
    selectProps.defaultValue = defaultValue;
  }

  return (
    <>
      <Spin spinning={isLoading} size="small">
        {!isLoading && (
          <Select {...selectProps} id="stateDropdown">
            {states.map((state) => (
              <Option key={state.value} value={state.value}>
                {state.display}
              </Option>
            ))}
          </Select>
        )}
      </Spin>
    </>
  );
};

export { StateDropdown };
