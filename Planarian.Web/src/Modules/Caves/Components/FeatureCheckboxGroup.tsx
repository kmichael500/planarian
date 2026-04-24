import { CSSProperties, ReactNode } from "react";
import { Checkbox, Collapse } from "antd";
import type { CheckboxOptionType } from "antd/es/checkbox";

interface FeatureCheckboxGroupProps<
  TValue extends string | number | boolean
> {
  title: ReactNode;
  options: CheckboxOptionType<TValue>[];
  value: TValue[];
  onChange: (values: TValue[]) => void | Promise<void>;
  collapsible?: boolean;
  collapsed?: boolean;
  onCollapsedChange?: (collapsed: boolean) => void;
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
  collapsible,
  collapsed,
  onCollapsedChange,
}: FeatureCheckboxGroupProps<TValue>) => {
  const checkboxGroup = (
    <Checkbox.Group
      options={options}
      value={value}
      onChange={(nextValues) => onChange(nextValues as TValue[])}
    />
  );

  if (collapsible) {
    return (
      <Collapse
        className="feature-checkbox-group feature-checkbox-group--compact"
        activeKey={collapsed ? [] : ["content"]}
        onChange={(keys) => {
          onCollapsedChange?.(
            Array.isArray(keys) ? !keys.includes("content") : keys !== "content"
          );
        }}
        items={[
          {
            key: "content",
            label: title,
            children: checkboxGroup,
          },
        ]}
      />
    );
  }

  return (
    <div style={containerStyle}>
      <div style={{ fontWeight: 450 }}>{title}</div>
      {checkboxGroup}
    </div>
  );
};

export { FeatureCheckboxGroup };
