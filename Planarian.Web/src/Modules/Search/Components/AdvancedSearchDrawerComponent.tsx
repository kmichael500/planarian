import {
  Button,
  Drawer,
  Form,
  FormInstance,
  Input,
  Dropdown,
  MenuProps,
  Popconfirm,
} from "antd";
import { FilterFormProps } from "../Models/NumberComparisonFormItemProps";
import { QueryOperator, QueryBuilder } from "../Services/QueryBuilder";
import { ReactNode, useEffect, useState } from "react";
import {
  SlidersOutlined,
  ClearOutlined,
  DownloadOutlined,
  SearchOutlined,
} from "@ant-design/icons";
import { NestedKeyOf } from "../../../Shared/Helpers/StringHelpers";
import { SelectListItem } from "../../../Shared/Models/SelectListItem";
import { ShouldDisplay } from "../../../Shared/Permissioning/Components/ShouldDisplay";
import { PermissionKey } from "../../Authentication/Models/PermissionKey";
import { ToolbarMetric } from "../../../Shared/Components/Toolbar/ResponsiveToolbar";
import { SplitSortControl } from "./SplitSortControl";
import "./AdvancedSearchDrawerComponent.scss";

const SEARCH_TOOLBAR_BREAKPOINT_PX = 720;

export interface AdvancedSearchDrawerComponentProps<T extends object>
  extends FilterFormProps<T> {
  onSearch: () => Promise<void>;
  children?: React.ReactNode;
  mainSearchField: NestedKeyOf<T>;
  mainSearchFieldLabel: string;
  form?: FormInstance<T>;
  sortOptions?: SelectListItem<string>[];
  onExportGpx?: () => Promise<void>;
  onExportCsv?: () => Promise<void>;
  onFiltersCleared?: () => void;
  onSortChange?: (sortBy: string) => Promise<void> | void;
  toolbarMetrics?: ToolbarMetric[];
  hidePersistentContentOnMobile?: boolean;
  inlineControls?: (
    context: AdvancedSearchInlineControlsContext<T>
  ) => React.ReactNode;
}

export interface AdvancedSearchInlineControlsContext<T extends object> {
  defaultControls: ReactNode;
  openDrawer: () => void;
  clearFilters: () => Promise<void>;
  onSearch: () => Promise<void>;
  setMainSearchValue: (value: string) => void;
  mainSearchValue: string;
  mainSearchFieldLabel: string;
  hasFilters: boolean;
  queryBuilder: QueryBuilder<T>;
}

