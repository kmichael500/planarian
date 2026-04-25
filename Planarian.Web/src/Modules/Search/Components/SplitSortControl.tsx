import { Button, Dropdown, MenuProps } from "antd";
import {
  BarsOutlined,
  DownOutlined,
  SortAscendingOutlined,
  SortDescendingOutlined,
} from "@ant-design/icons";
import { SelectListItem } from "../../../Shared/Models/SelectListItem";
import "./SplitSortControl.scss";

interface SplitSortControlProps {
  sortOptions: SelectListItem<string>[];
  selectedValue?: string;
  isDescending: boolean;
  onSelect: (value: string) => Promise<void> | void;
  onToggleDirection: () => Promise<void> | void;
  compact?: boolean;
}

const SplitSortControl: React.FC<SplitSortControlProps> = ({
  sortOptions,
  selectedValue,
  isDescending,
  onSelect,
  onToggleDirection,
  compact = false,
}) => {
  const selectedOption =
    sortOptions.find((option) => option.value === selectedValue) ??
    sortOptions[0];

  const menuItems: MenuProps["items"] = sortOptions.map((option) => ({
    key: option.value,
    label: option.display,
  }));

  return (
    <div className="planarian-split-sort">
      <Dropdown
        menu={{
          items: menuItems,
          onClick: ({ key }) => {
            void onSelect(key);
          },
        }}
        overlayClassName="planarian-split-sort__menu planarian-dropdown--touch"
        trigger={["click"]}
      >
        <Button
          className={[
            "planarian-split-sort__field",
            compact ? "planarian-split-sort__field--compact" : undefined,
          ]
            .filter(Boolean)
            .join(" ")}
          title={selectedOption?.display ?? "Sort"}
          type="default"
        >
          <BarsOutlined className="planarian-split-sort__field-icon" />
          <span className="planarian-split-sort__field-label">
            {selectedOption?.display ?? "Sort"}
          </span>
          <DownOutlined className="planarian-split-sort__caret" />
        </Button>
      </Dropdown>
      <Button
        aria-label={isDescending ? "Sort descending" : "Sort ascending"}
        className={[
          "planarian-split-sort__direction",
          compact ? "planarian-split-sort__direction--compact" : undefined,
        ]
          .filter(Boolean)
          .join(" ")}
        onClick={() => {
          void onToggleDirection();
        }}
        title={isDescending ? "Descending" : "Ascending"}
        type="default"
      >
        {isDescending ? (
          <SortDescendingOutlined className="planarian-split-sort__direction-icon" />
        ) : (
          <SortAscendingOutlined className="planarian-split-sort__direction-icon" />
        )}
      </Button>
    </div>
  );
};

export { SplitSortControl };
