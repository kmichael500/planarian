import React, { useContext, useEffect, useState } from "react";
import { MapService } from "../Services/MapService";
import {
  Map,
  Source,
  Layer,
  GeolocateControl,
  NavigationControl,
  MapLayerMouseEvent,
} from "react-map-gl/maplibre";
import { StyleSpecification } from "@maplibre/maplibre-gl-style-spec";
import { Modal, Spin } from "antd";
import { CaveVm } from "../../Caves/Models/CaveVm";
import { CaveService } from "../../Caves/Service/CaveService";
import { CaveComponent } from "../../Caves/Components/CaveComponent";
import { AppOptions } from "../../../Shared/Services/AppService";
import { AuthenticationService } from "../../Authentication/Services/AuthenticationService";
import { AppContext } from "../../../Configuration/Context/AppContext";

// import "maplibre-gl/dist/maplibre-gl.css";

interface MapComponentProps {
  initialCenter?: [number, number];
  initialZoom?: number;
}

const MapComponent: React.FC<MapComponentProps> = ({
  initialCenter,
  initialZoom,
}) => {
  const { setHideBodyPadding, hideBodyPadding } = useContext(AppContext);
  useEffect(() => {
    setHideBodyPadding(true);
    return () => {
      setHideBodyPadding(false);
    };
  }, []);
  const [isLoading, setIsLoading] = useState(true);
  const [mapCenter, setMapCenter] = useState<[number, number]>(
    initialCenter || [0, 0]
  );
  const [zoom, setZoom] = useState(initialZoom || 7);

  const mapStyle = {
    glyphs:
      "https://api.mapbox.com/fonts/v1/mapbox/{fontstack}/{range}.pbf?access_token=pk.eyJ1IjoibWljaGFlbGtldHpuZXIiLCJhIjoiY2xvODF0M2ZiMDloNTJpbzYzdXRrYWhrcSJ9.B8x4P8SK9Zpe-sdN6pJ3Eg",
    version: 8,
    sources: {
      "osm-tiles": {
        type: "raster",
        tiles: [
          "https://a.tile.openstreetmap.org/{z}/{x}/{y}.png",
          "https://b.tile.openstreetmap.org/{z}/{x}/{y}.png",
          "https://c.tile.openstreetmap.org/{z}/{x}/{y}.png",
        ],
        tileSize: 256,
      },
    },
    layers: [
      {
        id: "osm-tiles-layer",
        type: "raster",
        source: "osm-tiles",
        minzoom: 0,
        maxzoom: 19,
      },
    ],
  } as StyleSpecification;

  useEffect(() => {
    if (!initialCenter) {
      const fetchData = async () => {
        try {
          const data = await MapService.getMapCenter();
          setMapCenter([data.latitude, data.longitude]);
          setIsLoading(false);
        } catch (error) {
          console.error("An error occurred while fetching data", error);
        } finally {
        }
      };
      fetchData();
    } else {
      setIsLoading(false);
    }
  }, [initialCenter]);

  const [isModalVisible, setIsModalVisible] = useState(false); // State to control Modal visibility

  const showModal = () => {
    setIsModalVisible(true);
  };

  const handleOk = () => {
    setIsModalVisible(false);
  };

  const handleCancel = () => {
    setIsModalVisible(false);
  };

  const [isModalLoading, setIsModalLoading] = useState(false);
  const [cave, setCave] = useState<CaveVm>();

  const handleMapClick = async (event: MapLayerMouseEvent) => {
    const clickedFeatures = event.features;
    const clickedEntrance = clickedFeatures?.find(
      (feature: any) => feature.layer.id === "entrances"
    );
    if (clickedEntrance) {
      setIsModalLoading(true);
      showModal();

      const result = await CaveService.GetCave(
        clickedEntrance.properties?.CaveId
      );
      setCave(result);
      setIsModalLoading(false);
    }
  };

  return (
    <>
      <Spin spinning={isModalLoading || isLoading}>
        {!isLoading && AppOptions.serverBaseUrl && hideBodyPadding && (
          <>
            <Map
              interactiveLayerIds={["entrances"]}
              initialViewState={{
                longitude: mapCenter[1],
                latitude: mapCenter[0],
                zoom: zoom,
              }}
              onClick={handleMapClick}
              mapStyle={mapStyle}
            >
              <NavigationControl />
              <GeolocateControl />
              <Source
                id="entrances"
                type="vector"
                tiles={[
                  `${
                    AppOptions.serverBaseUrl
                  }/api/map/{z}/{x}/{y}.mvt?access_token=${AuthenticationService.GetToken()}&account_id=${AuthenticationService.GetAccountId()}&test=1`,
                ]}
              >
                <Layer
                  source-layer="entrances"
                  id="entrances"
                  type="circle"
                  paint={{
                    "circle-radius": [
                      "interpolate",
                      ["linear"],
                      ["zoom"],
                      5,
                      2, // At zoom level 5, radius is 2
                      10,
                      4, // At zoom level 10, radius is 4
                      15,
                      8, // At zoom level 15, radius is 8
                    ],
                    "circle-opacity": [
                      "step",
                      ["zoom"],
                      0, // invisible at zoom levels < 13
                      9,
                      0.6, // visible at zoom levels >= 13
                    ],

                    // "circle-color": "#161616",
                  }}
                ></Layer>

                <Layer
                  source-layer="entrances"
                  id="entrances-heatmap"
                  type="heatmap"
                  paint={{
                    "heatmap-radius": [
                      "interpolate",
                      ["linear"],
                      ["zoom"],
                      0,
                      0.000001, // At zoom level 0, radius is 1
                      22,
                      30, // At zoom level 22, radius is 30
                    ],
                    "heatmap-opacity": [
                      "step",
                      ["zoom"],
                      0.6,
                      9,
                      0, // invisible at zoom levels > 9
                    ],
                  }}
                />

                <Layer
                  source-layer="entrances"
                  id="entrance-labels"
                  type="symbol"
                  layout={{
                    "text-font": ["Open Sans Regular"],
                    "text-field": [
                      "concat",
                      ["get", "cavename"], // Always display 'cavename'
                      [
                        "case",
                        ["all", ["has", "Name"], ["!=", ["get", "Name"], ""]],
                        ["concat", " (", ["get", "Name"], ")"], // Only add ' (Name)' if 'Name' property exists and is not an empty string
                        "",
                      ],
                      [
                        "case",
                        ["get", "IsPrimary"],
                        "*", // Add a star symbol if 'IsPrimary' is true
                        "", // Do nothing if 'IsPrimary' is false
                      ],
                    ],

                    "text-size": [
                      "interpolate",
                      ["linear"],
                      ["zoom"],
                      5,
                      0, // At zoom level 5, text size is 0 (effectively hiding the text)
                      10,
                      8, // At zoom level 10, text size is 8
                      15,
                      12, // At zoom level 15, text size is 12
                    ],
                    "text-offset": [0, 1.5], // This offset will position the text above the point
                    "text-anchor": "top", // Anchor the text to the top of the point
                    "text-allow-overlap": false, // This will prevent text from overlapping, reducing clutter
                  }}
                  paint={{
                    "text-color": "#000", // Set text color to black
                    "text-opacity": [
                      "step", // Change interpolation type to 'step'
                      ["zoom"],
                      0, // Default opacity is 0
                      9,
                      0.8, // At zoom level 9, text opacity is 0.8
                      15,
                      1, // At zoom level 15, text opacity is 1
                    ],
                  }}
                />
              </Source>
            </Map>
            <Modal
              title={cave?.name || "Cave"}
              visible={isModalVisible}
              onOk={handleOk}
              onCancel={handleCancel}
              width="80vw"
              bodyStyle={{
                height: "65vh",
                overflow: "scroll",
                padding: "0px",
              }}
            >
              <Spin spinning={isModalLoading}>
                <CaveComponent
                  options={{ showMap: false }}
                  cave={cave}
                  isLoading={isModalLoading}
                />
              </Spin>
            </Modal>
          </>
        )}
      </Spin>
    </>
  );
};

export { MapComponent };
