import { Empty, Card, List } from "antd";
import { useCallback, useRef } from "react";
import { PagedResult } from "../../../Modules/Search/Models/PagedResult";
import { QueryBuilder } from "../../../Modules/Search/Services/QueryBuilder";
import { getScrollState } from "../../Scroll/getScrollState";
import type { ScrollState } from "../../Scroll/ScrollState";
import { CardGridPagination } from "./CardGridPagination";
import "./CardGridComponent.scss";

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
  fillHeight?: boolean;
  onScrollStateChange?: (
    isScrolled: boolean,
    state?: ScrollState
  ) => void;
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
  fillHeight,
  onScrollStateChange,
}: CardGridComponentProps<T, TQueryBuilder>) => {
  const bodyRef = useRef<HTMLDivElement | null>(null);
  const isScrolledRef = useRef(false);
  const previousScrollTopRef = useRef(0);
  let data: T[] = [];

  const notifyScrollStateChange = useCallback(
    (scrollTop: number) => {
      const nextScrollState = getScrollState(
        previousScrollTopRef.current,
        scrollTop
      );

      previousScrollTopRef.current = nextScrollState.nextPreviousScrollTop;
      isScrolledRef.current = nextScrollState.isScrolled;

      onScrollStateChange?.(
        nextScrollState.isScrolled,
        nextScrollState.state
      );
    },
    [onScrollStateChange]
  );

  if (items) {
    data = items;
  } else if (pagedItems) {
    data = pagedItems.results;
  }

  const pagination = pagedItems ? (
    <div className="planarian-card-grid__pagination">
      <CardGridPagination
        pagedItems={pagedItems}
        onPageChange={async (pageNumber, pageSize) => {
          if (queryBuilder) {
            queryBuilder.changePage(pageNumber, pageSize);
          }
          if (onSearch) {
            await onSearch();
          }

          if (bodyRef.current) {
            bodyRef.current.scrollTop = 0;
          }
          notifyScrollStateChange(0);
        }}
      />
    </div>
  ) : null;

  return (
    <div
      className={`planarian-card-grid${
        fillHeight ? " planarian-card-grid--fill-height" : ""
      }`}
    >
      <div
        className="planarian-card-grid__body"
        ref={bodyRef}
        onScroll={(event) => {
          notifyScrollStateChange(event.currentTarget.scrollTop);
        }}
      >
        {data.length === 0 && (
          <Card>
            <Empty description={<span>{noDataDescription}</span>}>
              {noDataCreateButton}
            </Empty>
          </Card>
        )}
        {!useList && (
          <div className="planarian-card-grid__items">
            {data.map((item) => {
              return (
                <div className="planarian-card-grid__item" key={itemKey(item)}>
                  {renderItem(item)}
                </div>
              );
            })}
          </div>
        )}
        {useList && (
          <List dataSource={data} renderItem={(item) => renderItem(item)} />
        )}
        {fillHeight && pagination}
      </div>
      {!fillHeight && pagination}
    </div>
  );
};

export { CardGridComponent };
