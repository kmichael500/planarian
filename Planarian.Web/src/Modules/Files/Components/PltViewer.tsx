import { Result, Spin } from "antd";
import { useEffect, useMemo, useState } from "react";
import type { Feature, FeatureCollection } from "geojson";
import bbox from "@turf/bbox";
import proj4 from "proj4";
import { MapBaseComponent } from "../../Map/Components/MapBaseComponent";
import { Source, Layer, useMap } from "react-map-gl/maplibre";
import { useFitMapBounds } from "../../Map/Hooks/useFitMapBounds";

const FEET_TO_METERS = 0.3048;
const PLT_SOURCE_ID = "plt-viewer-source";
const PLT_LAYER_ID = "plt-viewer-line";

type BoundsTuple = [[number, number], [number, number]];

type PltParseResult = {
  collection: FeatureCollection;
  crs?: string | null;
};

type SupportedDatum = "WGS84" | "NAD83";

interface ProjectionInfo {
  epsg: string;
  zone: number;
  isSouthernHemisphere: boolean;
  datum: SupportedDatum;
}

interface PltViewerProps {
  embedUrl: string;
  downloadButton?: React.ReactNode;
}

export const PltViewer: React.FC<PltViewerProps> = ({
  embedUrl,
  downloadButton,
}) => {
  const [result, setResult] = useState<PltParseResult | null>(null);
  const [bounds, setBounds] = useState<BoundsTuple | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let isCancelled = false;

    const fetchPlt = async () => {
      setIsLoading(true);
      setError(null);
      setResult(null);
      setBounds(null);

      try {
        const response = await fetch(embedUrl);
        if (!response.ok) {
          throw new Error("Unable to download PLT file.");
        }
        const raw = await response.text();
        const parsed = parsePlt(raw);

        if (!parsed || parsed.collection.features.length === 0) {
          throw new Error("No linework found in PLT file.");
        }

        const calculatedBounds = bbox(parsed.collection) as [
          number,
          number,
          number,
          number
        ];
        if (calculatedBounds.some((value) => !Number.isFinite(value))) {
          throw new Error("Unable to determine spatial extent for PLT file.");
        }

        const boundsTuple: BoundsTuple = [
          [calculatedBounds[0], calculatedBounds[1]],
          [calculatedBounds[2], calculatedBounds[3]],
        ];

        if (!isCancelled) {
          setResult(parsed);
          setBounds(boundsTuple);
          setIsLoading(false);
        }
      } catch (e) {
        if (!isCancelled) {
          setIsLoading(false);
          setError(
            e instanceof Error ? e.message : "Failed to parse PLT file."
          );
        }
      }
    };

    fetchPlt();

    return () => {
      isCancelled = true;
    };
  }, [embedUrl]);

  const center = useMemo<[number, number] | undefined>(() => {
    if (!bounds) {
      return undefined;
    }
    const [[minLng, minLat], [maxLng, maxLat]] = bounds;
    return [(minLat + maxLat) / 2, (minLng + maxLng) / 2];
  }, [bounds]);

  if (error && !isLoading) {
    return <Result status="warning" title={error} extra={downloadButton} />;
  }

  if (isLoading || !result || !bounds || !center) {
    return (
      <div
        style={{
          width: "100%",
          height: "100%",
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
        }}
      >
        <Spin />
      </div>
    );
  }

  return (
    <div style={{ width: "100%", height: "100%" }}>
      <MapBaseComponent
        key={embedUrl}
        initialCenter={center}
        initialZoom={10}
        initialBounds={bounds}
        initialFitBoundsOptions={{ maxZoom: 15 }}
        onCaveClicked={() => { }}
        onNonCaveClicked={() => { }}
        manageBodyPadding={false}
        showFullScreenControl={false}
        onMoveEnd={() => { }}
        additionalInteractiveLayerIds={[PLT_LAYER_ID]}
        reuseMaps={false}
      >
        <PltOverlay data={result.collection} bounds={bounds} />
      </MapBaseComponent>
    </div>
  );
};

interface PltOverlayProps {
  data: FeatureCollection;
  bounds: BoundsTuple;
}

