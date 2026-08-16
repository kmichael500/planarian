import { FeatureCollection } from "geojson";

const readText = (file: File): Promise<string> => new Promise((resolve, reject) => {
  const reader = new FileReader();
  reader.onload = () => resolve(String(reader.result ?? ""));
  reader.onerror = () => reject(reader.error ?? new Error("The file could not be read."));
  reader.readAsText(file);
});

const isFeatureCollection = (value: unknown): value is FeatureCollection =>
  !!value && typeof value === "object" &&
  (value as { type?: string }).type === "FeatureCollection" &&
  Array.isArray((value as { features?: unknown[] }).features);

export const parseZippedShapefile = async (file: File): Promise<FeatureCollection[]> => {
  const { default: shpjs } = await import("shpjs");
  const parsed = await shpjs(await file.arrayBuffer());
  const collections = (Array.isArray(parsed) ? parsed : [parsed]) as unknown[];
  if (collections.length === 0 || collections.some((value) => !isFeatureCollection(value))) {
    throw new Error("The ZIP did not contain a valid shapefile FeatureCollection.");
  }
  return collections as FeatureCollection[];
};

export const parseCaveLinePlotFile = async (file: File): Promise<FeatureCollection[]> => {
  const lowerName = file.name.toLowerCase();
  if (lowerName.endsWith(".zip")) return parseZippedShapefile(file);

  if (!lowerName.endsWith(".geojson") && !lowerName.endsWith(".json")) {
    throw new Error("Choose a .zip shapefile, .geojson, or .json file.");
  }

  const parsed = JSON.parse(await readText(file)) as unknown;
  const collections = Array.isArray(parsed) ? parsed : [parsed];
  if (collections.length === 0 || collections.some((value) => !isFeatureCollection(value))) {
    throw new Error("GeoJSON must contain a FeatureCollection or an array of FeatureCollections.");
  }
  return collections as FeatureCollection[];
};
