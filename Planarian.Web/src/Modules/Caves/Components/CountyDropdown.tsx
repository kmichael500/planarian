import { SelectProps, Spin, Select } from "antd";
import { useState, useEffect } from "react";
import { SelectListItem } from "../../../Shared/Models/SelectListItem";
import { SettingsService } from "../../Setting/Services/SettingsService";
const { Option } = Select;
export interface CountyDropdownProps extends SelectProps<string> {
  selectedState: string;
}
// Generic County Dropdown Component
const CountyDropdown = ({
  selectedState: selectedStateId,
  defaultValue,
  onSelect,
  ...rest
}: CountyDropdownProps) => {
  const [counties, setCounties] = useState<SelectListItem<string>[]>([]);

  const [isLoading, setIsLoading] = useState(false);
  useEffect(() => {
    if (selectedStateId) {
      SettingsService.GetCounties(selectedStateId).then((data) => {
        setCounties(data);
        setIsLoading(false);
      });
    }
  }, [selectedStateId]);

  return (
    <>
      <Spin spinning={isLoading}>
        {!isLoading && (
          <Select
            id="countyDropdown"
            loading={isLoading}
            placeholder="Select county"
            {...rest}
          >
            {counties.map((county) => (
              <Option key={county.value} value={county.value}>
                {county.display}
              </Option>
            ))}
          </Select>
        )}
      </Spin>
    </>
  );
};

export { CountyDropdown };
