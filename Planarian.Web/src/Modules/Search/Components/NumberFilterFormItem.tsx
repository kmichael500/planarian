import { Form, Input, Select } from "antd";
import { QueryOperator } from "../Services/QueryBuilder";
import { FilterFormItemProps } from "../Models/NumberComparisonFormItemProps";
import { LiteralUnion } from "antd/lib/_util/type";
const { Option } = Select;

export interface NumberFilterFormItemProps<T> extends FilterFormItemProps<T> {
  inputType:
    | "button"
    | "checkbox"
    | "color"
    | "date"
    | "datetime-local"
    | "email"
    | "file"
    | "hidden"
    | "image"
    | "month"
    | "number"
    | "password"
    | "radio"
    | "range"
    | "reset"
    | "search"
    | "submit"
    | "tel"
    | "text"
    | "time"
    | "url"
    | "week";
}

const NumberFilterFormItem = <T,>({
  queryBuilder,
  field,
  label,
  inputType,
}: NumberFilterFormItemProps<T>) => {
  const getGreaterThanKey = () => {
    return `${field.toString()}GreaterThan`;
  };
  const getGreaterThanDefaultOperator = () => {
    return QueryOperator.GreaterThanOrEqual;
  };
  const getLessThanKey = () => {
    return `${field.toString()}LessThan`;
  };
  const getLessThanDefaultOperator = () => {
    return QueryOperator.LessThanOrEqual;
  };
  return (
    <Form.Item label={label}>
      <div style={{ display: "flex", gap: "8px" }}>
        <Input
          type={inputType}
          defaultValue={
            queryBuilder.getFieldValue(getGreaterThanKey()) as number
          }
          onChange={(e) => {
            const currentOperator = queryBuilder.getOperatorValue(
              getGreaterThanKey(),
              getGreaterThanDefaultOperator()
            );
            queryBuilder.filterBy(
              field,
              currentOperator,
              e.target.value as any,
              getGreaterThanKey()
            );
          }}
        />
        <Select
          defaultValue={queryBuilder.getOperatorValue(
            getGreaterThanKey(),
            getGreaterThanDefaultOperator()
          )}
          onChange={(e) => {
            queryBuilder.changeOperators(field, e, getGreaterThanKey());
          }}
        >
          {" "}
          <Option value={QueryOperator.GreaterThanOrEqual}>
            {QueryOperator.GreaterThanOrEqual}
          </Option>
          <Option value={QueryOperator.GreaterThan}>
            {QueryOperator.GreaterThan}
          </Option>
        </Select>
      </div>
      <br />
      <div style={{ display: "flex", gap: "8px" }}>
        <Input
          type={inputType}
          defaultValue={queryBuilder.getFieldValue(getLessThanKey()) as number}
          onChange={(e) => {
            const currentOperator = queryBuilder.getOperatorValue(
              getLessThanKey(),
              getLessThanDefaultOperator()
            );
            queryBuilder.filterBy(
              field,
              currentOperator,
              e.target.value as any,
              getLessThanKey()
            );
          }}
        />
        <Select
          defaultValue={queryBuilder.getOperatorValue(
            getLessThanKey(),
            getLessThanDefaultOperator()
          )}
          onChange={(e) => {
            queryBuilder.changeOperators(field, e, getLessThanKey());
          }}
        >
          <Option value={QueryOperator.LessThanOrEqual}>
            {QueryOperator.LessThanOrEqual}
          </Option>
          <Option value={QueryOperator.LessThan}>
            {QueryOperator.LessThan}
          </Option>
        </Select>
      </div>
    </Form.Item>
  );
};

export { NumberFilterFormItem as NumberComparisonFormItem };
