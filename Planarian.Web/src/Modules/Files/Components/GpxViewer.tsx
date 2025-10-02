import { Result, Spin } from "antd";
import { useEffect, useMemo, useState } from "react";
import type { ReactNode } from "react";
import type { FeatureCollection, Feature, Position } from "geojson";
import bbox from "@turf/bbox";
import { Source, Layer, useMap } from "react-map-gl/maplibre";
import type { LngLatBoundsLike } from "maplibre-gl";
import { gpx as convertGpxToGeoJson } from "@tmcw/togeojson";
import { MapBaseComponent } from "../../Map/Components/MapBaseComponent";

const GPX_LAYER_ID = "gpx-viewer-layer";
const GPX_SOURCE_ID = "gpx-viewer-source";

type BoundsTuple = [[number, number], [number, number]];

const getElements = (root: Document | Element, tagName: string): Element[] => {
  const set = new Set<Element>();
  Array.from(root.getElementsByTagName(tagName)).forEach((el) => set.add(el));
  if ("getElementsByTagNameNS" in root) {
    const withNs = root.getElementsByTagNameNS?.("*", tagName);
    if (withNs) {
      Array.from(withNs).forEach((el) => set.add(el as Element));
    }
  }
  return Array.from(set);
};

const parseCoordinate = (element: Element): Position | null => {
  const latAttr = element.getAttribute("lat") ?? element.getAttribute("latitude");
  const lonAttr = element.getAttribute("lon") ?? element.getAttribute("longitude");

  if (!latAttr || !lonAttr) {
    return null;
  }

  const lat = Number(latAttr);
  const lon = Number(lonAttr);

  if (!Number.isFinite(lat) || !Number.isFinite(lon)) {
    return null;
  }

  const elevationElement = getElements(element, "ele")[0];
  const elevation = elevationElement ? Number(elevationElement.textContent) : NaN;

  if (Number.isFinite(elevation)) {
    return [lon, lat, elevation];
  }

  return [lon, lat];
};

const fallbackParse = (xmlDocument: Document): FeatureCollection | null => {
  const features: Feature[] = [];

  getElements(xmlDocument, "trk").forEach((track) => {
    const segmentCoordinates: Position[][] = [];

    getElements(track, "trkseg").forEach((segment) => {
      const coords: Position[] = [];
      getElements(segment, "trkpt").forEach((point) => {
        const coordinate = parseCoordinate(point);
        if (coordinate) {
          coords.push(coordinate);
        }
      });

      if (coords.length > 0) {
        segmentCoordinates.push(coords);
      }
    });

    if (segmentCoordinates.length === 1) {
      features.push({
        type: "Feature",
        geometry: { type: "LineString", coordinates: segmentCoordinates[0] },
        properties: { type: "track" },
      });
    } else if (segmentCoordinates.length > 1) {
      features.push({
        type: "Feature",
        geometry: { type: "MultiLineString", coordinates: segmentCoordinates },
        properties: { type: "track" },
      });
    }
  });

  getElements(xmlDocument, "rte").forEach((route) => {
    const coords: Position[] = [];
    getElements(route, "rtept").forEach((point) => {
      const coordinate = parseCoordinate(point);
      if (coordinate) {
        coords.push(coordinate);
      }
    });

    if (coords.length > 0) {
      features.push({
        type: "Feature",
        geometry: { type: "LineString", coordinates: coords },
        properties: { type: "route" },
      });
    }
  });

  getElements(xmlDocument, "wpt").forEach((waypoint) => {
    const coordinate = parseCoordinate(waypoint);
    if (!coordinate) {
      return;
    }

    features.push({
      type: "Feature",
      geometry: { type: "Point", coordinates: coordinate },
      properties: { type: "waypoint" },
    });
  });

  if (features.length === 0) {
    return null;
  }

  return { type: "FeatureCollection", features };
};

const normalizeGeoJson = (collection: FeatureCollection) => collection;

const parseGpxToGeoJson = (
  gpxContent: string
): FeatureCollection | null => {
  if (!gpxContent) {
    return null;
  }

  const domParser = new DOMParser();
  const xmlDocument = domParser.parseFromString(
    gpxContent,
    "application/xml"
  );

  if (xmlDocument.querySelector("parsererror")) {
    return null;
  }

  const geoJson = convertGpxToGeoJson(xmlDocument) as FeatureCollection;

  if (geoJson && geoJson.features.length > 0) {
    return normalizeGeoJson(geoJson);
  }

  const fallback = fallbackParse(xmlDocument);
  return fallback ? normalizeGeoJson(fallback) : null;
};

