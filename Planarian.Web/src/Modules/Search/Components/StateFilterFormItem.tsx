import { useEffect, useState } from "react";
import { NestedKeyOf } from "../../../Shared/Helpers/StringHelpers";
import {
  StateDropdownProps,
  StateDropdown,
} from "../../Caves/Components/StateDropdown";
import { QueryBuilder, QueryOperator } from "../Services/QueryBuilder";
import { Form } from "antd";
import {
  CountyDropdownProps,
  CountyDropdown,
} from "../../Caves/Components/CountyDropdown";
import { PermissionKey } from "../../Authentication/Models/PermissionKey";

interface StateCountyFilterFormItem<T extends object>
  extends StateDropdownProps {
  stateField: NestedKeyOf<T>;
  stateLabel: string;
  countyField: NestedKeyOf<T>;
  countyLabel: string;

  queryBuilder: QueryBuilder<T>;
}

const StateCountyFilterFormItem = <T extends object>({
  stateField,
  stateLabel,
  countyField,
  countyLabel,
  queryBuilder,
  onChange,
  ...rest
}: StateCountyFilterFormItem<T>) => {
  const [selectedCountyId, setSelectedCountyId] = useState<
    string | undefined
  >();
  const [selectedState, setSelectedState] = useState<string | undefined>();

  const stateDropDownProps = {
    ...rest,
    allowClear: true,
    value: selectedState,
    onClear: () => {
      queryBuilder.removeFromDictionary(stateField);
      setSelectedState(undefined);
      setSelectedCountyId(undefined);
    },
    onChange: (value) => {
      if (value !== undefined) {
        queryBuilder.filterBy(stateField, QueryOperator.Contains, value as any);
        setSelectedState(value);
      } else {
        setSelectedState(undefined);
        setSelectedCountyId(undefined);
        queryBuilder.removeFromDictionary(stateField);
        queryBuilder.removeFromDictionary(countyField);
      }
    },
  } as StateDropdownProps;

  useEffect(() => {
    // Set default values on component load
    const stateValue = queryBuilder.getFieldValue(stateField);
    const countyValue = queryBuilder.getFieldValue(countyField);
    if (stateValue === undefined) return;
    setSelectedState(stateValue as string);
    if (countyValue === undefined) return;
    setSelectedCountyId(countyValue as string);
  }, []);

  return (
    <>
      <Form.Item label={stateLabel} fieldId={stateField}>
        <StateDropdown {...stateDropDownProps} />
      </Form.Item>
      <Form.Item label={countyLabel} name={countyField.toString()}>
        {selectedState && (
          <CountyDropdown
            permissionKey={PermissionKey.View}
            selectedStateId={selectedState}
            allowClear={true}
            onClear={() => {
              queryBuilder.removeFromDictionary(countyField);
            }}
            onChange={(value) => {
              if (value !== undefined) {
                queryBuilder.filterBy(
                  countyField,
                  QueryOperator.Contains,
                  value as any
                );
              }
            }}
          />
        )}
      </Form.Item>
    </>
  );
};

export { StateCountyFilterFormItem };
