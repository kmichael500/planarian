import {
  Button,
  Col,
  Drawer,
  Form,
  FormInstance,
  Input,
  Row,
  Select,
  Space,
  Typography,
} from "antd";
import { FilterFormProps } from "../Models/NumberComparisonFormItemProps";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
import { QueryOperator } from "../Services/QueryBuilder";
import { useState } from "react";
import { SlidersOutlined, ClearOutlined } from "@ant-design/icons";
import { NestedKeyOf } from "../../../Shared/Helpers/StringHelpers";
import { CaveSearchSortByConstants } from "../../Caves/Models/CaveSearchVm";
import { SelectListItem } from "../../../Shared/Models/SelectListItem";

export interface AdvancedSearchDrawerComponentProps<T extends object>
  extends FilterFormProps<T> {
  onSearch: () => Promise<void>;
  children?: React.ReactNode;
  mainSearchField: NestedKeyOf<T>;
  mainSearchFieldLabel: string;
  form?: FormInstance<T>;
  sortOptions?: SelectListItem<string>[];
}

const AdvancedSearchDrawerComponent = <T extends object>({
  queryBuilder,
  onSearch,
  children,
  mainSearchField,
  mainSearchFieldLabel,
  form,
  sortOptions,
}: AdvancedSearchDrawerComponentProps<T>) => {
  const [isAdvancedSearchOpen, setIsAdvancedSearchOpen] = useState(false);
  const onClickSearch = async () => {
    setIsAdvancedSearchOpen(false);
    await onSearch();
  };

  const onClearSearch = async () => {
    queryBuilder.clear();
    form?.resetFields();
    await onSearch();
  };

  return (
    <Row gutter={[16, 16]} align="middle" style={{ marginBottom: 10 }}>
      <Col>
        <Input.Search
          placeholder={mainSearchFieldLabel}
          defaultValue={queryBuilder.getFieldValue(mainSearchField) as string}
          onChange={(e) => {
            queryBuilder.filterBy(
              mainSearchField,
              QueryOperator.Contains,
              e.target.value as any
            );
          }}
          onSearch={onClickSearch}
        />
      </Col>
      <Col>
        <Space>
          {sortOptions && (
            <>
              <Select
                style={{ width: "139px" }}
                value={queryBuilder.getSortBy()}
                onChange={(value) => {
                  queryBuilder.setSort(value);
                  onSearch();
                }}
              >
                {sortOptions.map((option) => (
                  <Select.Option key={option.value} value={option.value}>
                    {option.display}
                  </Select.Option>
                ))}
              </Select>
              <Select
                value={queryBuilder.getSortDescending() ? "desc" : "asc"}
                onChange={(value) => {
                  queryBuilder.setSortDescending(value === "desc");
                  onSearch();
                }}
                options={[
                  { label: "Descending", value: "desc" },
                  { label: "Ascending", value: "asc" },
                ]}
              />
            </>
          )}
          <PlanarianButton
            icon={<SlidersOutlined />}
            onClick={() => setIsAdvancedSearchOpen(true)}
            alwaysShowChildren
          >
            Advanced
          </PlanarianButton>
          <PlanarianButton icon={<ClearOutlined />} onClick={onClearSearch}>
            Clear
          </PlanarianButton>
        </Space>

        <Drawer
          title="Advanced Search"
          open={isAdvancedSearchOpen}
          onClose={() => setIsAdvancedSearchOpen(false)}
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
          <Button onClick={onClickSearch}>Search</Button>
        </Drawer>
      </Col>
    </Row>
  );
};

export { AdvancedSearchDrawerComponent };
