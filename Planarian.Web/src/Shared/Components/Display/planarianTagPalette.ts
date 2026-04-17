export type PlanarianTagPaletteColor = {
  background: string;
  border: string;
  text: string;
};

type PlanarianTagPaletteEntry = {
  light: PlanarianTagPaletteColor;
  dark: PlanarianTagPaletteColor;
};

const PALETTE: PlanarianTagPaletteEntry[] = [
  { light: { background: "#fde8e8", border: "#f5b7b1", text: "#7b241c" }, dark: { background: "#5f2422", border: "#a94442", text: "#f7d6d6" } },
  { light: { background: "#feece2", border: "#f8c8a8", text: "#7a3e12" }, dark: { background: "#5a3116", border: "#c97d3b", text: "#f8dec7" } },
  { light: { background: "#fff3d6", border: "#f0d08a", text: "#6d4c00" }, dark: { background: "#5b4714", border: "#b8942f", text: "#f7e8bc" } },
  { light: { background: "#f8f0c9", border: "#d9c36b", text: "#5e5200" }, dark: { background: "#52480d", border: "#a38e1c", text: "#f4ecb1" } },
  { light: { background: "#ecf7d6", border: "#c0df8a", text: "#3f5f00" }, dark: { background: "#324b11", border: "#8bb52a", text: "#def0b7" } },
  { light: { background: "#e3f6d8", border: "#acd892", text: "#22543d" }, dark: { background: "#1e4a31", border: "#4fa26d", text: "#d2f0dd" } },
  { light: { background: "#dff7ea", border: "#8fd2b2", text: "#0f5132" }, dark: { background: "#184636", border: "#3b9a73", text: "#cbf0df" } },
  { light: { background: "#def8f5", border: "#8ed9d0", text: "#0f4c5c" }, dark: { background: "#154650", border: "#3896a0", text: "#cef2ef" } },
  { light: { background: "#dff5fb", border: "#92d2eb", text: "#0c4a6e" }, dark: { background: "#173f57", border: "#3b89b8", text: "#d2edf9" } },
  { light: { background: "#e4f0ff", border: "#9ebcf8", text: "#1e3a8a" }, dark: { background: "#1f376b", border: "#4d74d1", text: "#dce6ff" } },
  { light: { background: "#e9ecff", border: "#b0b8f8", text: "#3730a3" }, dark: { background: "#2d2f71", border: "#686fd6", text: "#e0e2ff" } },
  { light: { background: "#efe7ff", border: "#c6b1f8", text: "#5b21b6" }, dark: { background: "#41266a", border: "#8754d0", text: "#eadfff" } },
  { light: { background: "#f6e4ff", border: "#d6a8ef", text: "#7a1d8d" }, dark: { background: "#51245d", border: "#aa4fbe", text: "#f2dfff" } },
  { light: { background: "#fde4f4", border: "#efabd3", text: "#9d174d" }, dark: { background: "#5f2247", border: "#c4548f", text: "#f7deed" } },
  { light: { background: "#ffe4ee", border: "#f5b1c7", text: "#9f1239" }, dark: { background: "#61263c", border: "#c25c81", text: "#f8dce7" } },
  { light: { background: "#fde7ec", border: "#f0b0c0", text: "#7f1d1d" }, dark: { background: "#5f272d", border: "#b75f72", text: "#f7dee3" } },
  { light: { background: "#f3ede4", border: "#d1c1aa", text: "#5b4636" }, dark: { background: "#48382c", border: "#9f8064", text: "#eadfce" } },
  { light: { background: "#f2f0ec", border: "#ccc3b7", text: "#57534e" }, dark: { background: "#44403b", border: "#8f8779", text: "#e8e2d9" } },
  { light: { background: "#eceff3", border: "#bcc6d0", text: "#334155" }, dark: { background: "#364150", border: "#7b8ba0", text: "#e1e7ee" } },
  { light: { background: "#e8eef5", border: "#b3c2d4", text: "#1f3c5c" }, dark: { background: "#2a3d55", border: "#6180a1", text: "#dde7f1" } },
  { light: { background: "#e7f2ef", border: "#adcfc2", text: "#134e4a" }, dark: { background: "#214843", border: "#5d998d", text: "#dbeee9" } },
  { light: { background: "#edf4e7", border: "#bfd4b0", text: "#365314" }, dark: { background: "#324625", border: "#7da35f", text: "#e4eedb" } },
  { light: { background: "#faf0df", border: "#dec69a", text: "#6b4f1d" }, dark: { background: "#56411f", border: "#ab8345", text: "#f4e6ca" } },
  { light: { background: "#f7ebeb", border: "#d8bcbc", text: "#5f3a3a" }, dark: { background: "#4c3333", border: "#967070", text: "#efe1e1" } },
];

const NEUTRAL_STYLE: PlanarianTagPaletteEntry = {
  light: {
    background: "#fafafa",
    border: "#d9d9d9",
    text: "rgba(0, 0, 0, 0.88)",
  },
  dark: {
    background: "#2a3038",
    border: "#3a4048",
    text: "#eee",
  },
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
  mode: "light" | "dark"
): PlanarianTagPaletteColor => {
  const paletteIndex = hashString(colorKey) % PALETTE.length;
  return PALETTE[paletteIndex][mode];
};

export const getPlanarianNeutralTagColor = (
  mode: "light" | "dark"
): PlanarianTagPaletteColor => NEUTRAL_STYLE[mode];
