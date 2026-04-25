type ScrollDirection = "up" | "down" | "idle";

interface ScrollState {
  direction: ScrollDirection;
  scrollTop: number;
}

export type { ScrollDirection };
export type { ScrollState };
