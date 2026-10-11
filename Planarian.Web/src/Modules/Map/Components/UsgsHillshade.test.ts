import {
  DEFAULT_USGS_HILLSHADE_VARIANT,
  getUsgsHillshadeTileUrl,
  normalizeUsgsHillshadeVariant,
  USGS_HILLSHADE_VARIANTS,
} from "./UsgsHillshade";

describe("USGS 3DEP hillshade", () => {
  test("uses multidirectional hillshade by default", () => {
    expect(DEFAULT_USGS_HILLSHADE_VARIANT).toBe("multidirectional");
  });

  test.each(USGS_HILLSHADE_VARIANTS)(
    "builds a dynamic 3DEP exportImage URL for $label",
    ({ value, rasterFunction }) => {
      const tileUrl = getUsgsHillshadeTileUrl(value);
      const parsed = new URL(tileUrl.replace("{bbox-epsg-3857}", "0,0,1,1"));

      expect(parsed.hostname).toBe("elevation.nationalmap.gov");
      expect(parsed.pathname).toBe(
        "/arcgis/rest/services/3DEPElevation/ImageServer/exportImage"
      );
      expect(parsed.searchParams.get("bboxSR")).toBe("3857");
      expect(parsed.searchParams.get("imageSR")).toBe("3857");
      expect(parsed.searchParams.get("size")).toBe("256,256");
      expect(parsed.searchParams.get("format")).toBe("png32");
      expect(parsed.searchParams.get("f")).toBe("image");
      expect(
        JSON.parse(parsed.searchParams.get("renderingRule") ?? "{}")
      ).toEqual({ rasterFunction });
      expect(tileUrl).not.toContain("USGSShadedReliefOnly");
    }
  );

  test("falls back safely when a persisted variant is unsupported", () => {
    expect(normalizeUsgsHillshadeVariant("multidirectional")).toBe(
      "multidirectional"
    );
    expect(normalizeUsgsHillshadeVariant("not-real")).toBe(
      DEFAULT_USGS_HILLSHADE_VARIANT
    );
    expect(normalizeUsgsHillshadeVariant(null)).toBe(
      DEFAULT_USGS_HILLSHADE_VARIANT
    );
  });
});
