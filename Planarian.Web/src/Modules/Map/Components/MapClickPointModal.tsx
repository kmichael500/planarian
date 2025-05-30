import { FC, useEffect, useState } from "react";
import { Descriptions, Grid, InputNumber, DatePicker } from "antd";

import dayjs, { Dayjs } from "dayjs";
import { CopyOutlined } from "@ant-design/icons";
import { Macrostrat } from "./Macrostrat";
import { PlanarianDividerComponent } from "../../../Shared/Components/PlanarianDivider/PlanarianDividerComponent";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
import {
  defaultIfEmpty,
  DistanceFormat,
  formatCoordinates,
  formatDistance,
} from "../../../Shared/Helpers/StringHelpers";
import { GageList } from "./GaugeList";
import { PlanarianModal } from "../../../Shared/Components/Buttons/PlanarianModal";
import { PublicAccessDetails } from "./PublicAccesDetails";
import { PlanarianDateRange } from "../../../Shared/Components/Buttons/PlanarianDateRange";

const { RangePicker } = DatePicker;

function formatDateTime(dateStr: string): string {
  if (!dateStr) return "";
  const date = new Date(dateStr);
  return date.toLocaleString();
}

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

  const [pendingDistanceMiles, setPendingDistanceMiles] = useState(20);
  const [distanceMiles, setDistanceMiles] = useState(25);

  const [dateRange, setDateRange] = useState<[Dayjs | null, Dayjs | null]>([
    dayjs().subtract(1, "month"),
    dayjs(),
  ]);

  const screens = Grid.useBreakpoint();
  const descriptionLayout = screens.sm ? "horizontal" : "vertical";

  const copyCoordinates = () => {
    navigator.clipboard.writeText(`${lat}, ${lng}`);
  };

  // Debounce the distance input
  useEffect(() => {
    const timer = setTimeout(() => {
      setDistanceMiles(pendingDistanceMiles);
    }, 500);
    return () => clearTimeout(timer);
  }, [pendingDistanceMiles]);

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

  return (
    <PlanarianModal
      open={isModalVisible}
      onClose={handleCancel}
      footer={[]}
      header={`${address?.county || ""}, ${address?.state || ""} `}
    >
      <Descriptions
        layout={descriptionLayout}
        bordered
        title="Location Information"
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
            ? formatDistance(elevation, DistanceFormat.feet)
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
        <Descriptions.Item label="Land Access" span={3}>
          <PublicAccessDetails lat={lat} lng={lng} />
        </Descriptions.Item>
      </Descriptions>

      <PlanarianDividerComponent
        title="Geology"
        secondaryTitle="from Macrostrat and NGMDB Map Viewer"
      />
      <Macrostrat lat={lat} lng={lng} />

      <PlanarianDividerComponent
        title="Water Gages"
        secondaryTitle="from USGS NWIS"
        element={
          <div style={{ display: "flex", gap: 8 }}>
            <InputNumber
              addonAfter="Miles"
              min={1}
              max={50}
              value={pendingDistanceMiles}
              onChange={(val) => {
                if (typeof val === "number") {
                  setPendingDistanceMiles(val);
                }
              }}
            />

            <PlanarianDateRange
              value={dateRange}
              onChange={(range, dateStrings) =>
                setDateRange(range || [null, null])
              }
            />
          </div>
        }
      />

      <GageList
        lat={lat}
        lng={lng}
        distanceMiles={distanceMiles}
        dateRange={dateRange}
      />
    </PlanarianModal>
  );
};
