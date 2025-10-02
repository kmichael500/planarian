import { Result, Spin } from "antd";
import { useEffect, useMemo, useState } from "react";
import type { ReactNode } from "react";
import type { Feature, FeatureCollection } from "geojson";
import bbox from "@turf/bbox";
import shp from "shpjs";
import { kml as convertKmlToGeoJson } from "@tmcw/togeojson";
import { MapBaseComponent } from "../../Map/Components/MapBaseComponent";
import { Source, Layer, useMap } from "react-map-gl/maplibre";
import type { LngLatBoundsLike } from "maplibre-gl";

const VECTOR_SOURCE_ID = "vector-dataset-viewer-source";
const VECTOR_FILL_LAYER_ID = "vector-dataset-viewer-fill";
const VECTOR_LINE_LAYER_ID = "vector-dataset-viewer-line";
const VECTOR_POINT_LAYER_ID = "vector-dataset-viewer-point";

type BoundsTuple = [[number, number], [number, number]];

interface VectorDatasetViewerProps {
  embedUrl: string;
  fileType: string | null | undefined;
  downloadButton?: ReactNode;
}

export const VectorDatasetViewer: React.FC<VectorDatasetViewerProps> = ({
  embedUrl,
  fileType,
  downloadButton,
}) => {
  const [collection, setCollection] = useState<FeatureCollection | null>(null);
  const [bounds, setBounds] = useState<BoundsTuple | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(false);

  useEffect(() => {
    let isCancelled = false;
    const fetchData = async () => {
      setIsLoading(true);
      setError(null);
      setCollection(null);
      setBounds(null);

      try {
        const normalizedType = (fileType || "").toLowerCase();
        let featureCollection: FeatureCollection | null = null;

        if (normalizedType === "zip") {
          const response = await fetch(embedUrl);
          if (!response.ok) {
            throw new Error("Unable to download shapefile archive.");
          }
          const arrayBuffer = await response.arrayBuffer();
          const parsed = await shp(arrayBuffer);
          featureCollection = normalizeToFeatureCollection(parsed);
        } else if (normalizedType === "kml") {
          const response = await fetch(embedUrl);
          if (!response.ok) {
            throw new Error("Unable to download KML file.");
          }
          const text = await response.text();
          const domParser = new DOMParser();
          const xmlDocument = domParser.parseFromString(text, "application/xml");
          if (xmlDocument.querySelector("parsererror")) {
            throw new Error("Invalid KML file.");
          }
          const geoJson = convertKmlToGeoJson(xmlDocument);
          featureCollection = normalizeToFeatureCollection(geoJson);
        } else if (normalizedType === "geojson" || normalizedType === "json") {
          const response = await fetch(embedUrl);
          if (!response.ok) {
            throw new Error("Unable to download GeoJSON file.");
          }
          const text = await response.text();
          const parsed = JSON.parse(text);
          featureCollection = normalizeToFeatureCollection(parsed);
        } else {
          throw new Error(`Unsupported geospatial file type: ${fileType}`);
        }

        if (!featureCollection || featureCollection.features.length === 0) {
          throw new Error("No spatial features found in this file.");
        }

        const computedBounds = bbox(featureCollection) as [number, number, number, number];
        if (computedBounds.some((value) => !Number.isFinite(value))) {
          throw new Error("Unable to determine spatial extent for this file.");
        }

        const [[minLng, minLat], [maxLng, maxLat]]: BoundsTuple = [
          [computedBounds[0], computedBounds[1]],
          [computedBounds[2], computedBounds[3]],
        ];

        if (!isCancelled) {
          setCollection(featureCollection);
          setBounds([[minLng, minLat], [maxLng, maxLat]]);
          setIsLoading(false);
        }
      } catch (e) {
        if (!isCancelled) {
          const message =
            e instanceof Error ? e.message : "Failed to load geospatial data.";
          setError(message);
          setIsLoading(false);
        }
      }
    };

    fetchData();
    return () => {
      isCancelled = true;
    };
  }, [embedUrl, fileType]);

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

  if (isLoading || !collection || !bounds || !center) {
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
        initialZoom={10}
        onCaveClicked={() => { }}
        onNonCaveClicked={() => { }}
        manageBodyPadding={false}
        showFullScreenControl={false}
        onMoveEnd={() => { }}
        additionalInteractiveLayerIds={[
          VECTOR_FILL_LAYER_ID,
          VECTOR_LINE_LAYER_ID,
          VECTOR_POINT_LAYER_ID,
        ]}
      >
        <VectorOverlay data={collection} bounds={bounds} />
      </MapBaseComponent>
    </div>
  );
};