interface GpxViewerProps {
  embedUrl: string;
  downloadButton?: ReactNode;
}

export const GpxViewer: React.FC<GpxViewerProps> = ({
  embedUrl,
  downloadButton,
}) => {
  const [gpxGeoJson, setGpxGeoJson] = useState<FeatureCollection | null>(null);
  const [gpxBounds, setGpxBounds] = useState<BoundsTuple | null>(null);
  const [gpxError, setGpxError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(false);

  useEffect(() => {
    let isCancelled = false;
    setIsLoading(true);
    setGpxError(null);
    setGpxGeoJson(null);
    setGpxBounds(null);

    fetch(embedUrl)
      .then((response) => response.text())
      .then((data) => {
        if (isCancelled) {
          return;
        }

        const geoJson = parseGpxToGeoJson(data);

        if (!geoJson || geoJson.features.length === 0) {
          setGpxError("No spatial data found in this GPX file.");
          setIsLoading(false);
          return;
        }

        setGpxGeoJson(geoJson);
        try {
          const [minX, minY, maxX, maxY] = bbox(geoJson);
          if (
            [minX, minY, maxX, maxY].every((value) => Number.isFinite(value))
          ) {
            const spanX = Math.abs(maxX - minX);
            const spanY = Math.abs(maxY - minY);
            const padding = 0.01;

            const adjustedMinX = spanX < 1e-5 ? minX - padding : minX;
            const adjustedMaxX = spanX < 1e-5 ? maxX + padding : maxX;
            const adjustedMinY = spanY < 1e-5 ? minY - padding : minY;
            const adjustedMaxY = spanY < 1e-5 ? maxY + padding : maxY;

            setGpxBounds([
              [adjustedMinX, adjustedMinY],
              [adjustedMaxX, adjustedMaxY],
            ]);
          }
        } catch (error) {
          setGpxError("Unable to determine GPX bounds.");
        }

        setIsLoading(false);
      })
      .catch(() => {
        if (isCancelled) {
          return;
        }
        setIsLoading(false);
        setGpxError("Unable to load GPX file.");
      });

    return () => {
      isCancelled = true;
    };
  }, [embedUrl]);

  const center = useMemo<[number, number] | undefined>(() => {
    if (!gpxBounds) {
      return undefined;
    }
    const [[minLng, minLat], [maxLng, maxLat]] = gpxBounds;
    return [(minLat + maxLat) / 2, (minLng + maxLng) / 2];
  }, [gpxBounds]);

  if (gpxError && !isLoading) {
    return (
      <Result status="warning" title={gpxError} extra={downloadButton} />
    );
  }

  if (isLoading || !gpxGeoJson || !gpxBounds || !center) {
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
        initialCenter={center}
        initialZoom={12}
        onCaveClicked={undefined}
        onNonCaveClicked={undefined}
        manageBodyPadding={false}
        showFullScreenControl={false}
        onMoveEnd={undefined}
      >
        <GpxOverlay data={gpxGeoJson} bounds={gpxBounds} />
      </MapBaseComponent>
    </div>
  );
};

interface GpxOverlayProps {
  data: FeatureCollection;
  bounds: BoundsTuple;
}

const GpxOverlay: React.FC<GpxOverlayProps> = ({ data, bounds }) => {
  const { current: map } = useMap();

  useEffect(() => {
    if (!map) {
      return;
    }

    const mapInstance = map.getMap();
    if (!mapInstance || !bounds) {
      return;
    }

    const fitToBounds = () => {
      try {
        mapInstance.fitBounds(bounds as LngLatBoundsLike, {
          padding: 20,
          duration: 0,
          maxZoom: 16,
        });
      } catch (error) {
        // Ignore fit errors
      }
    };

    if (mapInstance.isStyleLoaded()) {
      fitToBounds();
      return;
    }

    mapInstance.once("load", fitToBounds);

    return () => {
      mapInstance.off("load", fitToBounds);
    };
  }, [map, bounds]);

  return (
    <Source id={GPX_SOURCE_ID} type="geojson" data={data}>
      <Layer
        id={GPX_LAYER_ID}
        type="line"
        layout={{ "line-join": "round", "line-cap": "round" }}
        paint={{
          "line-color": "#00008B",
          "line-width": 3,
          "line-opacity": 0.9,
        }}
        filter={[
          "any",
          ["==", ["geometry-type"], "LineString"],
          ["==", ["geometry-type"], "MultiLineString"],
        ]}
      />
    </Source>
  );
};
