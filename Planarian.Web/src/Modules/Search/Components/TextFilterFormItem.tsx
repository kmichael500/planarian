import { Form, Input, Select } from "antd";
import { QueryOperator } from "../Services/QueryBuilder";
import { FilterFormItemProps } from "../Models/NumberComparisonFormItemProps";
const TextFilterFormItem = <T,>({
  queryBuilder,
  field,
  label,
}: FilterFormItemProps<T>) => {
  return (
    <Form.Item name={field.toString()} label={label}>
      <Input
        onChange={(e) => {
          queryBuilder.filterBy(
            field,
            QueryOperator.Contains,
            e.target.value as T[keyof T]
          );
        }}
      />
    </Form.Item>
  );
};

export { TextFilterFormItem };
