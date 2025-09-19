import { DatePicker, Form, Input, Select, Space } from "antd";
import { QueryOperator } from "../Services/QueryBuilder";
import { FilterFormItemProps } from "../Models/NumberComparisonFormItemProps";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
import { ClearOutlined } from "@ant-design/icons";
import { useEffect, useState } from "react";
import dayjs from "dayjs";

const { Option } = Select;

export interface NumberFilterFormItemProps<T extends object>
  extends FilterFormItemProps<T> {
  onFiltersCleared?: number;
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
  onFiltersCleared: onFiltersCleared,
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
  const [inputValue1, setInputValue1] = useState<string>(
    () =>
      (queryBuilder.getFieldValue(getGreaterThanKey()) as string | undefined) ??
      ""
  );
  const [inputValue2, setInputValue2] = useState<string>(
    () =>
      (queryBuilder.getFieldValue(getLessThanKey()) as string | undefined) ??
      ""
  );

  const [operatorValue1, setOperatorValue1] = useState<QueryOperator>(
    getGreaterThanDefaultOperator()
  );
  const [operatorValue2, setOperatorValue2] = useState<QueryOperator>(
    getLessThanDefaultOperator()
  );

  useEffect(() => {
    setInputValue1(
      (queryBuilder.getFieldValue(getGreaterThanKey()) as string | undefined) ??
      ""
    );
    setInputValue2(
      (queryBuilder.getFieldValue(getLessThanKey()) as string | undefined) ??
      ""
    );
    setOperatorValue1(
      queryBuilder.getOperatorValue(
        getGreaterThanKey(),
        getGreaterThanDefaultOperator()
      )
    );
    setOperatorValue2(
      queryBuilder.getOperatorValue(
        getLessThanKey(),
        getLessThanDefaultOperator()
      )
    );
  }, [onFiltersCleared]);

  const onClear = () => {
    queryBuilder.removeFromDictionary(getGreaterThanKey());
    queryBuilder.removeFromDictionary(getLessThanKey());
    setInputValue1("");
    setInputValue2("");
    setOperatorValue1(getGreaterThanDefaultOperator());
    setOperatorValue2(getLessThanDefaultOperator());
  };

  const handleValueChange = (
    rawValue: string,
    key: string,
    defaultOperator: QueryOperator,
    setValue: (value: string) => void
  ) => {
    setValue(rawValue ?? "");
    const currentOperator = queryBuilder.getOperatorValue(key, defaultOperator);

    if (!rawValue) {
      queryBuilder.removeFromDictionary(key);

      queryBuilder.changeOperators(field, defaultOperator, key);
    } else {
      queryBuilder.filterBy(field, currentOperator, rawValue as any, key);
    }
  };

  const renderInput = (
    value: string,
    key: string,
    defaultOperator: QueryOperator,
    setValue: (value: string) => void
  ) => {
    if (inputType === "date") {
      return (
        <DatePicker
          allowClear
          style={{ width: 180 }}
          value={value ? dayjs(value, "YYYY-MM-DD") : null}
          format="YYYY-MM-DD"
          onChange={(date) => {
            const formatted = date.format("YYYY-MM-DD");
            handleValueChange(formatted, key, defaultOperator, setValue);
          }}
        />
      );
    }

    return (
      <Input
        id={`${field.toString()}-${key}`}
        min={inputType === "number" ? 0 : undefined}
        allowClear
        type={inputType}
        value={value}
        onChange={(e) => {
          handleValueChange(
            e.target.value,
            key,
            defaultOperator,
            setValue
          );
        }}
        style={{ minWidth: 160 }}
      />
    );
  };

  return (
    <Form.Item label={label}>
      <Space direction="vertical" size={8} style={{ width: "100%" }}>
        <Space align="center" size={8} wrap>
          {renderInput(
            inputValue1,
            getGreaterThanKey(),
            getGreaterThanDefaultOperator(),
            setInputValue1
          )}
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
        </Space>

        <Space align="center" size={8} wrap>
          {renderInput(
            inputValue2,
            getLessThanKey(),
            getLessThanDefaultOperator(),
            setInputValue2
          )}
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
          <PlanarianButton
            icon={<ClearOutlined />}
            onClick={onClear}
            type="default"
          >
            Clear
          </PlanarianButton>
        </Space>
      </Space>
    </Form.Item>
  );
};

export { NumberFilterFormItem as NumberComparisonFormItem };
