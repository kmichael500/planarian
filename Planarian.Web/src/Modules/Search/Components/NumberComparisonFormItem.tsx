import { Form, Input, Select } from "antd";
import { QueryOperator } from "../Services/QueryBuilder";
import { FilterFormItemProps } from "./NumberComparisonFormItemProps";
const { Option } = Select;
const NumberFilterFormItem = <T,>({
  queryBuilder,
  field,
  label,
}: FilterFormItemProps<T>) => {
  return (
    <Form.Item label={label}>
      <div style={{ display: "flex", gap: "8px" }}>
        <Input
          type="number"
          defaultValue={
            queryBuilder.getFieldValue(
              `${field.toString()}GreaterThan`
            ) as number
          }
          onChange={(e) => {
            const currentOperator = queryBuilder.getOperatorValue(
              `${field.toString()}GreaterThan`,
              QueryOperator.GreaterThanOrEqual
            );
            queryBuilder.filterBy(
              field,
              currentOperator,
              e.target.value as any,
              `${field.toString()}GreaterThan`
            );
          }}
        />
        <Select
          defaultValue={queryBuilder.getOperatorValue(
            `${field.toString()}GreaterThan`,
            QueryOperator.GreaterThanOrEqual
          )}
          onChange={(e) => {
            queryBuilder.changeOperators(
              field,
              e,
              `${field.toString()}GreaterThan`
            );
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
          defaultValue={
            queryBuilder.getFieldValue(`${field.toString()}LessThan`) as number
          }
          onChange={(e) => {
            const currentOperator = queryBuilder.getOperatorValue(
              `${field.toString()}LessThan`,
              QueryOperator.LessThanOrEqual
            );
            queryBuilder.filterBy(
              field,
              currentOperator,
              e.target.value as any,
              `${field.toString()}LessThan`
            );
          }}
        />
        <Select
          defaultValue={queryBuilder.getOperatorValue(
            "numberOfPhotosLessThan",
            QueryOperator.LessThanOrEqual
          )}
          onChange={(e) => {
            queryBuilder.changeOperators(
              field,
              e,
              `${field.toString()}LessThan`
            );
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
