import { Button, Col, Drawer, Form, Input, Row } from "antd";
import { FilterFormProps } from "./NumberComparisonFormItemProps";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
import { QueryOperator } from "../Services/QueryBuilder";
import { useState } from "react";
import { SlidersOutlined } from "@ant-design/icons";

export interface AdvancedSearchDrawerComponentProps<T>
  extends FilterFormProps<T> {
  onSearch: () => Promise<void>;
  children?: React.ReactNode;
  mainSearchField: keyof T;
  mainSearchFieldLabel: string;
}

const AdvancedSearchDrawerComponent = <T,>({
  queryBuilder,
  onSearch,
  children,
  mainSearchField,
  mainSearchFieldLabel,
}: AdvancedSearchDrawerComponentProps<T>) => {
  const [isAdvancedSearchOpen, setIsAdvancedSearchOpen] = useState(false);
  const onClickSearch = async () => {
    setIsAdvancedSearchOpen(false);
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
              "name" as any,
              QueryOperator.Contains,
              e.target.value as any
            );
          }}
          onSearch={onClickSearch}
        />
      </Col>
      <Col>
        <PlanarianButton
          icon={<SlidersOutlined />}
          onClick={(e) => setIsAdvancedSearchOpen(true)}
        >
          Advanced
        </PlanarianButton>
        <Drawer
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
