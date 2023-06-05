import React, { useState, useEffect } from "react";
import { Select, Spin } from "antd";
import { SelectProps } from "antd/lib/select";
import { SelectListItem } from "../../../Shared/Models/SelectListItem";
import { SettingsService } from "../../Setting/Services/SettingsService";
const { Option } = Select;

interface StateDropdownProps extends SelectProps<string> {
  onChange?: (value: string) => void;
}
// Generic State Dropdown Component
const StateDropdown = ({ onChange, ...rest }: StateDropdownProps) => {
  const [states, setStates] = useState<SelectListItem<string>[]>([]);
  const [defaultValue, setDefaultValue] = useState<string>("");

  const [isLoading, setIsLoading] = useState(true);
  useEffect(() => {
    // Fetch states from API

    SettingsService.GetStates().then((data) => {
      setIsLoading(true);
      setStates(data);
      if (data.length > 0) {
        setDefaultValue(data[0].value);
        if (onChange) {
          onChange(data[0].value);
        }
      }
      setIsLoading(false);
    });
  }, []);

  return (
    <>
      <Spin spinning={isLoading} size="small">
        {!isLoading && (
          <Select
            loading={isLoading}
            placeholder="Select state"
            defaultValue={defaultValue}
            onChange={(e) => {
              if (onChange) {
                onChange(e);
              }
            }}
            {...rest}
          >
            {states.map((state) => (
              <Option key={state.value} value={state.value}>
                {state.display}
              </Option>
            ))}
          </Select>
        )}{" "}
      </Spin>
    </>
  );
};

export { StateDropdown };
