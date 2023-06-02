import { Row, Col, Button, Empty, Card } from "antd";
import Pagination, { PaginationConfig } from "antd/lib/pagination";
import { Key } from "react";
import { PagedResult } from "../../../Modules/Search/Models/PagedResult";

interface CardGridComponentProps<T> {
  renderItem: (item: T) => React.ReactNode;
  items?: T[] | undefined;
  pagedItems?: PagedResult<T> | undefined;
  noDataDescription?: string;
  noDataCreateButton?: React.ReactNode;
  pagination?: PaginationConfig | false;
}

interface ReactNodeWithKey {
  item: React.ReactNode;
  key: Key | null | undefined;
}

const CardGridComponent = <T,>({
  renderItem,
  items,
  pagedItems,
  noDataDescription,
  noDataCreateButton,
  pagination,
}: CardGridComponentProps<T>) => {
  let data: T[] = [];

  if (items) {
    data = items;
  } else if (pagedItems) {
    data = pagedItems.results;
  }

  return (
    <>
      {data.length === 0 && (
        <Card>
          <Empty description={<span>{noDataDescription}</span>}>
            {noDataCreateButton}
          </Empty>
        </Card>
      )}
      <Row
        gutter={[
          { xs: 8, sm: 8, md: 12, lg: 12 },
          { xs: 8, sm: 8, md: 12, lg: 12 },
        ]}
      >
        {data.map((node, i) => {
          return (
            <Col key={i} xs={24} sm={12} md={8} lg={6}>
              {renderItem(node)}
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
