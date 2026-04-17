import React, {
  createContext,
  ReactNode,
  useContext,
  useEffect,
  useLayoutEffect,
  useRef,
  useState,
} from "react";

export type ThemeMode = "light" | "dark" | "system";

interface ThemeContextProps {
    mode: ThemeMode;
    effectiveMode: "light" | "dark";
    modeLabel: "Auto" | "Light" | "Dark";
    cycleMode: () => void;
}

const ThemeContext = createContext<ThemeContextProps | undefined>(undefined);

export const ThemeProvider = ({ children }: { children: ReactNode }) => {
  const getInitialMode = (): ThemeMode => {
    if (typeof window !== "undefined") {
      const stored = localStorage.getItem("theme-mode");
      if (stored === "light" || stored === "dark" || stored === "system") {
        return stored;
      }
    }

    return "system";
  };

  const getInitialSystemDark = () => {
    if (typeof window !== "undefined" && window.matchMedia) {
      return window.matchMedia("(prefers-color-scheme: dark)").matches;
    }

    return false;
  };

  const [mode, setMode] = useState<ThemeMode>(getInitialMode);
  const [systemDark, setSystemDark] = useState(getInitialSystemDark);
  const themeSwitchRafRef = useRef<number | null>(null);

  useEffect(() => {
    if (mode !== "system") {
      localStorage.setItem("theme-mode", mode);
    } else {
      localStorage.removeItem("theme-mode");
    }
  }, [mode]);

  useEffect(() => {
    if (typeof window !== "undefined" && window.matchMedia) {
      const mq = window.matchMedia("(prefers-color-scheme: dark)");
      const update = () => setSystemDark(mq.matches);
      update();
      mq.addEventListener("change", update);
      return () => mq.removeEventListener("change", update);
    }
  }, []);

  const effectiveMode: "light" | "dark" =
    mode === "system" ? (systemDark ? "dark" : "light") : mode;
  const modeLabel: "Auto" | "Light" | "Dark" =
    mode === "system" ? "Auto" : mode === "dark" ? "Dark" : "Light";

  useLayoutEffect(() => {
    const root = document.documentElement;
    const { body } = document;

    if (themeSwitchRafRef.current !== null) {
      window.cancelAnimationFrame(themeSwitchRafRef.current);
    }

    root.classList.add("theme-switching");
    body.classList.add("theme-switching");
    body.classList.toggle("dark", effectiveMode === "dark");
    body.dataset.themeMode = mode;
    body.dataset.themeEffective = effectiveMode;
    root.dataset.themeMode = mode;
    root.dataset.themeEffective = effectiveMode;

    themeSwitchRafRef.current = window.requestAnimationFrame(() => {
      root.classList.remove("theme-switching");
      body.classList.remove("theme-switching");
      themeSwitchRafRef.current = null;
    });

    return () => {
      if (themeSwitchRafRef.current !== null) {
        window.cancelAnimationFrame(themeSwitchRafRef.current);
        themeSwitchRafRef.current = null;
      }
    };
  }, [effectiveMode, mode]);

  const cycleMode = () => {
    setMode((prev) =>
      prev === "system" ? "dark" : prev === "dark" ? "light" : "system"
    );
  };

  return (
    <ThemeContext.Provider value={{ mode, effectiveMode, modeLabel, cycleMode }}>
      {children}
    </ThemeContext.Provider>
  );
};

export const useTheme = () => {
  const ctx = useContext(ThemeContext);
  if (!ctx) throw new Error("useTheme must be used within ThemeProvider");
  return ctx;
};
