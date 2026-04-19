import React, { ReactNode, useEffect, useMemo, useRef, useState } from "react";
import { VIRTUAL_LIST_OVERSCAN } from "./fileUploaderHelpers";

interface VirtualFileListProps<T extends { id: string }> {
  items: T[];
  rowHeight: number;
  getRowHeight?: (item: T) => number;
  overscan?: number;
  className?: string;
  emptyState: ReactNode;
  renderItem: (item: T) => ReactNode;
}

export function VirtualFileList<T extends { id: string }>({
  items,
  rowHeight,
  getRowHeight,
  overscan = VIRTUAL_LIST_OVERSCAN,
  className,
  emptyState,
  renderItem,
}: VirtualFileListProps<T>) {
  const scrollContainerRef = useRef<HTMLDivElement | null>(null);
  const [viewportHeight, setViewportHeight] = useState(0);
  const [scrollTop, setScrollTop] = useState(0);

  useEffect(() => {
    const container = scrollContainerRef.current;
    if (!container) return;

    const updateViewportHeight = () => {
      setViewportHeight(container.clientHeight);
    };

    updateViewportHeight();
    const resizeObserver = new ResizeObserver(updateViewportHeight);
    resizeObserver.observe(container);

    return () => resizeObserver.disconnect();
  }, []);

  const rowMetrics = useMemo(() => {
    let runningTop = 0;
    const heights = items.map((item) => getRowHeight?.(item) ?? rowHeight);
    const offsets = heights.map((height) => {
      const top = runningTop;
      runningTop += height;
      return top;
    });

    return {
      heights,
      offsets,
      totalHeight: runningTop,
    };
  }, [getRowHeight, items, rowHeight]);

  const firstVisibleIndex = rowMetrics.offsets.findIndex(
    (top, index) => top + rowMetrics.heights[index] > scrollTop
  );
  const startIndex = Math.max(
    0,
    (firstVisibleIndex === -1 ? 0 : firstVisibleIndex) - overscan
  );
  const viewportBottom = scrollTop + viewportHeight;
  let endIndex = startIndex;
  while (
    endIndex < items.length &&
    rowMetrics.offsets[endIndex] < viewportBottom
  ) {
    endIndex += 1;
  }
  endIndex = Math.min(items.length, endIndex + overscan);
  const visibleItems = items.slice(startIndex, endIndex);

  if (items.length === 0) {
    return (
      <div className={`import-files-dashboard__virtual-list ${className ?? ""}`}>
        {emptyState}
      </div>
    );
  }

  return (
    <div
      ref={scrollContainerRef}
      className={`import-files-dashboard__virtual-list ${className ?? ""}`}
      onScroll={(event) => setScrollTop(event.currentTarget.scrollTop)}
      tabIndex={0}
    >
      <div style={{ height: rowMetrics.totalHeight, position: "relative" }}>
        <div style={{ height: rowMetrics.offsets[startIndex] ?? 0 }} />
        {visibleItems.map((item, itemIndex) => (
          <div
            key={item.id}
            className="import-files-dashboard__virtual-row"
            style={{ height: rowMetrics.heights[startIndex + itemIndex] }}
          >
            {renderItem(item)}
          </div>
        ))}
        <div
          style={{
            height:
              rowMetrics.totalHeight -
              (rowMetrics.offsets[endIndex] ?? rowMetrics.totalHeight),
          }}
        />
      </div>
    </div>
  );
}