const AdvancedSearchDrawerComponent = <T extends object>({
  queryBuilder,
  onSearch,
  children,
  mainSearchField,
  mainSearchFieldLabel,
  form,
  sortOptions,
  onExportGpx,
  onExportCsv,
  onFiltersCleared,
  onSortChange,
  toolbarMetrics,
  hidePersistentContentOnMobile = false,
  inlineControls,
}: AdvancedSearchDrawerComponentProps<T>) => {
  const [isBelowToolbarBreakpoint, setIsBelowToolbarBreakpoint] = useState(
    window.innerWidth < SEARCH_TOOLBAR_BREAKPOINT_PX
  );
  const [mainSearchValue, setMainSearchValue] = useState(
    ((queryBuilder.getFieldValue(mainSearchField) as string) ?? "")
  );
  const [isAdvancedSearchOpen, setIsAdvancedSearchOpen] = useState(false);

  useEffect(() => {
    const updateBreakpoint = () => {
      setIsBelowToolbarBreakpoint(
        window.innerWidth < SEARCH_TOOLBAR_BREAKPOINT_PX
      );
    };

    window.addEventListener("resize", updateBreakpoint);
    return () => window.removeEventListener("resize", updateBreakpoint);
  }, []);
  const onClickSearch = async () => {
    setIsAdvancedSearchOpen(false);
    await onSearch();
  };

  const onClearSearch = async () => {
    queryBuilder.clear();
    setMainSearchValue("");
    form?.resetFields();
    onFiltersCleared?.();
    await onSearch();
  };

  const handleMainSearchChange = (value: string) => {
    setMainSearchValue(value);
    queryBuilder.filterBy(
      mainSearchField,
      QueryOperator.Contains,
      value as any
    );
  };

  const handleSortOptionChange = async (value: string) => {
    if (onSortChange) {
      await onSortChange(value);
      return;
    }

    queryBuilder.setSort(value);
    await onSearch();
  };

  const handleSortDirectionToggle = async () => {
    queryBuilder.setSortDescending(!queryBuilder.getSortDescending());
    await onSearch();
  };

  const shouldShowPersistentContent =
    !hidePersistentContentOnMobile || !isBelowToolbarBreakpoint;

  const defaultControls = (
    <div className="planarian-search-toolbar">
      <div className="planarian-search-toolbar__primary">
        <div className="planarian-search-toolbar__embedded" role="search">
          <Input
            bordered={false}
            className="planarian-search-toolbar__embedded-input"
            placeholder={mainSearchFieldLabel}
            value={mainSearchValue}
            onChange={(e) => handleMainSearchChange(e.target.value)}
            onPressEnter={() => {
              void onClickSearch();
            }}
          />
          <div className="planarian-search-toolbar__embedded-actions">
            <Button
              aria-label="Search"
              className="planarian-search-toolbar__embedded-action"
              icon={<SearchOutlined />}
              onClick={() => {
                void onClickSearch();
              }}
              title="Search"
              type="text"
            >
              <span className="planarian-search-toolbar__embedded-action-label">
                Search
              </span>
            </Button>
            <Button
              aria-label="Advanced search"
              className="planarian-search-toolbar__embedded-action"
              icon={<SlidersOutlined />}
              onClick={() => setIsAdvancedSearchOpen(true)}
              title="Advanced search"
              type="text"
            >
              <span className="planarian-search-toolbar__embedded-action-label">
                Advanced
              </span>
            </Button>
            <Popconfirm
              cancelText="Cancel"
              okText="Clear"
              onConfirm={() => {
                void onClearSearch();
              }}
              placement="bottomRight"
              title="Clear all search filters?"
            >
              <Button
                aria-label="Clear search"
                className="planarian-search-toolbar__embedded-action"
                icon={<ClearOutlined />}
                title="Clear search"
                type="text"
              >
                <span className="planarian-search-toolbar__embedded-action-label">
                  Clear
                </span>
              </Button>
            </Popconfirm>
          </div>
        </div>
      </div>
      <div className="planarian-search-toolbar__right">
        {shouldShowPersistentContent && sortOptions?.length ? (
          <SplitSortControl
            isDescending={queryBuilder.getSortDescending() ?? false}
            onSelect={handleSortOptionChange}
            onToggleDirection={handleSortDirectionToggle}
            selectedValue={queryBuilder.getSortBy()}
            sortOptions={sortOptions}
          />
        ) : null}
        {toolbarMetrics?.map((metric) => (
          <div
            className={[
              "planarian-search-toolbar__metric",
              metric.className,
            ]
              .filter(Boolean)
              .join(" ")}
            key={metric.key}
          >
            <span className="planarian-search-toolbar__metric-label">
              {metric.label}
            </span>
            <span className="planarian-search-toolbar__metric-value">
              {metric.value}
            </span>
          </div>
        ))}
      </div>
    </div>
  );

  const exportMenuItems: MenuProps["items"] = [
    {
      key: "gpx",
      label: "Export GPX",
    },
    {
      key: "csv",
      label: "Export CSV",
    },
  ];

  const handleExportClick: MenuProps["onClick"] = async (e) => {
    if (e.key === "gpx" && onExportGpx) {
      await onExportGpx();
    } else if (e.key === "csv" && onExportCsv) {
      await onExportCsv();
    }
  };

  const inlineControlsNode = inlineControls
    ? inlineControls({
        defaultControls,
        openDrawer: () => setIsAdvancedSearchOpen(true),
        clearFilters: onClearSearch,
        onSearch: onClickSearch,
        setMainSearchValue: handleMainSearchChange,
        mainSearchValue,
        mainSearchFieldLabel,
        hasFilters: queryBuilder.hasFilters(),
        queryBuilder,
      })
    : defaultControls;

  return (
    <>
      {inlineControlsNode}
      <Drawer
        title="Advanced Search"
        open={isAdvancedSearchOpen}
        onClose={() => setIsAdvancedSearchOpen(false)}
        footer={
          <div
            style={{
              width: "100%",
              display: "flex",
              justifyContent: "space-between",
            }}
          >
            <Button type="primary" onClick={onClickSearch}>
              Search
            </Button>
            {onExportGpx && (
              <div style={{ marginLeft: "auto" }}>
                <ShouldDisplay permissionKey={PermissionKey.Export}>
                  <Dropdown.Button
                    menu={{
                      items: exportMenuItems,
                      onClick: handleExportClick,
                    }}
                    onClick={() => {
                      if (onExportGpx) {
                        onExportGpx();
                      }
                    }}
                    icon={<DownloadOutlined />}
                  >
                    Export
                  </Dropdown.Button>
                </ShouldDisplay>
              </div>
            )}
          </div>
        }
      >
        <Form
          onKeyDown={(e) => {
            if (e.key === "Enter") {
              onClickSearch();
            }
          }}
          layout="vertical"
          initialValues={queryBuilder.getDefaultValues()}
          form={form}
        >
          {children}
        </Form>
      </Drawer>
    </>
  );
};

export { AdvancedSearchDrawerComponent };
