import { useCallback, useEffect, useRef, useState } from "react";
import type { CardGridScrollState } from "../Components/CardGrid/CardGridComponent";

type ScrollRevealVisibilityMode = "direct" | "mobileDebounce";

interface UseScrollRevealVisibilityOptions {
  enabled?: boolean;
  hideThresholdPx?: number;
  initialVisible?: boolean;
  mode?: ScrollRevealVisibilityMode;
  revealDelayMs?: number;
  revealThresholdPx?: number;
}

interface UseScrollRevealVisibilityResult {
  isVisible: boolean;
  handleScrollStateChange: (
    isScrolled: boolean,
    state?: CardGridScrollState
  ) => void;
  reset: () => void;
}

const useScrollRevealVisibility = ({
  enabled = true,
  hideThresholdPx = 32,
  initialVisible = true,
  mode = "direct",
  revealDelayMs = 140,
  revealThresholdPx = 12,
}: UseScrollRevealVisibilityOptions = {}): UseScrollRevealVisibilityResult => {
  const [isVisible, setIsVisible] = useState(initialVisible);
  const revealTimeoutRef = useRef<number | null>(null);

  const clearRevealTimeout = useCallback(() => {
    if (revealTimeoutRef.current !== null) {
      window.clearTimeout(revealTimeoutRef.current);
      revealTimeoutRef.current = null;
    }
  }, []);

  const reset = useCallback(() => {
    clearRevealTimeout();
    setIsVisible(initialVisible);
  }, [clearRevealTimeout, initialVisible]);

  const scheduleReveal = useCallback(() => {
    clearRevealTimeout();

    revealTimeoutRef.current = window.setTimeout(() => {
      setIsVisible(true);
      revealTimeoutRef.current = null;
    }, revealDelayMs);
  }, [clearRevealTimeout, revealDelayMs]);

  const handleScrollStateChange = useCallback(
    (isScrolled: boolean, state?: CardGridScrollState) => {
      if (!enabled) {
        return;
      }

      if (mode === "direct") {
        setIsVisible(!isScrolled);
        return;
      }

      const scrollTop = Math.max(state?.scrollTop ?? 0, 0);
      const direction = state?.direction ?? "idle";

      if (direction === "down" || scrollTop > revealThresholdPx) {
        clearRevealTimeout();
      }

      if (scrollTop >= hideThresholdPx) {
        setIsVisible(false);
        return;
      }

      if (scrollTop <= revealThresholdPx) {
        if (direction === "up" || direction === "idle") {
          if (!isVisible) {
            scheduleReveal();
          }
        } else {
          clearRevealTimeout();
        }
        return;
      }

      clearRevealTimeout();
    },
    [
      clearRevealTimeout,
      enabled,
      hideThresholdPx,
      isVisible,
      mode,
      revealThresholdPx,
      scheduleReveal,
    ]
  );

  useEffect(() => {
    return () => clearRevealTimeout();
  }, [clearRevealTimeout]);

  useEffect(() => {
    if (!enabled) {
      reset();
    }
  }, [enabled, reset]);

  return {
    isVisible,
    handleScrollStateChange,
    reset,
  };
};

export { useScrollRevealVisibility };
