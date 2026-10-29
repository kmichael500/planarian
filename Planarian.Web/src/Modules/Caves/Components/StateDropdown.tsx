import { useState, useEffect, useRef } from "react";
import { Select, Spin } from "antd";
import { SelectProps } from "antd/lib/select";
import { SelectListItem } from "../../../Shared/Models/SelectListItem";
import { SettingsService } from "../../Setting/Services/SettingsService";
import { isNullOrWhiteSpace } from "../../../Shared/Helpers/StringHelpers";
import { PermissionKey } from "../../Authentication/Models/PermissionKey";
const { Option } = Select;

export interface StateDropdownProps extends SelectProps<string> {
  onChange?: (value: string) => void;
  autoSelectFirst?: boolean;
  permissionKey?: PermissionKey;
}

const StateDropdown = ({
  onChange,
  autoSelectFirst,
  permissionKey,
  ...rest
}: StateDropdownProps) => {
  const [states, setStates] = useState<SelectListItem<string>[]>([]);
  const [defaultValue, setDefaultValue] = useState<string>("");
  const [isLoading, setIsLoading] = useState(true);
  const onChangeRef = useRef(onChange);
  onChangeRef.current = onChange;
  const shouldAutoSelectFirst = autoSelectFirst ?? false;

  useEffect(() => {
    let isCurrent = true;
    setIsLoading(true);

    SettingsService.GetStates(permissionKey)
      .then((data) => {
        if (!isCurrent) return;

        setStates(data);
        setDefaultValue("");
        if (data.length === 1 && shouldAutoSelectFirst) {
          setDefaultValue(data[0].value);
          onChangeRef.current?.(data[0].value);
        }
      })
      .finally(() => {
        if (isCurrent) {
          setIsLoading(false);
        }
      });

    return () => {
      isCurrent = false;
    };
  }, [permissionKey, shouldAutoSelectFirst]);

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