const PltOverlay: React.FC<PltOverlayProps> = ({ data, bounds }) => {
  const map = useMap();
  const mapRef = map?.current;

  useFitMapBounds(mapRef, bounds, { maxZoom: 15, padding: 20 });

  return (
    <Source id={PLT_SOURCE_ID} type="geojson" data={data}>
      <Layer
        id={PLT_LAYER_ID}
        type="line"
        layout={{ "line-join": "round", "line-cap": "round" }}
        paint={{
          "line-color": "#00008B",
          "line-width": 3,
          "line-opacity": 0.9,
        }}
        filter={[
          "==",
          ["geometry-type"],
          "LineString",
        ]}
      />
    </Source>
  );
};

function parsePlt(raw: string): PltParseResult | null {
  const lines = raw.split(/\r?\n/);
  if (lines.length === 0) {
    return null;
  }

  let datum: string | null = null;
  let utmZone: string | null = null;

  const features: Feature[] = [];
  let currentCoords: number[][] = [];
  let currentProps: Record<string, unknown> = {};
  let lastCoords: number[] | null = null;
  let surveyName = "Unknown Survey";
  let sectionName = "Default Section";

  const maxHeaderLines = 10;
  let processedHeaderLines = 0;

  for (const rawLine of lines) {
    const line = rawLine.trim();
    if (!line) {
      continue;
    }

    if (processedHeaderLines < maxHeaderLines && (!datum || !utmZone)) {
      processedHeaderLines += 1;
      const command = line[0];
      const content = line.slice(1).trim();

      if (command === "O" && !datum) {
        datum = content;
      } else if (command === "G" && !utmZone) {
        utmZone = content;
      }
    } else if (datum && utmZone) {
      break;
    }
  }

  const projection = resolveProjection(datum, utmZone);
  const epsg = projection?.epsg ?? null;

  const pushFeature = () => {
    if (currentCoords.length > 1) {
      features.push({
        type: "Feature",
        geometry: {
          type: "LineString",
          coordinates: currentCoords,
        },
        properties: { ...currentProps },
      });
    }
  };

  for (const rawLine of lines) {
    const line = rawLine.trim();
    if (!line) {
      continue;
    }

    const command = line[0];
    const remainder = line.slice(1);
    const tokens = tokenizeParts(remainder);

    switch (command.toUpperCase()) {
      case "M": {
        pushFeature();
        const { coords, props } = parseCoordsAndProperties(tokens);
        if (coords) {
          currentCoords = [coords];
          currentProps = {
            survey: surveyName,
            section: sectionName,
            command: command,
            ...props,
          };
          lastCoords = coords;
        }
        break;
      }
      case "D": {
        const { coords, props } = parseCoordsAndProperties(tokens);
        if (coords) {
          if (currentCoords.length === 0 && lastCoords) {
            currentCoords.push(lastCoords);
          }
          currentCoords.push(coords);
          currentProps = {
            ...currentProps,
            ...props,
          };
          lastCoords = coords;
        }
        break;
      }
      case "N": {
        pushFeature();
        currentCoords = [];
        lastCoords = null;
        surveyName = tokens[0] || "Unnamed Survey";
        currentProps = {
          survey: surveyName,
          section: sectionName,
        };
        break;
      }
      case "S": {
        pushFeature();
        currentCoords = [];
        lastCoords = null;
        sectionName = remainder.trim() || "Unnamed Section";
        currentProps = {
          survey: surveyName,
          section: sectionName,
        };
        break;
      }
      default:
        break;
    }
  }

  pushFeature();

  const featureCollection: FeatureCollection = {
    type: "FeatureCollection",
    features,
  };

  if (projection) {
    const converter = createCoordinateConverter(projection);
    applyCoordinateTransform(featureCollection, converter);
    if (featureCollection.features.length === 0) {
      throw new Error(
        "Unable to transform PLT coordinates to longitude/latitude."
      );
    }
  } else if (!coordinatesLookGeographic(featureCollection)) {
    throw new Error(
      "Unsupported or missing coordinate reference system in PLT header."
    );
  }

  if (epsg) {
    (featureCollection as FeatureCollection & { crs?: any }).crs = {
      type: "name",
      properties: {
        name: `urn:ogc:def:crs:${epsg}`,
      },
    };
  }

  return { collection: featureCollection, crs: epsg };
}

