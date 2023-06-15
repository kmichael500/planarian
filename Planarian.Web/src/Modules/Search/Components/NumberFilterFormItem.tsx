import { Form, Input, InputRef, Select } from "antd";
import { QueryOperator } from "../Services/QueryBuilder";
import { FilterFormItemProps } from "../Models/NumberComparisonFormItemProps";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
import { ClearOutlined } from "@ant-design/icons";
import { useRef, useState } from "react";

const { Option } = Select;

export interface NumberFilterFormItemProps<T extends object>
  extends FilterFormItemProps<T> {
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

const NumberFilterFormItem = <T extends object>({
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
  const [inputValue1, setInputValue1] = useState<string | undefined>(
    queryBuilder.getFieldValue(getGreaterThanKey()) as string | undefined
  );
  const [inputValue2, setInputValue2] = useState<string | undefined>(
    queryBuilder.getFieldValue(getLessThanKey()) as string | undefined
  );

  const [operatorValue1, setOperatorValue1] = useState<QueryOperator>(
    getGreaterThanDefaultOperator()
  );
  const [operatorValue2, setOperatorValue2] = useState<QueryOperator>(
    getLessThanDefaultOperator()
  );

  const onClear = (): void => {
    queryBuilder.removeFromDictionary(getGreaterThanKey());
    queryBuilder.removeFromDictionary(getLessThanKey());

    setInputValue1(undefined);
    setInputValue2(undefined);

    setOperatorValue1(getGreaterThanDefaultOperator());
    setOperatorValue2(getLessThanDefaultOperator());
  };

  return (
    <Form.Item label={label}>
      <div style={{ display: "flex", gap: "8px" }}>
        <Input
          id={field.toString() + " field 1"}
          min={0}
          allowClear
          type={inputType}
          value={inputValue1}
          onChange={(e) => {
            const value = e.target.value;
            setInputValue1(value);
            const currentOperator = queryBuilder.getOperatorValue(
              getGreaterThanKey(),
              getGreaterThanDefaultOperator()
            );
            queryBuilder.filterBy(
              field,
              currentOperator,
              value as any,
              getGreaterThanKey()
            );
          }}
        />
        <Select
          value={operatorValue1}
          onChange={(e) => {
            queryBuilder.changeOperators(field, e, getGreaterThanKey());
            setOperatorValue1(e);
          }}
        >
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
          min={0}
          id={field.toString() + " field 2"}
          allowClear
          type={inputType}
          value={inputValue2}
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
            setInputValue2(e.target.value);
          }}
        />
        <Select
          value={operatorValue2}
          onChange={(e) => {
            queryBuilder.changeOperators(field, e, getLessThanKey());
            setOperatorValue2(e);
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
