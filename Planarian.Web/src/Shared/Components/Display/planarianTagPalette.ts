export type PlanarianTagPaletteColor = {
  background: string;
  border: string;
  text: string;
};

const getCssPaletteColor = (index: number): PlanarianTagPaletteColor => ({
  background: `var(--planarian-palette-${index}-background)`,
  border: `var(--planarian-palette-${index}-border)`,
  text: `var(--planarian-palette-${index}-text)`,
});

const PALETTE: PlanarianTagPaletteColor[] = Array.from(
  { length: 24 },
  (_, index) => getCssPaletteColor(index)
);

const NEUTRAL_STYLE: PlanarianTagPaletteColor = {
  background: "var(--planarian-palette-neutral-background)",
  border: "var(--planarian-palette-neutral-border)",
  text: "var(--planarian-palette-neutral-text)",
};

const hashString = (value: string) => {
  let hash = 0;

  for (let i = 0; i < value.length; i += 1) {
    hash = value.charCodeAt(i) + ((hash << 5) - hash);
  }

  return Math.abs(hash);
};

export const getPlanarianTagPaletteColor = (
  colorKey: string,
  _mode: "light" | "dark"
): PlanarianTagPaletteColor => {
  const paletteIndex = hashString(colorKey) % PALETTE.length;
  return PALETTE[paletteIndex];
};

export const getPlanarianNeutralTagColor = (
  _mode: "light" | "dark"
): PlanarianTagPaletteColor => NEUTRAL_STYLE;
