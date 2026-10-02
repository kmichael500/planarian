import { FC, useEffect, useState } from "react";
import { Macrostrat } from "./Macrostrat";
import { StreamGages } from "./StreamGages";
import { PlanarianDividerComponent } from "../../../Shared/Components/PlanarianDivider/PlanarianDividerComponent";
import {
  PlanarianDescription,
  type PlanarianDescriptionItem,
} from "../../../Shared/Components/Buttons/PlanarianDescription";
import {
  defaultIfEmpty,
  DistanceFormat,
  formatCoordinates,
  formatDistance,
} from "../../../Shared/Helpers/StringHelpers";
import { PlanarianModal } from "../../../Shared/Components/Buttons/PlanarianModal";
import { PublicAccessDetails } from "./PublicAccesDetails";

/** MapClickPointModal Props */
interface MapClickPointModalProps {
  isModalVisible: boolean;
  lat: number;
  lng: number;
  handleCancel: () => void;
}

export const MapClickPointModal: FC<MapClickPointModalProps> = ({
  isModalVisible,
  lat,
  lng,
  handleCancel,
}) => {
  // Elevation
  const [elevation, setElevation] = useState<number | null>(null);
  const [loadingElevation, setLoadingElevation] = useState(false);
  const [errorElevation, setErrorElevation] = useState<string | null>(null);

  const [address, setAddress] = useState<any>(null);
  const [loadingAddress, setLoadingAddress] = useState(false);
  const [errorAddress, setErrorAddress] = useState<string | null>(null);

  // --- Fetch Elevation ---
  useEffect(() => {
    if (!lat || !lng) return;
    const fetchElevation = async () => {
      setLoadingElevation(true);
      setErrorElevation(null);
      try {
        const resp = await fetch(
          `https://epqs.nationalmap.gov/v1/json?x=${lng}&y=${lat}&units=Feet&wkid=4326&includeDate=False`
        );
        const data = await resp.json();
        if (data && data.value) {
          setElevation(data.value);
        } else {
          setErrorElevation("No elevation data found.");
        }
      } catch (err) {
        setErrorElevation("Error fetching elevation data.");
      }
      setLoadingElevation(false);
    };
    fetchElevation();
  }, [lat, lng]);

  // --- Fetch Address ---
  useEffect(() => {
    if (!lat || !lng) return;
    const fetchAddress = async () => {
      setLoadingAddress(true);
      setErrorAddress(null);
      try {
        const resp = await fetch(
          `https://nominatim.openstreetmap.org/reverse?lat=${lat}&lon=${lng}&format=json`
        );
        const data = await resp.json();
        if (data && data.address) {
          setAddress(data.address);
        } else {
          setErrorAddress("No address data found.");
        }
      } catch (err) {
        setErrorAddress("Error fetching address data.");
      }
      setLoadingAddress(false);
    };
    fetchAddress();
  }, [lat, lng]);

  const locationItems: PlanarianDescriptionItem[] = [
    {
      key: "coordinates",
      copyText: `${lat}, ${lng}`,
      label: "Coordinates",
      children: formatCoordinates(lat, lng),
    },
    {
      key: "elevation",
      label: "Elevation",
      children: loadingElevation
        ? "Loading..."
        : errorElevation
        ? errorElevation
        : elevation
        ? formatDistance(elevation, DistanceFormat.feet)
        : defaultIfEmpty(""),
    },
    {
      key: "address",
      label: "Address",
      children: loadingAddress ? (
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
      ),
    },
    {
      key: "land-access",
      label: "Land Access",
      span: "filled",
      children: <PublicAccessDetails lat={lat} lng={lng} />,
    },
  ];

  return (
    <PlanarianModal
      open={isModalVisible}
      onClose={handleCancel}
      footer={[]}
      header={`${address?.county || ""}, ${address?.state || ""} `}
    >
      <PlanarianDescription
        title="Location Information"
        items={locationItems}
      />

      <PlanarianDividerComponent
        title="Geology"
        secondaryTitle="from Macrostrat and NGMDB Map Viewer"
      />
      <Macrostrat lat={lat} lng={lng} />

      <StreamGages lat={lat} lng={lng} />
    </PlanarianModal>
  );
};
