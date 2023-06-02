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
}

const SearchFormComponent = <T,>({
  queryBuilder,
  onSearch,
  children,
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
          defaultValue={queryBuilder.getFieldValue("name") as string}
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
                onSearch();
              }
            }}
            layout="vertical"
            initialValues={queryBuilder.getDefaultValues()}
          >
            {children}
          </Form>
          <Button
            onClick={() => {
              onSearch();
            }}
          >
            Search
          </Button>
        </Drawer>
      </Col>
    </Row>
  );
};

export { SearchFormComponent };
