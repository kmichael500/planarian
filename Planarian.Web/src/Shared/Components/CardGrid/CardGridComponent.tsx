import { Row, Col, Button, Empty, Card } from "antd";
import Pagination, { PaginationConfig } from "antd/lib/pagination";
import { Key } from "react";
interface CardGridComponentProps {
  items: ReactNodeWithKey[] | undefined;
  noDataDescription?: string;
  noDataCreateButton?: React.ReactNode;
  pagination?: PaginationConfig | false;
}

interface ReactNodeWithKey {
  item: React.ReactNode;
  key: Key | null | undefined;
}

const CardGridComponent: React.FC<CardGridComponentProps> = ({
  items,
  noDataDescription,
  noDataCreateButton,
  pagination,
}) => {
  return (
    <>
      {items === undefined ||
        (items.length === 0 && (
          <Card>
            <Empty description={<span>{noDataDescription}</span>}>
              {noDataCreateButton}
            </Empty>
          </Card>
        ))}
      <Row
        gutter={[
          { xs: 8, sm: 8, md: 12, lg: 12 },
          { xs: 8, sm: 8, md: 12, lg: 12 },
        ]}
      >
        {items?.map((node) => {
          return (
            <Col key={node.key} xs={24} sm={12} md={8} lg={6}>
              {node.item}
            </Col>
          );
        })}
      </Row>
      <Row style={{ marginTop: "10px" }}>
        <Col flex="auto"></Col>

        <Col>
          <Pagination {...pagination} />
        </Col>
      </Row>
    </>
  );
};

export { CardGridComponent };
