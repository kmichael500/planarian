import { SelectProps, Spin, Select } from "antd";
import { useState, useEffect } from "react";
import { SelectListItem } from "../../../Shared/Models/SelectListItem";
import { SettingsService } from "../../Setting/Services/SettingsService";
import { sortSelectListItems } from "../../../Shared/Helpers/ArrayHelpers";
const { Option } = Select;
export interface CountyDropdownProps extends SelectProps<string> {
  selectedStateId: string;
}
const CountyDropdown = ({
  selectedStateId,
  defaultValue,
  onSelect,
  ...rest
}: CountyDropdownProps) => {
  const [counties, setCounties] = useState<SelectListItem<string>[]>([]);

  const [isLoading, setIsLoading] = useState(false);
  useEffect(() => {
    SettingsService.GetCounties(selectedStateId).then((data) => {
      setCounties(data);
      setIsLoading(false);
    });
  }, [selectedStateId]);

  const sortedCounties = sortSelectListItems(counties);

  return (
    <>
      <Spin spinning={isLoading}>
        {!isLoading && counties.length > 0 && (
          <Select
            id="countyDropdown"
            loading={isLoading}
            showSearch
            placeholder="Select county"
            {...rest}
            filterOption={(input, option) => {
              return option?.props.children
                .toLowerCase()
                .includes(input.toLowerCase());
            }}
          >
            {sortedCounties.map((county) => (
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
