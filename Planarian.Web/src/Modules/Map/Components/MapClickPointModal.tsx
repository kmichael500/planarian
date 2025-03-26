import React, { FC, useEffect, useState } from "react";
import { Modal, Button, Descriptions, Grid } from "antd";
import { CopyOutlined } from "@ant-design/icons";
import { Macrostrat } from "./Macrostrat";
import { PlanarianDividerComponent } from "../../../Shared/Components/PlanarianDivider/PlanarianDividerComponent";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
import {
  defaultIfEmpty,
  formatCoordinates,
  formatDistance,
} from "../../../Shared/Helpers/StringHelpers";

interface MapClickPointModalProps {
  isModalVisible: boolean;
  lat: number;
  lng: number;
  handleCancel: () => void;
}

const MapClickPointModal: FC<MapClickPointModalProps> = ({
  isModalVisible,
  lat,
  lng,
  handleCancel,
}) => {
  const [elevation, setElevation] = useState<number | null>(null);
  const [loadingElevation, setLoadingElevation] = useState(false);
  const [errorElevation, setErrorElevation] = useState<string | null>(null);

  const [address, setAddress] = useState<any>(null);
  const [loadingAddress, setLoadingAddress] = useState(false);
  const [errorAddress, setErrorAddress] = useState<string | null>(null);

  const screens = Grid.useBreakpoint();
  const descriptionLayout = screens.sm ? "horizontal" : "vertical";

  const copyCoordinates = () => {
    const coordinates = `${lat}, ${lng}`;
    navigator.clipboard.writeText(coordinates);
  };

  // Fetch elevation data
  useEffect(() => {
    const fetchElevation = async () => {
      setLoadingElevation(true);
      setErrorElevation(null);
      try {
        const response = await fetch(
          `https://epqs.nationalmap.gov/v1/json?x=${lng}&y=${lat}&units=Feet&wkid=4326&includeDate=False`
        );
        const data = await response.json();
        if (data && data.value) {
          setElevation(data.value);
        } else {
          setErrorElevation("No elevation data found.");
        }
      } catch (error) {
        setErrorElevation("Error fetching elevation data.");
      }
      setLoadingElevation(false);
    };

    if (lat && lng) {
      fetchElevation();
    }
  }, [lat, lng]);

  // Fetch reverse geocoded address data
  useEffect(() => {
    const fetchAddress = async () => {
      setLoadingAddress(true);
      setErrorAddress(null);
      try {
        const response = await fetch(
          `https://nominatim.openstreetmap.org/reverse?lat=${lat}&lon=${lng}&format=json`
        );
        const data = await response.json();
        if (data && data.address) {
          setAddress(data.address);
        } else {
          setErrorAddress("No address data found.");
        }
      } catch (error) {
        setErrorAddress("Error fetching address data.");
      }
      setLoadingAddress(false);
    };

    if (lat && lng) {
      fetchAddress();
    }
  }, [lat, lng]);

  return (
    <Modal
      open={isModalVisible}
      onCancel={handleCancel}
      width="80vw"
      bodyStyle={{
        height: "65vh",
        overflow: "scroll",
        padding: "16px",
      }}
      footer={[
        <Button key="close" onClick={handleCancel}>
          Close
        </Button>,
      ]}
      title={`${address?.county || ""}, ${address?.state || ""} `}
    >
      <Descriptions
        layout={descriptionLayout}
        bordered
        title="Location Information"
        // column={3}
      >
        <Descriptions.Item
          label={
            <span>
              Coordinates{" "}
              <PlanarianButton
                type="link"
                icon={<CopyOutlined />}
                onClick={copyCoordinates}
              />
            </span>
          }
        >
          {formatCoordinates(lat, lng)}
        </Descriptions.Item>
        <Descriptions.Item label="Elevation">
          {loadingElevation
            ? "Loading..."
            : errorElevation
            ? errorElevation
            : elevation
            ? formatDistance(elevation)
            : defaultIfEmpty("")}
        </Descriptions.Item>
        <Descriptions.Item label="Address">
          {loadingAddress ? (
            "Loading..."
          ) : errorAddress ? (
            errorAddress
          ) : address ? (
            <>
              {address.road && <div>{address.road}</div>}
              {address.city && <div>{address.city}</div>}
              {address.county && <div>{address.county}</div>}
              {address.state && <div>{address.state}</div>}
              {address.country && <div>{address.country}</div>}
            </>
          ) : (
            defaultIfEmpty("")
          )}
        </Descriptions.Item>
      </Descriptions>
      <PlanarianDividerComponent
        title={"Geology"}
        secondaryTitle="from Macrostrat"
      />
      <Macrostrat lat={lat} lng={lng} />
    </Modal>
  );
};

export { MapClickPointModal };
