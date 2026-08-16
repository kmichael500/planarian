import { parseCaveLinePlotFile } from "./LinePlotFileParser";

const file = (name: string, content: string) => new File([content], name, { type: "application/json" });

it("parses one GeoJSON FeatureCollection without changing its feature ordering", async () => {
  const result = await parseCaveLinePlotFile(file("survey.geojson",
    '{"type":"FeatureCollection","features":[{"type":"Feature","properties":{"n":1},"geometry":null},{"type":"Feature","properties":{"n":2},"geometry":null}]}'));

  expect(result).toHaveLength(1);
  expect(result[0].features.map(feature => feature.properties?.n)).toEqual([1, 2]);
});

it("accepts an array of FeatureCollections", async () => {
  const result = await parseCaveLinePlotFile(file("survey.json",
    '[{"type":"FeatureCollection","features":[]},{"type":"FeatureCollection","features":[]}]'));

  expect(result).toHaveLength(2);
});

it("rejects JSON that is not a FeatureCollection", async () => {
  await expect(parseCaveLinePlotFile(file("survey.json", '{"type":"Feature","properties":{}}')))
    .rejects.toThrow("FeatureCollection");
});

it("rejects unsupported file extensions before parsing", async () => {
  await expect(parseCaveLinePlotFile(file("survey.txt", '{"type":"FeatureCollection","features":[]}')))
    .rejects.toThrow("Choose a .zip shapefile, .geojson, or .json file.");
});
