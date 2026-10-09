import {
  DEFAULT_HILLSHADE_PREFERENCES,
  getHillshadePaletteColors,
  getHillshadeStyle,
  HILLSHADE_CONTRAST,
  HILLSHADE_EXAGGERATION,
  HILLSHADE_ILLUMINATION_DIRECTION,
  normalizeHillshadePreferences,
} from "./HillshadeStyle";

const alpha = (rgba: string) => {
  const match = rgba.match(/rgba\([^,]+,[^,]+,[^,]+,\s*([^)]+)\)/);
  if (!match) throw new Error(`Expected rgba color, received: ${rgba}`);
  return Number(match[1]);
};

const shadeColors = (style: ReturnType<typeof getHillshadeStyle>) => [
  style.paint["hillshade-shadow-color"],
  style.paint["hillshade-highlight-color"],
  style.paint["hillshade-accent-color"],
];

describe("getHillshadeStyle", () => {
  test("uses Planarian's high-detail hillshade defaults", () => {
    expect(DEFAULT_HILLSHADE_PREFERENCES).toMatchObject({
      exaggeration: 1,
      contrast: 1.3,
      illuminationDirection: 315,
    });
    expect(HILLSHADE_EXAGGERATION).toBe(1);
    expect(HILLSHADE_CONTRAST).toBe(1.3);
    expect(HILLSHADE_ILLUMINATION_DIRECTION).toBe(315);
  });

  test("keeps terrain geometry and illumination semantics identical across default themes", () => {
    const light = getHillshadeStyle("light", 1);
    const dark = getHillshadeStyle("dark", 1);

    expect(light.paint["hillshade-exaggeration"]).toBe(HILLSHADE_EXAGGERATION);
    expect(dark.paint["hillshade-exaggeration"]).toBe(HILLSHADE_EXAGGERATION);
    expect(light.paint["hillshade-illumination-anchor"]).toBe("map");
    expect(dark.paint["hillshade-illumination-anchor"]).toBe("map");
    expect(light.paint["hillshade-illumination-direction"]).toBe(
      HILLSHADE_ILLUMINATION_DIRECTION
    );
    expect(dark.paint["hillshade-illumination-direction"]).toBe(
      HILLSHADE_ILLUMINATION_DIRECTION
    );

    expect(light.backgroundColor).not.toBe(dark.backgroundColor);
    expect(shadeColors(light)).not.toEqual(shadeColors(dark));
  });

  test("applies user relief and light direction without changing map anchoring", () => {
    const customized = getHillshadeStyle("light", 1, {
      ...DEFAULT_HILLSHADE_PREFERENCES,
      exaggeration: 0.4,
      illuminationDirection: 90,
    });

    expect(customized.paint["hillshade-exaggeration"]).toBe(0.4);
    expect(customized.paint["hillshade-illumination-direction"]).toBe(90);
    expect(customized.paint["hillshade-illumination-anchor"]).toBe("map");
  });

  test("scales shade strength with contrast independently of the default contrast", () => {
    const lowContrast = getHillshadeStyle("light", 1, {
      ...DEFAULT_HILLSHADE_PREFERENCES,
      contrast: 0.75,
    });
    const highContrast = getHillshadeStyle("light", 1, {
      ...DEFAULT_HILLSHADE_PREFERENCES,
      contrast: 1.25,
    });

    shadeColors(lowContrast).forEach((color, index) => {
      expect(alpha(shadeColors(highContrast)[index])).toBeGreaterThan(
        alpha(color)
      );
    });
  });

  test("scales shade strength with layer opacity without changing the theme background", () => {
    const full = getHillshadeStyle("light", 1);
    const half = getHillshadeStyle("light", 0.5);

    expect(half.backgroundColor).toBe(full.backgroundColor);
    shadeColors(full).forEach((color, index) => {
      expect(alpha(shadeColors(half)[index])).toBeCloseTo(alpha(color) / 2, 3);
    });
  });

  test("uses palette overrides only for the selected theme", () => {
    const preferences = {
      ...DEFAULT_HILLSHADE_PREFERENCES,
      paletteOverrides: {
        light: {
          backgroundColor: "#112233",
          shadowColor: "#445566",
        },
      },
    };

    expect(getHillshadePaletteColors("light", preferences)).toMatchObject({
      backgroundColor: "#112233",
      shadowColor: "#445566",
    });
    expect(getHillshadePaletteColors("dark", preferences)).toEqual(
      getHillshadePaletteColors("dark")
    );
  });

  test("clamps invalid persisted settings and ignores invalid palette values", () => {
    const normalized = normalizeHillshadePreferences({
      exaggeration: 9,
      illuminationDirection: -45,
      contrast: 0,
      paletteOverrides: {
        light: {
          backgroundColor: "#123456",
          shadowColor: "not-a-color",
        },
      },
    });

    expect(normalized.exaggeration).toBe(1);
    expect(normalized.illuminationDirection).toBe(315);
    expect(normalized.contrast).toBe(0.5);
    expect(normalized.paletteOverrides.light).toEqual({
      backgroundColor: "#123456",
    });
  });

  test("clamps opacity so invalid values cannot overdrive the hillshade", () => {
    const zero = getHillshadeStyle("dark", 0);
    const belowZero = getHillshadeStyle("dark", -1);
    const full = getHillshadeStyle("dark", 1);
    const aboveFull = getHillshadeStyle("dark", 2);

    expect(shadeColors(belowZero)).toEqual(shadeColors(zero));
    expect(shadeColors(aboveFull)).toEqual(shadeColors(full));
    shadeColors(zero).forEach((color) => expect(alpha(color)).toBe(0));
  });
});
