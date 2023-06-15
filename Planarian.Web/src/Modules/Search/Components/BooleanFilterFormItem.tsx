import { Checkbox, Form, Input, Select } from "antd";
import { QueryOperator } from "../Services/QueryBuilder";
import { FilterFormItemProps } from "../Models/NumberComparisonFormItemProps";
import { useState } from "react";

export interface BooleanFilterFormItemProps<T extends object>
  extends FilterFormItemProps<T> {
  opposite?: boolean;
  keyValue?: string;
}
const BooleanFilterFormItem = <T extends object>({
  queryBuilder,
  field,
  label,
  opposite,
  keyValue,
}: BooleanFilterFormItemProps<T>) => {
  let keyVal = keyValue ?? field.toString();

  let [isChecked, setIsChecked] = useState<boolean>(
    queryBuilder.getFieldValue(keyVal.toString()) as boolean
  );
  return (
    <Form.Item name={field.toString()} label={label}>
      <Checkbox
        id={field.toString()}
        checked={isChecked}
        onChange={(e) => {
          if (e.target.checked == false) {
            queryBuilder.removeFromDictionary(keyVal);
            setIsChecked(false);
          } else {
            queryBuilder.filterBy(
              field,
              QueryOperator.Equal,
              opposite
                ? (!e.target.checked as T[keyof T])
                : (e.target.checked as T[keyof T]),
              keyValue
            );
            setIsChecked(true);
          }
        }}
      />
    </Form.Item>
  );
};

export { BooleanFilterFormItem };