interface VectorOverlayProps {
  data: FeatureCollection;
  bounds: BoundsTuple;
}

const VectorOverlay: React.FC<VectorOverlayProps> = ({ data, bounds }) => {
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
          maxZoom: 15,
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

  useEffect(() => {
    if (!map) {
      return;
    }

    const mapInstance = map.getMap();
    const layerIds = [
      VECTOR_FILL_LAYER_ID,
      VECTOR_LINE_LAYER_ID,
      VECTOR_POINT_LAYER_ID,
    ];

    const handleMouseEnter = () => {
      mapInstance.getCanvas().style.cursor = "pointer";
    };

    const handleMouseLeave = () => {
      mapInstance.getCanvas().style.cursor = "";
    };

    layerIds.forEach((layerId) => {
      mapInstance.on("mouseenter", layerId, handleMouseEnter);
      mapInstance.on("mouseleave", layerId, handleMouseLeave);
    });

    return () => {
      layerIds.forEach((layerId) => {
        mapInstance.off("mouseenter", layerId, handleMouseEnter);
        mapInstance.off("mouseleave", layerId, handleMouseLeave);
      });
      mapInstance.getCanvas().style.cursor = "";
    };
  }, [map]);

  return (
    <>
      <Source id={VECTOR_SOURCE_ID} type="geojson" data={data}>
        <Layer
          id={VECTOR_FILL_LAYER_ID}
          type="fill"
          paint={{
            "fill-color": "#FF0000",
            "fill-opacity": 0.8,
            "fill-outline-color": "#B22222",
          }}
          filter={[
            "any",
            ["==", ["geometry-type"], "Polygon"],
            ["==", ["geometry-type"], "MultiPolygon"],
          ]}
        />
        <Layer
          id={VECTOR_LINE_LAYER_ID}
          type="line"
          layout={{ "line-join": "round", "line-cap": "round" }}
          paint={{
            "line-color": "#00008B",
            "line-width": 2,
            "line-opacity": 0.9,
          }}
          filter={[
            "any",
            ["==", ["geometry-type"], "LineString"],
            ["==", ["geometry-type"], "MultiLineString"],
          ]}
        />
        <Layer
          id={VECTOR_POINT_LAYER_ID}
          type="circle"
          paint={{
            "circle-radius": [
              "interpolate",
              ["linear"],
              ["zoom"],
              0,
              1,
              12,
              1.5,
              16,
              2.5,
            ],
            "circle-color": "#ff5722",
            "circle-stroke-color": "#ffffff",
            "circle-stroke-width": 0.5,
            "circle-opacity": 0.85,
          }}
          filter={[
            "any",
            ["==", ["geometry-type"], "Point"],
            ["==", ["geometry-type"], "MultiPoint"],
          ]}
        />
      </Source>

    </>
  );
};

function normalizeToFeatureCollection(input: unknown): FeatureCollection | null {
  if (!input) {
    return null;
  }

  if (isFeatureCollection(input)) {
    return {
      type: "FeatureCollection",
      features: input.features.filter((feature) => feature.geometry),
    };
  }

  if (isFeature(input)) {
    return input.geometry
      ? { type: "FeatureCollection", features: [input] }
      : null;
  }

  if (Array.isArray(input)) {
    const features: Feature[] = [];
    input.forEach((item) => {
      const normalized = normalizeToFeatureCollection(item);
      if (normalized) {
        features.push(...normalized.features);
      }
    });
    return features.length > 0
      ? { type: "FeatureCollection", features }
      : null;
  }

  if (typeof input === "object") {
    const features: Feature[] = [];
    Object.values(input as Record<string, unknown>).forEach((value) => {
      const normalized = normalizeToFeatureCollection(value);
      if (normalized) {
        features.push(...normalized.features);
      }
    });
    return features.length > 0
      ? { type: "FeatureCollection", features }
      : null;
  }

  return null;
}

function isFeatureCollection(input: unknown): input is FeatureCollection {
  return (
    typeof input === "object" &&
    input !== null &&
    (input as FeatureCollection).type === "FeatureCollection" &&
    Array.isArray((input as FeatureCollection).features)
  );
}

function isFeature(input: unknown): input is Feature {
  return (
    typeof input === "object" &&
    input !== null &&
    (input as Feature).type === "Feature" &&
    "geometry" in (input as Feature)
  );
}
