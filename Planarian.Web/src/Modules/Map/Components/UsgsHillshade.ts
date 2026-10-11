export type UsgsHillshadeVariant = "gray" | "gray-stretch" | "multidirectional";

interface UsgsHillshadeVariantDefinition {
  value: UsgsHillshadeVariant;
  label: string;
  rasterFunction: string;
}

export const USGS_HILLSHADE_VARIANTS: readonly UsgsHillshadeVariantDefinition[] =
  [
    {
      value: "gray",
      label: "Gray",
      rasterFunction: "Hillshade Gray",
    },
    {
      value: "gray-stretch",
      label: "Gray-Stretch",
      rasterFunction: "Hillshade Gray-Stretch",
    },
    {
      value: "multidirectional",
      label: "Multidirectional",
      rasterFunction: "Hillshade Multidirectional",
    },
  ];

export const DEFAULT_USGS_HILLSHADE_VARIANT: UsgsHillshadeVariant =
  "multidirectional";

const USGS_3DEP_EXPORT_IMAGE_URL =
  "https://elevation.nationalmap.gov/arcgis/rest/services/3DEPElevation/ImageServer/exportImage";

export const normalizeUsgsHillshadeVariant = (
  value: unknown
): UsgsHillshadeVariant =>
  USGS_HILLSHADE_VARIANTS.some((variant) => variant.value === value)
    ? (value as UsgsHillshadeVariant)
    : DEFAULT_USGS_HILLSHADE_VARIANT;

const getUsgsHillshadeVariantDefinition = (variant: UsgsHillshadeVariant) =>
  USGS_HILLSHADE_VARIANTS.find((candidate) => candidate.value === variant) ??
  USGS_HILLSHADE_VARIANTS.find(
    (candidate) => candidate.value === DEFAULT_USGS_HILLSHADE_VARIANT
  )!;

export const getUsgsHillshadeTileUrl = (variant: UsgsHillshadeVariant) => {
  const { rasterFunction } = getUsgsHillshadeVariantDefinition(variant);
  const renderingRule = encodeURIComponent(JSON.stringify({ rasterFunction }));

  return (
    USGS_3DEP_EXPORT_IMAGE_URL +
    "?bbox={bbox-epsg-3857}" +
    "&bboxSR=3857" +
    "&imageSR=3857" +
    "&size=256,256" +
    "&format=png32" +
    "&transparent=true" +
    "&renderingRule=" +
    renderingRule +
    "&f=image"
  );
};