function tokenizeParts(input: string): string[] {
  const tokens: string[] = [];
  const regex = /\"(?:\\\"|[^\"])*\"|\S+/g;
  const matches = input.match(regex);
  if (!matches) {
    return tokens;
  }
  return matches.map((token) => token.trim());
}

function parseCoordsAndProperties(parts: string[]) {
  const coords: number[] = [];
  const props: Record<string, unknown> = {};

  let i = 0;
  while (i < parts.length && coords.length < 3) {
    const value = Number(parts[i]);
    if (Number.isFinite(value)) {
      coords.push(value);
      i += 1;
    } else {
      break;
    }
  }

  if (coords.length !== 3) {
    return { coords: null, props };
  }

  while (i < parts.length) {
    const token = parts[i];
    if (token.startsWith("S") && token.length > 1) {
      props.station = token.slice(1);
      i += 1;
    } else if (token.startsWith("F") && token.length > 1) {
      props.flags = token.slice(1);
      i += 1;
    } else if (token.startsWith('"')) {
      const comment = parseComment(parts.slice(i));
      if (comment) {
        props.comment = comment;
      }
      break;
    } else {
      i += 1;
    }
  }

  const [northFt, eastFt, verticalFt] = coords;
  const geojsonCoords = [
    eastFt * FEET_TO_METERS,
    northFt * FEET_TO_METERS,
    verticalFt * FEET_TO_METERS,
  ];

  return { coords: geojsonCoords, props };
}

