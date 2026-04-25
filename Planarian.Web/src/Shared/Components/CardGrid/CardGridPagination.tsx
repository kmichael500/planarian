import { LeftOutlined, RightOutlined, UpOutlined } from "@ant-design/icons";
import { Dropdown, MenuProps } from "antd";
import {
  useCallback,
  useEffect,
  useLayoutEffect,
  useMemo,
  useRef,
  useState,
  type ReactNode,
} from "react";
import { PagedResult } from "../../../Modules/Search/Models/PagedResult";
import { formatNumber } from "../../Helpers/StringHelpers";
import { useArrowPageNavigation } from "../../Hooks/useArrowPageNavigation";
import "./CardGridPagination.scss";

interface CardGridPaginationProps<T extends object> {
  pagedItems: PagedResult<T>;
  onPageChange: (pageNumber: number, pageSize: number) => Promise<void>;
}

type PaginationToken =
  | { type: "page"; pageNumber: number }
  | { type: "ellipsis"; key: string; targetPage: number; label: string };

type MiddleMode = "full" | "compact" | "hidden";

const FULL_PAGE_WINDOW = 5;

const getSafeTotalPages = (totalPages: number | undefined) =>
  Math.max(totalPages ?? 0, 1);

const clampPageNumber = (pageNumber: number | undefined, totalPages: number) =>
  Math.min(Math.max(pageNumber ?? 1, 1), totalPages);

const getPageWindow = (
  currentPage: number,
  totalPages: number,
  maxVisiblePages: number
) => {
  if (totalPages <= maxVisiblePages) {
    return { start: 1, end: totalPages };
  }

  const halfWindow = Math.floor(maxVisiblePages / 2);
  let start = currentPage - halfWindow;
  let end = start + maxVisiblePages - 1;

  if (start < 1) {
    start = 1;
    end = maxVisiblePages;
  }

  if (end > totalPages) {
    end = totalPages;
    start = totalPages - maxVisiblePages + 1;
  }

  return { start, end };
};

const getFullPaginationTokens = (
  currentPage: number,
  totalPages: number
): PaginationToken[] => {
  if (totalPages === 1) {
    return [{ type: "page", pageNumber: 1 }];
  }

  const { start, end } = getPageWindow(currentPage, totalPages, FULL_PAGE_WINDOW);
  const tokens: PaginationToken[] = [];

  if (start > 1) {
    tokens.push({ type: "page", pageNumber: 1 });
  }

  if (start > 2) {
    tokens.push({
      type: "ellipsis",
      key: `start-${start}`,
      targetPage: Math.max(start - FULL_PAGE_WINDOW, 1),
      label: "Jump backward",
    });
  }

  for (let pageNumber = start; pageNumber <= end; pageNumber += 1) {
    if (
      pageNumber === 1 &&
      tokens.some((token) => token.type === "page" && token.pageNumber === 1)
    ) {
      continue;
    }

    if (
      pageNumber === totalPages &&
      tokens.some(
        (token) => token.type === "page" && token.pageNumber === totalPages
      )
    ) {
      continue;
    }

    tokens.push({ type: "page", pageNumber });
  }

  if (end < totalPages - 1) {
    tokens.push({
      type: "ellipsis",
      key: `end-${end}`,
      targetPage: Math.min(end + FULL_PAGE_WINDOW, totalPages),
      label: "Jump forward",
    });
  }

  if (end < totalPages) {
    tokens.push({ type: "page", pageNumber: totalPages });
  }

  return tokens;
};

const getCompactPaginationTokens = (
  totalPages: number
): PaginationToken[] => {
  if (totalPages === 1) {
    return [{ type: "page", pageNumber: 1 }];
  }

  if (totalPages === 2) {
    return [
      { type: "page", pageNumber: 1 },
      { type: "page", pageNumber: 2 },
    ];
  }

  return [
    { type: "page", pageNumber: 1 },
    {
      type: "ellipsis",
      key: "compact",
      targetPage: 1,
      label: "Collapsed pages",
    },
    { type: "page", pageNumber: totalPages },
  ];
};

