import {
  Button,
  Col,
  Drawer,
  Form,
  FormInstance,
  Input,
  Row,
  Select,
} from "antd";
import { FilterFormProps } from "../Models/NumberComparisonFormItemProps";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
import { QueryOperator } from "../Services/QueryBuilder";
import { useState } from "react";
import { SlidersOutlined, ClearOutlined } from "@ant-design/icons";
import { NestedKeyOf } from "../../../Shared/Helpers/StringHelpers";
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
    <Row align="middle" gutter={[16, 10]} style={{ marginBottom: 10 }}>
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
        <Row gutter={[8, 8]} align="middle" style={{ flexWrap: "wrap" }}>
          {sortOptions && (
            <>
              <Col style={{ flex: "0 0 auto" }}>
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
              </Col>
              <Col style={{ flex: "0 0 auto" }}>
                <Select
                  value={queryBuilder.getSortDescending() ? "desc" : "asc"}
                  onChange={(value) => {
                    queryBuilder.setSortDescending(value === "desc");
                    onSearch();
                  }}
                  options={[
                    { label: "Desc", value: "desc" },
                    { label: "Asc", value: "asc" },
                  ]}
                />
              </Col>
            </>
          )}
          <Col style={{ flex: "0 0 auto" }}>
            <PlanarianButton
              icon={<SlidersOutlined />}
              onClick={() => setIsAdvancedSearchOpen(true)}
              collapseOnScreenSize="xs"
            >
              Advanced
            </PlanarianButton>
          </Col>
          <Col style={{ flex: "0 0 auto" }}>
            <PlanarianButton
              collapseOnScreenSize="sm"
              icon={<ClearOutlined />}
              onClick={onClearSearch}
            >
              Clear
            </PlanarianButton>
          </Col>
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
        </Row>
      </Col>
    </Row>
  );
};

export { AdvancedSearchDrawerComponent };
