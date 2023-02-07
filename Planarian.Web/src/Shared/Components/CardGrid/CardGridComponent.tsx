import { Row, Col, Button, Empty, Card } from "antd";
import { Key } from "react";
interface CardGridComponentProps {
  items: ReactNodeWithKey[] | undefined;
  noDataDescription?: string;
  noDataCreateButton?: React.ReactNode;
}

interface ReactNodeWithKey {
  item: React.ReactNode;
  key: Key | null | undefined;
}

const CardGridComponent: React.FC<CardGridComponentProps> = ({
  items,
  noDataDescription,
  noDataCreateButton,
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
          { xs: 8, sm: 8, md: 24, lg: 32 },
          { xs: 8, sm: 8, md: 24, lg: 32 },
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
    </>
  );
};

export { CardGridComponent };
