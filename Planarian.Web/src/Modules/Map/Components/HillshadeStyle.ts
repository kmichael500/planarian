export type MapTheme = "light" | "dark";

type Rgb = readonly [number, number, number];

export type HillshadePaletteColorKey =
  | "backgroundColor"
  | "shadowColor"
  | "highlightColor"
  | "accentColor";

export interface HillshadePaletteColors {
  backgroundColor: string;
  shadowColor: string;
  highlightColor: string;
  accentColor: string;
}

interface HillshadePalette extends HillshadePaletteColors {
  shadowOpacity: number;
  highlightOpacity: number;
  accentOpacity: number;
}

export interface HillshadePreferences {
  exaggeration: number;
  illuminationDirection: number;
  contrast: number;
  paletteOverrides: Partial<Record<MapTheme, Partial<HillshadePaletteColors>>>;
}

const HILLSHADE_PALETTES: Record<MapTheme, HillshadePalette> = {
  light: {
    backgroundColor: "#d9dcda",
    shadowColor: "#1c2022",
    shadowOpacity: 0.62,
    highlightColor: "#ffffff",
    highlightOpacity: 0.72,
    accentColor: "#2a2e30",
    accentOpacity: 0.32,
  },
  dark: {
    backgroundColor: "#34383a",
    shadowColor: "#050708",
    shadowOpacity: 0.72,
    highlightColor: "#f4f7f6",
    highlightOpacity: 0.62,
    accentColor: "#0a0c0e",
    accentOpacity: 0.42,
  },
};

export const HILLSHADE_ILLUMINATION_DIRECTION = 315;
export const HILLSHADE_EXAGGERATION = 1;
export const HILLSHADE_CONTRAST = 1.3;

export const DEFAULT_HILLSHADE_PREFERENCES: HillshadePreferences = {
  exaggeration: HILLSHADE_EXAGGERATION,
  illuminationDirection: HILLSHADE_ILLUMINATION_DIRECTION,
  contrast: HILLSHADE_CONTRAST,
  paletteOverrides: {},
};

const clamp = (value: number, min: number, max: number) =>
  Math.max(min, Math.min(max, value));

const normalizeDirection = (value: number) => ((value % 360) + 360) % 360;
const isHexColor = (value: unknown): value is string =>
  typeof value === "string" && /^#[0-9a-f]{6}$/i.test(value);

const normalizePaletteOverride = (
  value: unknown
): Partial<HillshadePaletteColors> | undefined => {
  if (!value || typeof value !== "object") return undefined;
  const candidate = value as Record<string, unknown>;
  const normalized: Partial<HillshadePaletteColors> = {};
  (
    [
      "backgroundColor",
      "shadowColor",
      "highlightColor",
      "accentColor",
    ] as HillshadePaletteColorKey[]
  ).forEach((key) => {
    const color = candidate[key];
    if (isHexColor(color)) normalized[key] = color;
  });
  return Object.keys(normalized).length ? normalized : undefined;
};

export const normalizeHillshadePreferences = (
  value: unknown
): HillshadePreferences => {
  const candidate =
    value && typeof value === "object"
      ? (value as Record<string, unknown>)
      : {};
  const exaggeration =
    typeof candidate.exaggeration === "number" &&
    Number.isFinite(candidate.exaggeration)
      ? clamp(candidate.exaggeration, 0, 1)
      : HILLSHADE_EXAGGERATION;
  const direction =
    typeof candidate.illuminationDirection === "number" &&
    Number.isFinite(candidate.illuminationDirection)
      ? normalizeDirection(candidate.illuminationDirection)
      : HILLSHADE_ILLUMINATION_DIRECTION;
  const contrast =
    typeof candidate.contrast === "number" &&
    Number.isFinite(candidate.contrast)
      ? clamp(candidate.contrast, 0.5, 1.5)
      : HILLSHADE_CONTRAST;
  const rawOverrides =
    candidate.paletteOverrides && typeof candidate.paletteOverrides === "object"
      ? (candidate.paletteOverrides as Record<string, unknown>)
      : {};
  const paletteOverrides: HillshadePreferences["paletteOverrides"] = {};
  const light = normalizePaletteOverride(rawOverrides.light);
  const dark = normalizePaletteOverride(rawOverrides.dark);
  if (light) paletteOverrides.light = light;
  if (dark) paletteOverrides.dark = dark;

  return {
    exaggeration,
    illuminationDirection: direction,
    contrast,
    paletteOverrides,
  };
};

export const getHillshadePaletteColors = (
  theme: MapTheme,
  preferences: HillshadePreferences = DEFAULT_HILLSHADE_PREFERENCES
): HillshadePaletteColors => {
  const palette = HILLSHADE_PALETTES[theme];
  return {
    backgroundColor:
      preferences.paletteOverrides[theme]?.backgroundColor ??
      palette.backgroundColor,
    shadowColor:
      preferences.paletteOverrides[theme]?.shadowColor ?? palette.shadowColor,
    highlightColor:
      preferences.paletteOverrides[theme]?.highlightColor ??
      palette.highlightColor,
    accentColor:
      preferences.paletteOverrides[theme]?.accentColor ?? palette.accentColor,
  };
};

const hexToRgb = (hex: string): Rgb => [
  parseInt(hex.slice(1, 3), 16),
  parseInt(hex.slice(3, 5), 16),
  parseInt(hex.slice(5, 7), 16),
];

const rgba = (hex: string, opacity: number) => {
  const [red, green, blue] = hexToRgb(hex);
  const normalizedOpacity = Math.round(clamp(opacity, 0, 1) * 1000) / 1000;
  return `rgba(${red}, ${green}, ${blue}, ${normalizedOpacity})`;
};

export const getHillshadeStyle = (
  theme: MapTheme,
  opacity: number,
  preferences: HillshadePreferences = DEFAULT_HILLSHADE_PREFERENCES
) => {
  const palette = HILLSHADE_PALETTES[theme];
  const colors = getHillshadePaletteColors(theme, preferences);
  const normalizedOpacity = clamp(opacity, 0, 1);
  const contrast = clamp(preferences.contrast, 0.5, 1.5);

  return {
    backgroundColor: colors.backgroundColor,
    paint: {
      "hillshade-exaggeration": clamp(preferences.exaggeration, 0, 1),
      "hillshade-illumination-anchor": "map" as const,
      "hillshade-illumination-direction": normalizeDirection(
        preferences.illuminationDirection
      ),
      "hillshade-shadow-color": rgba(
        colors.shadowColor,
        palette.shadowOpacity * normalizedOpacity * contrast
      ),
      "hillshade-highlight-color": rgba(
        colors.highlightColor,
        palette.highlightOpacity * normalizedOpacity * contrast
      ),
      "hillshade-accent-color": rgba(
        colors.accentColor,
        palette.accentOpacity * normalizedOpacity * contrast
      ),
    },
  };
};
