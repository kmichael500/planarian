import {
  Button,
  Col,
  Drawer,
  Form,
  FormInstance,
  Input,
  Row,
  Space,
} from "antd";
import { FilterFormProps } from "../Models/NumberComparisonFormItemProps";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
import { QueryOperator } from "../Services/QueryBuilder";
import { useState } from "react";
import { SlidersOutlined, ClearOutlined } from "@ant-design/icons";
import { NestedKeyOf } from "../../../Shared/Helpers/StringHelpers";

export interface AdvancedSearchDrawerComponentProps<T extends object>
  extends FilterFormProps<T> {
  onSearch: () => Promise<void>;
  children?: React.ReactNode;
  mainSearchField: NestedKeyOf<T>;
  mainSearchFieldLabel: string;
  form?: FormInstance<T>;
}

const AdvancedSearchDrawerComponent = <T extends object>({
  queryBuilder,
  onSearch,
  children,
  mainSearchField,
  mainSearchFieldLabel,
  form,
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
    <Row style={{ marginBottom: 10 }} gutter={5}>
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
          <PlanarianButton
            icon={<SlidersOutlined />}
            onClick={(e) => setIsAdvancedSearchOpen(true)}
          >
            Advanced
          </PlanarianButton>
          <PlanarianButton
            icon={<ClearOutlined />}
            onClick={(e) => onClearSearch()}
          >
            Clear
          </PlanarianButton>
        </Space>
        <Drawer
          title="Advanced Search"
          open={isAdvancedSearchOpen}
          onClose={(e) => setIsAdvancedSearchOpen(false)}
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
          <Button
            onClick={() => {
              onClickSearch();
            }}
          >
            Search
          </Button>
        </Drawer>
      </Col>
    </Row>
  );
};

export { AdvancedSearchDrawerComponent };
