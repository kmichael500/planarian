import { Form, Input, Tooltip } from "antd";
import { InfoCircleOutlined } from "@ant-design/icons";
import { QueryOperator } from "../Services/QueryBuilder";
import {
  FilterFormItemProps,
  FilterFormProps,
} from "../Models/NumberComparisonFormItemProps";

export interface TextFilterFormItemProps<T extends object>
  extends FilterFormItemProps<T> {
  queryOperator?: QueryOperator;
  helpText?: string | React.ReactNode;
}

const TextFilterFormItem = <T extends object>({
  queryBuilder,
  field,
  label,
  queryOperator,
  helpText,
}: TextFilterFormItemProps<T>) => {
  queryOperator = queryOperator ?? QueryOperator.Contains;

  const labelWithHelp = helpText ? (
    <span>
      {label}{" "}
      <Tooltip title={helpText}>
        <InfoCircleOutlined style={{ color: "#1890ff" }} />
      </Tooltip>
    </span>
  ) : (
    label
  );

  return (
    <Form.Item name={field.toString()} label={labelWithHelp}>
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
