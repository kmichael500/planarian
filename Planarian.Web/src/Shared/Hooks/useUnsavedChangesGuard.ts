import { useCallback, useRef, useState } from "react";
import { useBeforeUnload, useBlocker } from "react-router-dom";

export const useUnsavedChangesGuard = () => {
  const dirtyRef = useRef(false);
  const [isDirty, setIsDirty] = useState(false);

  const markDirty = useCallback(() => {
    dirtyRef.current = true;
    setIsDirty(true);
  }, []);

  const markClean = useCallback(() => {
    dirtyRef.current = false;
    setIsDirty(false);
  }, []);

  const blocker = useBlocker(({ currentLocation, nextLocation }) =>
    dirtyRef.current &&
    (currentLocation.pathname !== nextLocation.pathname ||
      currentLocation.search !== nextLocation.search ||
      currentLocation.hash !== nextLocation.hash)
  );

  useBeforeUnload(
    useCallback((event) => {
      if (!dirtyRef.current) return;
      event.preventDefault();
      event.returnValue = "";
    }, []),
    { capture: true }
  );

  const keepEditing = useCallback(() => {
    if (blocker.state === "blocked") blocker.reset();
  }, [blocker]);

  const discardChanges = useCallback(() => {
    markClean();
    if (blocker.state === "blocked") blocker.proceed();
  }, [blocker, markClean]);

  return {
    isDirty,
    isBlocked: blocker.state === "blocked",
    markDirty,
    markClean,
    keepEditing,
    discardChanges,
  };
};
