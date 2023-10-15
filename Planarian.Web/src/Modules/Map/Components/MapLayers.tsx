import { LayersControl, TileLayer, WMSTileLayer } from "react-leaflet";

const LAYERS: Layer[] = [
  {
    name: "Open Topo",
    type: "TileLayer",
    url: "https://{s}.tile.opentopomap.org/{z}/{x}/{y}.png",
    attribution:
      '&copy; <a href="http://osm.org/copyright">OpenStreetMap</a> contributors',
    maxZoom: 15,
    checked: true,
  },
  {
    name: "USGS National Hydrography Dataset",
    type: "WMSTileLayer",
    url: "https://hydro.nationalmap.gov/arcgis/services/nhd/MapServer/WMSServer?",
    attribution:
      'Map data: <a href="https://www.usgs.gov/national-hydrography/national-hydrography-dataset" target="_blank">USGS National Hydrography Dataset</a>',
    layers: "0,1,2,3,4,5,6,7,8,9,10,11,12", // This will include all layers, you can specify which layers you want to include
    maxZoom: 20,
    opacity: 1,
    checked: false, // Set this to true if you want this layer to be displayed by default,
    subdomains: [],
  },
  {
    name: "Macrostrat Geology",
    type: "TileLayer",
    url: "https://tiles.macrostrat.org/carto/{z}/{x}/{y}.png",
    attribution:
      '<a href="https://macrostrat.org" target="_blank">Macrostrat</a>',
    maxZoom: 20,
    checked: false,
  },
  {
    name: "3DEP Elevation",
    type: "WMSTileLayer",
    url: "https://elevation.nationalmap.gov/arcgis/services/3DEPElevation/ImageServer/WMSServer?",
    attribution:
      '<a href="https://viewer.nationalmap.gov/advanced-viewer/" target="_blank">USGS The National Map: 3DEP Elevation Program</a>',
    layers: "3DEPElevation:Hillshade Gray",
    opacity: 1,
    maxZoom: 20,
    subdomains: [],
  },
  {
    name: "Satellite",
    type: "WMSTileLayer",
    url: "http://{s}.google.com/vt/lyrs=s,h&x={x}&y={y}&z={z}",
    maxZoom: 20,
    attribution: "Map data ©2020 Google",
    subdomains: ["mt0", "mt1", "mt2", "mt3"],
    opacity: 1,
  },

  {
    name: "Google Maps Terrain",
    type: "WMSTileLayer",
    url: "http://{s}.google.com/vt/lyrs=p&x={x}&y={y}&z={z}",
    maxZoom: 15,
    attribution: "Map data ©2020 Google",
    subdomains: ["mt0", "mt1", "mt2", "mt3"],
    opacity: 1,
  },
  {
    name: "Open Street Maps",
    type: "TileLayer",
    url: "https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png",
    attribution:
      '&copy; <a href="http://osm.org/copyright">OpenStreetMap</a> contributors',
    maxZoom: 19,
  },
];

interface BaseLayer {
  name: string;
  type: string; // the type of the layer, e.g., "TileLayer", "WMSTileLayer"
  attribution: string;
  url: string;
  maxZoom?: number;
  checked?: boolean;
}

interface TileLayer extends BaseLayer {
  type: "TileLayer";
}

interface WMSTileLayer extends BaseLayer {
  type: "WMSTileLayer";
  layers?: string;
  opacity?: number;
  subdomains?: string[];
}

type Layer = TileLayer | WMSTileLayer;

const DynamicLayer = ({ layer }: { layer: Layer }) => {
  switch (layer.type) {
    case "TileLayer":
      return <TileLayerComponent layer={layer} />;
    case "WMSTileLayer":
      return <WMSTileLayerComponent layer={layer} />;
    default:
      return null;
  }
};
const TileLayerComponent: React.FC<{ layer: TileLayer }> = ({ layer }) => (
  <TileLayer
    maxZoom={layer.maxZoom}
    attribution={layer.attribution}
    url={layer.url}
    keepBuffer={100}
  />
);

const WMSTileLayerComponent: React.FC<{ layer: WMSTileLayer }> = ({
  layer,
}) => (
  <WMSTileLayer
    maxZoom={layer.maxZoom}
    url={layer.url}
    attribution={layer.attribution}
    layers={layer.layers}
    opacity={layer.opacity}
    subdomains={layer.subdomains}
    keepBuffer={5}
  />
);

const MapLayers: React.FC = () => (
  <LayersControl position="topright">
    {LAYERS.map((layer, index) => (
      <LayersControl.BaseLayer
        name={layer.name}
        checked={layer.checked}
        key={index}
      >
        <DynamicLayer layer={layer} />
      </LayersControl.BaseLayer>
    ))}
  </LayersControl>
);

export { MapLayers };
