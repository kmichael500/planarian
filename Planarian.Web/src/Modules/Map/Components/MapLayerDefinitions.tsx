import type { DataDrivenPropertyValueSpecification } from "maplibre-gl";
import { PublicAccessLegend } from "./PublicAccessLegend";
import { PUBLIC_ACCESS_INFO } from "./PublicAccesDetails";

export const MAPTERHORN_TILEJSON_URL = "https://tiles.mapterhorn.com/tilejson.json";

export interface MapLayerSourceDefinition {
  tiles?: string[];
  url?: string;
  tileSize?: number;
  minzoom?: number;
  maxzoom?: number;
  encoding?: "terrarium" | "mapbox";
}

interface BaseMapLayerDefinition {
  id: string;
  displayName: string;
  defaultVisible: boolean;
  defaultOpacity: number;
  attribution?: string;
  groupId?: string;
  minzoom?: number;
  maxzoom?: number;
}

export interface RasterMapLayerDefinition extends BaseMapLayerDefinition {
  type: "raster";
  source: MapLayerSourceDefinition;
}

export interface HillshadeMapLayerDefinition extends BaseMapLayerDefinition {
  type: "hillshade";
  source: MapLayerSourceDefinition;
}

export interface VectorMapLayerDefinition extends BaseMapLayerDefinition {
  type: "vector";
  source: MapLayerSourceDefinition;
  fillLayer: {
    id: string;
    sourceLayer: string;
    layout: Record<string, any>;
    paint: Record<string, any>;
  };
  labelLayer?: {
    id: string;
    minzoom?: number;
    maxzoom?: number;
    source: MapLayerSourceDefinition & { layerName: string; tiles: string[] };
    layout: Record<string, any>;
    paint: (opacity: number) => Record<string, any>;
  };
  legend?: React.ReactNode;
}

export interface MapLayerGroupDefinition extends BaseMapLayerDefinition {
  type: "group";
  memberLayerIds: string[];
}

export type MapLayerDefinition =
  | RasterMapLayerDefinition
  | HillshadeMapLayerDefinition
  | VectorMapLayerDefinition
  | MapLayerGroupDefinition;

const NGMDB_LAYER_SPECS = [
  { scale: "500K", maximumTileZoom: 12 },
  { scale: "250K", maximumTileZoom: 12 },
  { scale: "125K", maximumTileZoom: 14 },
  { scale: "100K", maximumTileZoom: 14 },
  { scale: "63K", maximumTileZoom: 14 },
  { scale: "48K", maximumTileZoom: 14 },
  { scale: "24K", maximumTileZoom: 15 },
] as const;

const ngmdbLayerId = (scale: string) => `usgs-${scale.toLowerCase()}-geology`;

const createNgmdbLayer = ({ scale, maximumTileZoom }: (typeof NGMDB_LAYER_SPECS)[number]): RasterMapLayerDefinition => ({
  id: ngmdbLayerId(scale),
  displayName: scale,
  type: "raster",
  source: {
    tiles: [`/api/map/ngmdb/${scale}/{z}/{x}/{y}`],
    tileSize: 256,
    minzoom: 4,
    maxzoom: maximumTileZoom,
  },
  defaultVisible: false,
  defaultOpacity: 1,
  groupId: "ngmdb-geology-group",
  attribution: "NGMDB Map Viewer",
});

const publicAccessColorExpression: DataDrivenPropertyValueSpecification<string> = [
  "match",
  ["get", "Pub_Access"],
  ...Object.entries(PUBLIC_ACCESS_INFO).flatMap(([code, info]) => [code, info.color]),
  "#cccccc",
] as unknown as DataDrivenPropertyValueSpecification<string>;