function parseComment(tokens: string[]): string | null {
  const joined = tokens.join(" ");
  const match = joined.match(/\"((?:\\\"|[^\"])*)\"/);
  if (!match) {
    return null;
  }
  return match[1].replace(/\\\"/g, '"');
}

const WGS84_ALIASES = new Set([
  "WGS84",
  "WGS1984",
  "WORLDGEODETICSYSTEM1984",
  "WGS84G1762",
  "WGS84G1674",
]);

const NAD83_ALIASES = new Set([
  "NAD83",
  "NAD1983",
  "NORTHAMERICAN1983",
  "NORTHAMERICANDATUM1983",
  "NAD83CSRS",
  "NORTHAMERICAN1983CSRS",
]);

function resolveProjection(
  datum: string | null,
  zone: string | null
): ProjectionInfo | null {
  if (!datum || !zone) {
    return null;
  }

  const datumToken = normalizeDatumToken(datum);
  const zoneMatch = zone.match(/(-?)(\d{1,2})/);
  if (!zoneMatch) {
    return null;
  }

  const [, sign, zoneDigits] = zoneMatch;
  const zoneNumber = Number(zoneDigits);
  if (!Number.isFinite(zoneNumber) || zoneNumber < 1 || zoneNumber > 60) {
    return null;
  }

  const isSouthernHemisphere = sign === "-";

  const isWgs84 =
    WGS84_ALIASES.has(datumToken) || datumToken.startsWith("WGS84");
  if (isWgs84) {
    const epsgPrefix = isSouthernHemisphere ? "327" : "326";
    return {
      epsg: `EPSG:${epsgPrefix}${padZoneNumber(zoneNumber)}`,
      zone: zoneNumber,
      isSouthernHemisphere,
      datum: "WGS84",
    };
  }

  const isNad83 =
    NAD83_ALIASES.has(datumToken) ||
    datumToken.startsWith("NAD83") ||
    datumToken.startsWith("NORTHAMERICAN1983");

  if (isNad83 && !isSouthernHemisphere) {
    return {
      epsg: `EPSG:269${padZoneNumber(zoneNumber)}`,
      zone: zoneNumber,
      isSouthernHemisphere,
      datum: "NAD83",
    };
  }

  return null;
}

function normalizeDatumToken(value: string): string {
  return value.toUpperCase().replace(/[^A-Z0-9]/g, "");
}

function padZoneNumber(zone: number): string {
  return zone.toString().padStart(2, "0");
}

function ensureProjectionDefinition(info: ProjectionInfo) {
  if (proj4.defs(info.epsg)) {
    return;
  }

  const parts = [
    "+proj=utm",
    `+zone=${info.zone}`,
    `+datum=${info.datum}`,
    "+units=m",
    "+no_defs",
    info.isSouthernHemisphere ? "+south" : undefined,
    "+type=crs",
  ].filter((part): part is string => Boolean(part));

  proj4.defs(info.epsg, parts.join(" "));
}

function createCoordinateConverter(
  info: ProjectionInfo
): (coordinate: number[]) => number[] | null {
  ensureProjectionDefinition(info);

  return (coordinate: number[]): number[] | null => {
    if (coordinate.length < 2) {
      return null;
    }

    const [x, y, z] = coordinate;
    if (!Number.isFinite(x) || !Number.isFinite(y)) {
      return null;
    }

    const [lon, lat] = proj4(info.epsg, proj4.WGS84, [x, y]);
    if (!Number.isFinite(lon) || !Number.isFinite(lat)) {
      return null;
    }

    if (typeof z === "number" && Number.isFinite(z)) {
      return [lon, lat, z];
    }

    return [lon, lat];
  };
}

function applyCoordinateTransform(
  collection: FeatureCollection,
  convert: (coordinate: number[]) => number[] | null
): void {
  const transformedFeatures: Feature[] = [];

  for (const feature of collection.features) {
    if (!feature.geometry) {
      continue;
    }

    const transformedGeometry = transformGeometryToWgs84(
      feature.geometry,
      convert
    );

    if (transformedGeometry) {
      feature.geometry = transformedGeometry;
      transformedFeatures.push(feature);
    }
  }

  collection.features = transformedFeatures;
}

function transformGeometryToWgs84(
  geometry: Feature["geometry"],
  convert: (coordinate: number[]) => number[] | null
): Feature["geometry"] | null {
  if (!geometry) {
    return null;
  }

  switch (geometry.type) {
    case "LineString": {
      const transformed = transformLineStringCoordinates(
        geometry.coordinates as number[][],
        convert
      );
      if (!transformed) {
        return null;
      }
      return { ...geometry, coordinates: transformed };
    }
    case "MultiLineString": {
      const transformedLines = (geometry.coordinates as number[][][])
        .map((line) => transformLineStringCoordinates(line, convert))
        .filter((line): line is number[][] => Boolean(line));
      if (transformedLines.length === 0) {
        return null;
      }
      return { ...geometry, coordinates: transformedLines };
    }
    case "Point": {
      const transformed = convert(geometry.coordinates as number[]);
      return transformed ? { ...geometry, coordinates: transformed } : null;
    }
    case "MultiPoint": {
      const transformed = (geometry.coordinates as number[][])
        .map((coord) => convert(coord))
        .filter((coord): coord is number[] => Boolean(coord));
      if (transformed.length === 0) {
        return null;
      }
      return { ...geometry, coordinates: transformed };
    }
    default:
      return geometry;
  }
}

function transformLineStringCoordinates(
  coordinates: number[][],
  convert: (coordinate: number[]) => number[] | null
): number[][] | null {
  const transformed: number[][] = [];

  for (const coord of coordinates) {
    const next = convert(coord);
    if (next) {
      transformed.push(next);
    }
  }

  return transformed.length > 1 ? transformed : null;
}

function coordinatesLookGeographic(collection: FeatureCollection): boolean {
  for (const feature of collection.features) {
    if (!feature.geometry) {
      continue;
    }

    if (!geometryLooksGeographic(feature.geometry)) {
      return false;
    }
  }

  return true;
}

function geometryLooksGeographic(geometry: Feature["geometry"]): boolean {
  if (!geometry) {
    return true;
  }

  switch (geometry.type) {
    case "LineString":
      return positionsLookGeographic(geometry.coordinates as number[][]);
    case "MultiLineString":
      return (geometry.coordinates as number[][][]).every((line) =>
        positionsLookGeographic(line)
      );
    case "Point":
      return coordinateLooksGeographic(geometry.coordinates as number[]);
    case "MultiPoint":
      return (geometry.coordinates as number[][]).every((coord) =>
        coordinateLooksGeographic(coord)
      );
    default:
      return true;
  }
}

function positionsLookGeographic(coordinates: number[][]): boolean {
  return coordinates.every((coord) => coordinateLooksGeographic(coord));
}

function coordinateLooksGeographic(coordinate: number[]): boolean {
  if (coordinate.length < 2) {
    return false;
  }

  const [lon, lat] = coordinate;
  if (!Number.isFinite(lon) || !Number.isFinite(lat)) {
    return false;
  }

  return Math.abs(lon) <= 180 && Math.abs(lat) <= 90;
}
