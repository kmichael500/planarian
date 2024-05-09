import { Row, Col, Button, Empty, Card, List } from "antd";
import Pagination, { PaginationConfig } from "antd/lib/pagination";
import { Key } from "react";
import { PagedResult } from "../../../Modules/Search/Models/PagedResult";
import { QueryBuilder } from "../../../Modules/Search/Services/QueryBuilder";

interface CardGridComponentProps<
  T extends object,
  TQueryBuilder extends object
> {
  renderItem: (item: T) => React.ReactNode;
  itemKey: (item: T) => string;
  items?: T[] | undefined;
  pagedItems?: PagedResult<T> | undefined;
  noDataDescription?: string;
  noDataCreateButton?: React.ReactNode;
  queryBuilder?: QueryBuilder<TQueryBuilder>;
  onSearch?: () => Promise<void>;
  useList?: boolean;
}

const CardGridComponent = <T extends object, TQueryBuilder extends object>({
  renderItem,
  itemKey,
  items,
  pagedItems,
  noDataDescription,
  noDataCreateButton,
  queryBuilder,
  onSearch,
  useList,
}: CardGridComponentProps<T, TQueryBuilder>) => {
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
      {!useList && (
        <Row
          gutter={[
            { xs: 8, sm: 8, md: 12, lg: 12 },
            { xs: 8, sm: 8, md: 12, lg: 12 },
          ]}
        >
          {data.map((item, i) => {
            return (
              <Col key={itemKey(item)} xs={24} sm={12} md={8} lg={6}>
                {renderItem(item)}
              </Col>
            );
          })}
        </Row>
      )}
      {useList && (
        <>
          <List dataSource={data} renderItem={(item) => renderItem(item)} />
        </>
      )}
      <Row style={{ marginTop: "10px" }}>
        <Col flex="auto"></Col>
        <Col>
          {pagedItems && (
            <Pagination
              onChange={async (pageNumber, pageSize) => {
                if (queryBuilder) {
                  queryBuilder.changePage(pageNumber, pageSize);
                }
                if (onSearch) {
                  await onSearch();
                }
              }}
              showSizeChanger={false}
              responsive={true}
              current={pagedItems?.pageNumber}
              pageSize={pagedItems?.pageSize}
              total={pagedItems?.totalCount}
            />
          )}
        </Col>
      </Row>
    </>
  );
};

export { CardGridComponent };