export const MAP_LAYER_DEFINITIONS: MapLayerDefinition[] = [
  {
    id: "osm-street",
    displayName: "Street",
    type: "raster",
    source: { tiles: ["https://tile.openstreetmap.org/{z}/{x}/{y}.png"], tileSize: 256 },
    defaultVisible: true,
    defaultOpacity: 1,
    attribution: "© OpenStreetMap contributors",
  },
  {
    id: "open-topo",
    displayName: "Topo",
    type: "raster",
    source: { tiles: ["https://tile.opentopomap.org/{z}/{x}/{y}.png"], tileSize: 256 },
    defaultVisible: false,
    defaultOpacity: 1,
    attribution: "© OpenStreetMap contributors",
  },
  {
    id: "esri-satellite",
    displayName: "Satellite",
    type: "raster",
    source: {
      tiles: ["https://services.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}"],
      tileSize: 256,
    },
    defaultVisible: false,
    defaultOpacity: 1,
    attribution: "Esri",
  },
  {
    id: "mapterhorn-hillshade",
    displayName: "Hillshade",
    type: "hillshade",
    source: { url: MAPTERHORN_TILEJSON_URL, tileSize: 512, encoding: "terrarium" },
    defaultVisible: false,
    defaultOpacity: 1,
    attribution: '<a href="https://mapterhorn.com/attribution">© Mapterhorn</a>',
  },
  {
    id: "arcgis-public-access",
    displayName: "Public Land",
    type: "vector",
    source: {
      tiles: ["https://tiles.arcgis.com/tiles/v01gqwM5QqNysAAi/arcgis/rest/services/PADUS4_0VectorAnalysis_National_WebMerc_PA/VectorTileServer/tile/{z}/{y}/{x}.pbf"],
      tileSize: 512,
    },
    defaultVisible: false,
    defaultOpacity: 0.8,
    attribution: "PADUS 4.0",
    fillLayer: {
      id: "arcgis-public-access-fill",
      sourceLayer: "PADUS",
      layout: {},
      paint: { "fill-color": publicAccessColorExpression, "fill-outline-color": "#000000" },
    },
    labelLayer: {
      id: "arcgis-public-access-text",
      minzoom: 8,
      maxzoom: 15,
      source: {
        layerName: "PADUS",
        tiles: ["https://tiles.arcgis.com/tiles/v01gqwM5QqNysAAi/arcgis/rest/services/PADUS_Management_Areas_Manager_Type/VectorTileServer/tile/{z}/{y}/{x}.pbf"],
        tileSize: 512,
      },
      layout: {
        "text-field": ["get", "MngTp_Desc"],
        "text-size": ["interpolate", ["linear"], ["zoom"], 8, 0, 10, 8, 13, 12, 15, 16],
        "text-anchor": "center",
        "text-allow-overlap": false,
        "symbol-placement": "point",
        "text-optional": true,
      },
      paint: (opacity) => ({
        "text-color": "#000000",
        "text-opacity": ["interpolate", ["linear"], ["zoom"], 8, 0.5 * opacity, 13, 0.7 * opacity, 15, 0.9 * opacity],
      }),
    },
    legend: <PublicAccessLegend />,
  },
  {
    id: "regrid-parcel-boundaries",
    displayName: "Parcel Boundaries",
    type: "raster",
    minzoom: 15,
    maxzoom: 17,
    source: {
      tiles: ["https://tiles.arcgis.com/tiles/KzeiCaQsMoeCfoCq/arcgis/rest/services/Regrid_Nationwide_Parcel_Boundaries_v1/MapServer/tile/{z}/{y}/{x}"],
      tileSize: 256,
    },
    defaultVisible: false,
    defaultOpacity: 1,
    attribution: "Regrid Nationwide Parcel Boundaries v1",
  },
  {
    id: "macrostrat",
    displayName: "Macrostrat Geology",
    type: "raster",
    source: { tiles: ["https://tiles.macrostrat.org/carto/{z}/{x}/{y}.png"], tileSize: 256 },
    defaultVisible: false,
    defaultOpacity: 1,
    attribution: "Macrostrat",
  },
  {
    id: "ngmdb-geology-group",
    displayName: "NGMDB Geology",
    type: "group",
    memberLayerIds: NGMDB_LAYER_SPECS.map(({ scale }) => ngmdbLayerId(scale)),
    defaultVisible: false,
    defaultOpacity: 1,
    attribution: "NGMDB Map Viewer",
  },
  ...NGMDB_LAYER_SPECS.map(createNgmdbLayer),
  {
    id: "usgs-hydro",
    displayName: "Hydrology",
    type: "raster",
    source: {
      tiles: ["https://hydro.nationalmap.gov/arcgis/services/nhd/MapServer/WMSServer?bbox={bbox-epsg-3857}&format=image/png&service=WMS&version=1.1.1&request=GetMap&srs=EPSG:3857&transparent=true&width=256&height=256&layers=0,1,2,3,4,5,6,7,8,9,10,11,12&styles="],
      tileSize: 256,
    },
    defaultVisible: false,
    defaultOpacity: 1,
    attribution: "USGS",
  },
  {
    id: "usgs-drainage-basins-16digit",
    displayName: "Watershed Boundary",
    type: "raster",
    source: {
      tiles: ["https://hydro.nationalmap.gov/arcgis/services/wbd/MapServer/WMSServer?bbox={bbox-epsg-3857}&format=image/png&service=WMS&version=1.1.1&request=GetMap&srs=EPSG:3857&transparent=true&width=256&height=256&layers=8&styles="],
      tileSize: 256,
    },
    defaultVisible: false,
    defaultOpacity: 1,
    attribution: "USGS Watershed Boundary Dataset",
  },
];

export const getLayerDefinition = (id: string) => MAP_LAYER_DEFINITIONS.find((layer) => layer.id === id);
export const selectableMapLayers = MAP_LAYER_DEFINITIONS.filter((layer) => !layer.groupId);
