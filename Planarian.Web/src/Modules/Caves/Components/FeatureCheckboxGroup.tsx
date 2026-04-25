import { CSSProperties, ReactNode } from "react";
import { RightOutlined } from "@ant-design/icons";
import { Checkbox } from "antd";
import type { CheckboxOptionType } from "antd/es/checkbox";
import { ScrollCollapseSection } from "../../../Shared/Components/ScrollCollapseSection/ScrollCollapseSection";
import "./FeatureCheckboxGroup.scss";

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
    const isCollapsed = collapsed ?? false;

    return (
      <div className="feature-checkbox-group feature-checkbox-group--compact">
        <button
          type="button"
          className="feature-checkbox-group__toggle"
          aria-expanded={!isCollapsed}
          onClick={() => onCollapsedChange?.(!isCollapsed)}
        >
          <span className="feature-checkbox-group__toggle-label">{title}</span>
          <RightOutlined
            className={[
              "feature-checkbox-group__toggle-icon",
              isCollapsed ? "" : "feature-checkbox-group__toggle-icon--expanded",
            ]
              .filter(Boolean)
              .join(" ")}
          />
        </button>
        <ScrollCollapseSection
          visible={!isCollapsed}
          className="feature-checkbox-group__content"
        >
          <div className="feature-checkbox-group__content-inner">
            {checkboxGroup}
          </div>
        </ScrollCollapseSection>
      </div>
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
