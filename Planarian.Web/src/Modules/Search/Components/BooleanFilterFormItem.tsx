import { Checkbox, Form, Input, Select } from "antd";
import { QueryOperator } from "../Services/QueryBuilder";
import { FilterFormItemProps } from "../Models/NumberComparisonFormItemProps";

export interface BooleanFilterFormItemProps<T> extends FilterFormItemProps<T> {
  opposite?: boolean;
  key?: string;
}
const BooleanFilterFormItem = <T,>({
  queryBuilder,
  field,
  label,
  opposite,
  key,
}: BooleanFilterFormItemProps<T>) => {
  let keyValue = key ?? field.toString();
  return (
    <Form.Item name={field.toString()} label={label}>
      <Checkbox
        defaultChecked={
          queryBuilder.getFieldValue(keyValue.toString()) as boolean
        }
        onChange={(e) => {
          console.log("Here");

          if (e.target.checked == false) {
            queryBuilder.removeFromDictionary(keyValue);
          } else {
            queryBuilder.filterBy(
              field,
              QueryOperator.Equal,
              opposite
                ? (!e.target.checked as T[keyof T])
                : (e.target.checked as T[keyof T]),
              keyValue
            );
          }
        }}
      />
    </Form.Item>
  );
};

export { BooleanFilterFormItem };
