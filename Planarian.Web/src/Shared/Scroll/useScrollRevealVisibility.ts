import { useCallback, useEffect, useRef, useState } from "react";
import type { ScrollState } from "./ScrollState";

type ScrollRevealVisibilityMode = "direct" | "thresholdLockout";
const DEFAULT_HIDE_THRESHOLD_PX = 32;
const DEFAULT_REVEAL_THRESHOLD_PX = 12;

interface UseScrollRevealVisibilityOptions {
  breakpointPx?: number;
  enabled?: boolean;
  hideThresholdPx?: number;
  hideThresholdRatio?: number;
  initialVisible?: boolean;
  mode?: ScrollRevealVisibilityMode;
  revealThresholdPx?: number;
  revealThresholdRatio?: number;
}

interface UseScrollRevealVisibilityResult {
  contentRef: (node: HTMLDivElement | null) => void;
  isVisible: boolean;
  handleScrollStateChange: (isScrolled: boolean, state?: ScrollState) => void;
  reset: () => void;
}

const useScrollRevealVisibility = ({
  breakpointPx,
  enabled = true,
  hideThresholdPx,
  hideThresholdRatio = 0.35,
  initialVisible = true,
  mode = "direct",
  revealThresholdPx,
  revealThresholdRatio = 0.12,
}: UseScrollRevealVisibilityOptions = {}): UseScrollRevealVisibilityResult => {
  const contentElementRef = useRef<HTMLDivElement | null>(null);
  const [isVisible, setIsVisible] = useState(initialVisible);
  const [isWithinBreakpoint, setIsWithinBreakpoint] = useState(() =>
    breakpointPx === undefined
      ? true
      : typeof window === "undefined"
        ? true
        : window.innerWidth <= breakpointPx
  );
  const [measuredContentHeight, setMeasuredContentHeight] = useState(0);
  const topZoneRevealLockedRef = useRef(false);
  const isActive = enabled && isWithinBreakpoint;
  const resolvedHideThresholdPx =
    hideThresholdPx ??
    (measuredContentHeight > 0
      ? measuredContentHeight * hideThresholdRatio
      : DEFAULT_HIDE_THRESHOLD_PX);
  const resolvedRevealThresholdPx =
    revealThresholdPx ??
    (measuredContentHeight > 0
      ? measuredContentHeight * revealThresholdRatio
      : DEFAULT_REVEAL_THRESHOLD_PX);

  const reset = useCallback(() => {
    setIsVisible(initialVisible);
    topZoneRevealLockedRef.current = false;
  }, [initialVisible]);

  const contentRef = useCallback((node: HTMLDivElement | null) => {
    contentElementRef.current = node;

    if (node) {
      setMeasuredContentHeight(node.scrollHeight);
    }
  }, []);

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

      if (scrollTop >= resolvedHideThresholdPx) {
        setIsVisible(false);
        topZoneRevealLockedRef.current = false;
        return;
      }

      if (scrollTop <= resolvedRevealThresholdPx) {
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
    [
      isActive,
      isVisible,
      mode,
      resolvedHideThresholdPx,
      resolvedRevealThresholdPx,
    ]
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

  useEffect(() => {
    if (
      typeof ResizeObserver === "undefined" ||
      !contentElementRef.current
    ) {
      return;
    }

    const element = contentElementRef.current;
    const updateMeasuredHeight = () => {
      setMeasuredContentHeight(element.scrollHeight);
    };

    updateMeasuredHeight();

    const observer = new ResizeObserver(() => {
      updateMeasuredHeight();
    });

    observer.observe(element);

    return () => observer.disconnect();
  }, []);

  return {
    contentRef,
    isVisible: isActive ? isVisible : true,
    handleScrollStateChange,
    reset,
  };
};

export { useScrollRevealVisibility };
