import { Form, Input, Select } from "antd";
import { QueryOperator } from "../Services/QueryBuilder";
import {
  FilterFormItemProps,
  FilterFormProps,
} from "../Models/NumberComparisonFormItemProps";

export interface TextFilterFormItemProps<T extends object>
  extends FilterFormItemProps<T> {
  queryOperator?: QueryOperator;
}

const TextFilterFormItem = <T extends object>({
  queryBuilder,
  field,
  label,
  queryOperator,
}: TextFilterFormItemProps<T>) => {
  queryOperator = queryOperator ?? QueryOperator.Contains;
  return (
    <Form.Item name={field.toString()} label={label}>
      <Input
        id={field.toString()}
        onChange={(e) => {
          queryBuilder.filterBy(
            field,
            queryOperator as QueryOperator,
            e.target.value as T[keyof T]
          );
        }}
      />
    </Form.Item>
  );
};

export { TextFilterFormItem };