const CardGridPagination = <T extends object>({
  pagedItems,
  onPageChange,
}: CardGridPaginationProps<T>) => {
  const totalPages = getSafeTotalPages(pagedItems.totalPages);
  const currentPage = clampPageNumber(pagedItems.pageNumber, totalPages);
  const pageSize = pagedItems.pageSize;
  const pagesRef = useRef<HTMLDivElement | null>(null);
  const [windowWidth, setWindowWidth] = useState(() =>
    typeof window === "undefined" ? 0 : window.innerWidth
  );
  const [layoutVersion, setLayoutVersion] = useState(0);
  const [middleMode, setMiddleMode] = useState<MiddleMode>("full");
  const [isChangingPage, setIsChangingPage] = useState(false);
  const resultText = `${formatNumber(pagedItems.totalCount) ?? "0"} results`;
  const pageMenuItems = useMemo<MenuProps["items"]>(
    () =>
      Array.from({ length: totalPages }, (_, index) => {
        const pageNumber = index + 1;
        return {
          key: pageNumber.toString(),
          label: `Page ${pageNumber}`,
          disabled: pageNumber === currentPage || isChangingPage,
        };
      }),
    [currentPage, isChangingPage, totalPages]
  );

  const tokens = useMemo(
    () =>
      middleMode === "full"
        ? getFullPaginationTokens(currentPage, totalPages)
        : middleMode === "compact"
          ? getCompactPaginationTokens(totalPages)
          : [],
    [currentPage, middleMode, totalPages]
  );

  useEffect(() => {
    setMiddleMode("full");
  }, [currentPage, totalPages, windowWidth, layoutVersion]);

  useLayoutEffect(() => {
    if (!pagesRef.current || middleMode === "hidden") {
      return;
    }

    const isOverflowing =
      pagesRef.current.scrollWidth > pagesRef.current.clientWidth + 1;

    if (!isOverflowing) {
      return;
    }

    if (middleMode === "full") {
      setMiddleMode("compact");
      return;
    }

    setMiddleMode("hidden");
  }, [layoutVersion, middleMode, tokens, windowWidth]);

  useEffect(() => {
    if (typeof window === "undefined") {
      return;
    }

    const handleResize = () => {
      setWindowWidth(window.innerWidth);
    };

    window.addEventListener("resize", handleResize);

    return () => window.removeEventListener("resize", handleResize);
  }, []);

  const changePage = useCallback(
    async (nextPageNumber: number) => {
      if (
        isChangingPage ||
        nextPageNumber === currentPage ||
        nextPageNumber < 1 ||
        nextPageNumber > totalPages
      ) {
        return;
      }

      setIsChangingPage(true);

      try {
        await onPageChange(nextPageNumber, pageSize);
      } finally {
        setIsChangingPage(false);
      }
    },
    [currentPage, isChangingPage, onPageChange, pageSize, totalPages]
  );

  useArrowPageNavigation({
    onPrevious: () => {
      void changePage(currentPage - 1);
    },
    onNext: () => {
      void changePage(currentPage + 1);
    },
  });

  useEffect(() => {
    if (
      typeof window === "undefined" ||
      typeof ResizeObserver === "undefined" ||
      !pagesRef.current
    ) {
      return;
    }

    const resizeObserver = new ResizeObserver(() => {
      setLayoutVersion((current) => current + 1);
    });

    resizeObserver.observe(pagesRef.current);

    return () => resizeObserver.disconnect();
  }, []);

  const renderNavButton = (
    label: string,
    icon: ReactNode,
    pageNumber: number,
    disabled: boolean
  ) => (
    <button
      type="button"
      className="planarian-card-grid-pagination__button planarian-card-grid-pagination__button--nav"
      onClick={() => void changePage(pageNumber)}
      disabled={disabled || isChangingPage}
      aria-label={label}
    >
      {icon}
      <span>{label}</span>
    </button>
  );

  const renderSummaryPrimary = () => {
    const pageLabel = `Page ${currentPage} of ${totalPages}`;

    if (totalPages <= 1) {
      return (
        <span className="planarian-card-grid-pagination__summary-primary">
          {pageLabel}
        </span>
      );
    }

    return (
      <Dropdown
        menu={{
          items: pageMenuItems,
          onClick: ({ key }) => void changePage(Number(key)),
        }}
        overlayClassName="planarian-card-grid-pagination__page-menu planarian-dropdown--touch"
        placement="topLeft"
        trigger={["click"]}
        disabled={isChangingPage}
      >
        <button
          type="button"
          className="planarian-card-grid-pagination__summary-primary planarian-card-grid-pagination__summary-page-button"
          aria-label="Choose page"
        >
          <span>{pageLabel}</span>
          <UpOutlined />
        </button>
      </Dropdown>
    );
  };

  return (
    <div className="planarian-card-grid-pagination">
      <div className="planarian-card-grid-pagination__summary">
        {renderSummaryPrimary()}
        <span className="planarian-card-grid-pagination__summary-secondary">
          {resultText}
        </span>
      </div>

      <div
        ref={pagesRef}
        className="planarian-card-grid-pagination__pages"
        aria-label="Pagination pages"
        hidden={middleMode === "hidden"}
      >
        {tokens.map((token) => {
          if (token.type === "ellipsis") {
            return (
              <button
                type="button"
                className="planarian-card-grid-pagination__ellipsis"
                key={token.key}
                onClick={
                  middleMode === "compact"
                    ? undefined
                    : () => void changePage(token.targetPage)
                }
                disabled={middleMode === "compact" || isChangingPage}
                aria-label={token.label}
              >
                ...
              </button>
            );
          }

          return (
            <button
              type="button"
              key={token.pageNumber}
              className={`planarian-card-grid-pagination__button${
                middleMode === "full" && token.pageNumber === currentPage
                  ? " planarian-card-grid-pagination__button--active"
                  : ""
              }`}
              onClick={() => void changePage(token.pageNumber)}
              disabled={token.pageNumber === currentPage || isChangingPage}
              aria-current={token.pageNumber === currentPage ? "page" : undefined}
              aria-label={`Go to page ${token.pageNumber}`}
            >
              {token.pageNumber}
            </button>
          );
        })}
      </div>

      <div className="planarian-card-grid-pagination__nav">
        {renderNavButton(
          "Previous",
          <LeftOutlined />,
          currentPage - 1,
          currentPage === 1
        )}
        {renderNavButton(
          "Next",
          <RightOutlined />,
          currentPage + 1,
          currentPage === totalPages
        )}
      </div>
    </div>
  );
};

export { CardGridPagination };
