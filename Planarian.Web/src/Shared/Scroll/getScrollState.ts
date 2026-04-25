import type { ScrollState } from "./ScrollState";

interface GetScrollStateResult {
  isScrolled: boolean;
  nextPreviousScrollTop: number;
  state: ScrollState;
}

const getScrollState = (
  previousScrollTop: number,
  scrollTop: number
): GetScrollStateResult => {
  const normalizedScrollTop = Math.max(scrollTop, 0);
  const scrollDelta = normalizedScrollTop - previousScrollTop;
  const direction =
    Math.abs(scrollDelta) <= 1
      ? "idle"
      : scrollDelta > 0
        ? "down"
        : "up";

  return {
    isScrolled: normalizedScrollTop > 0,
    nextPreviousScrollTop: normalizedScrollTop,
    state: {
      direction,
      scrollTop: normalizedScrollTop,
    },
  };
};

export { getScrollState };
