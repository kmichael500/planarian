import { useCallback, useEffect, useRef, useState } from "react";
import type { ScrollState } from "./ScrollState";

type ScrollRevealVisibilityMode = "direct" | "thresholdLockout";

interface UseScrollRevealVisibilityOptions {
  breakpointPx?: number;
  enabled?: boolean;
  hideThresholdPx?: number;
  initialVisible?: boolean;
  mode?: ScrollRevealVisibilityMode;
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
  const topZoneRevealLockedRef = useRef(false);
  const isActive = enabled && isWithinBreakpoint;

  const reset = useCallback(() => {
    setIsVisible(initialVisible);
    topZoneRevealLockedRef.current = false;
  }, [initialVisible]);

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

      if (scrollTop >= hideThresholdPx) {
        setIsVisible(false);
        topZoneRevealLockedRef.current = false;
        return;
      }

      if (scrollTop <= revealThresholdPx) {
        if (
          (direction === "up" || direction === "idle") &&
          !isVisible &&
          !topZoneRevealLockedRef.current
        ) {
          setIsVisible(true);
          topZoneRevealLockedRef.current = true;
        }
        return;
      }
    },
    [hideThresholdPx, isActive, isVisible, mode, revealThresholdPx]
  );

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
