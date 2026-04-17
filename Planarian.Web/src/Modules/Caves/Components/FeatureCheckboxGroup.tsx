import { CSSProperties } from "react";
import { Checkbox } from "antd";
import type { CheckboxOptionType } from "antd/es/checkbox";

interface FeatureCheckboxGroupProps<
  TValue extends string | number | boolean
> {
  title: string;
  options: CheckboxOptionType<TValue>[];
  value: TValue[];
  onChange: (values: TValue[]) => void | Promise<void>;
}

const containerStyle: CSSProperties = {
  borderRadius: "2px",
  padding: "10px",
  border: "1px solid var(--header-border-color, #d9d9d9)",
  backgroundColor: "var(--background-color)",
};

const FeatureCheckboxGroup = <TValue extends string | number | boolean>({
  title,
  options,
  value,
  onChange,
}: FeatureCheckboxGroupProps<TValue>) => {
  return (
    <div style={containerStyle}>
      <div style={{ fontWeight: 450 }}>{title}</div>
      <Checkbox.Group
        options={options}
        value={value}
        onChange={(nextValues) => onChange(nextValues as TValue[])}
      />
    </div>
  );
};

export { FeatureCheckboxGroup };
