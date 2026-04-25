import { useCallback, useEffect, useRef, useState } from "react";
import type { ScrollState } from "./ScrollState";

type ScrollRevealVisibilityMode = "direct" | "mobileDebounce";

interface UseScrollRevealVisibilityOptions {
  breakpointPx?: number;
  enabled?: boolean;
  hideThresholdPx?: number;
  initialVisible?: boolean;
  mode?: ScrollRevealVisibilityMode;
  revealDelayMs?: number;
  revealThresholdPx?: number;
}

interface UseScrollRevealVisibilityResult {
  isVisible: boolean;
  handleScrollStateChange: (isScrolled: boolean, state?: ScrollState) => void;
  reset: () => void;
}

const useScrollRevealVisibility = ({
  breakpointPx,
  enabled = true,
  hideThresholdPx = 32,
  initialVisible = true,
  mode = "direct",
  revealDelayMs = 140,
  revealThresholdPx = 12,
}: UseScrollRevealVisibilityOptions = {}): UseScrollRevealVisibilityResult => {
  const [isVisible, setIsVisible] = useState(initialVisible);
  const [isWithinBreakpoint, setIsWithinBreakpoint] = useState(() =>
    breakpointPx === undefined
      ? true
      : typeof window === "undefined"
        ? true
        : window.innerWidth <= breakpointPx
  );
  const revealTimeoutRef = useRef<number | null>(null);
  const isActive = enabled && isWithinBreakpoint;

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
    (isScrolled: boolean, state?: ScrollState) => {
      if (!isActive) {
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
      hideThresholdPx,
      isActive,
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
    if (!isActive) {
      reset();
    }
  }, [isActive, reset]);

  useEffect(() => {
    if (breakpointPx === undefined || typeof window === "undefined") {
      return;
    }

    const updateBreakpoint = () => {
      setIsWithinBreakpoint(window.innerWidth <= breakpointPx);
    };

    window.addEventListener("resize", updateBreakpoint);
    return () => window.removeEventListener("resize", updateBreakpoint);
  }, [breakpointPx]);

  return {
    isVisible: isActive ? isVisible : true,
    handleScrollStateChange,
    reset,
  };
};

export { useScrollRevealVisibility };
