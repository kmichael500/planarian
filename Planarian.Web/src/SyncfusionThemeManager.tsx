import React, { useEffect } from "react";
import { useTheme } from "./ThemeProvider";

export const SyncfusionThemeManager: React.FC = () => {
  const { effectiveMode } = useTheme();

  useEffect(() => {
    const syncfusionTheme = effectiveMode === "dark" ? "dark" : "light";
    const root = document.documentElement;
    const { body } = document;

    root.dataset.syncfusionTheme = syncfusionTheme;
    body.dataset.syncfusionTheme = syncfusionTheme;

    return () => {
      delete root.dataset.syncfusionTheme;
      delete body.dataset.syncfusionTheme;
    };
  }, [effectiveMode]);

  return null;
};
