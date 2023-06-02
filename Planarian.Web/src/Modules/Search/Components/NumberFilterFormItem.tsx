import { Form, Input, Select } from "antd";
import { QueryOperator } from "../Services/QueryBuilder";
import { FilterFormItemProps } from "./NumberComparisonFormItemProps";
const { Option } = Select;
const NumberFilterFormItem = <T,>({
  queryBuilder,
  field,
  label,
}: FilterFormItemProps<T>) => {
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
          type="number"
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
          type="number"
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
