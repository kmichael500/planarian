import { FC, useEffect, useState } from "react";
import { Spin, Tag, Typography } from "antd";

const { Text } = Typography;

interface ProtectedAreaDetailsProps {
  lat: number;
  lng: number;
}

export const GAP_STATUS_INFO: Record<
  string,
  { label: string; color: string; description: string }
> = {
  "1": {
    label: "1",
    color: "#008000", // Dark Green
    description:
      "Fully protected; managed to maintain natural conditions without human disturbance.",
  },
  "2": {
    label: "2",
    color: "#32CD32", // Lime Green
    description:
      "Permanently protected; mostly natural conditions with some limited human disturbance allowed.",
  },
  "3": {
    label: "3",
    color: "#FFD700", // Gold
    description:
      "Protected from significant development; allows sustainable resource use and recreation.",
  },
  "4": {
    label: "4",
    color: "#FF4500", // Orange Red
    description:
      "Minimal or no formal protection; potential for land conversion and intensive use.",
  },
};

export const PUBLIC_ACCESS_INFO: Record<
  string,
  { label: string; color: string; description: string }
> = {
  OA: {
    label: "Open Access",
    color: "green",
    description: "Public access is allowed.",
  },
  RA: {
    label: "Restricted Access",
    color: "orange",
    description: "Public access is limited or controlled.",
  },
  XA: {
    label: "Closed Access",
    color: "red",
    description: "Public access is not allowed.",
  },
  UK: {
    label: "Unknown",
    color: "#D3D3D3",
    description: "Public access is unknown.",
  },
};

export const IUCN_CATEGORY_INFO: Record<
  string,
  { label: string; color: string; description: string }
> = {
  Ia: {
    label: "Strict Nature Reserve",
    color: "#00441b", // Dark green
    description:
      "Highly protected area allowing minimal human use, primarily for research and conservation.",
  },
  Ib: {
    label: "Wilderness Area",
    color: "#238b45", // Medium green
    description:
      "Large, untouched natural areas protected from significant human impact, allowing limited access.",
  },
  II: {
    label: "National Park",
    color: "#3690c0", // Blue
    description:
      "Large protected area preserving ecosystems while allowing recreation and tourism.",
  },
  III: {
    label: "Natural Monument",
    color: "#88419d", // Purple
    description:
      "Protected area preserving a specific natural landmark or cultural feature.",
  },
  IV: {
    label: "Habitat/Species Management Area",
    color: "#41b6c4", // Cyan
    description:
      "Area managed to maintain and protect particular species or habitats through active intervention.",
  },
  V: {
    label: "Protected Landscape/Seascape",
    color: "#fec44f", // Gold
    description:
      "Areas protecting the interaction of people and nature, allowing sustainable economic activities.",
  },
  VI: {
    label: "Sustainable Use Area",
    color: "#fe9929", // Orange
    description:
      "Protected area allowing sustainable use of natural resources with minimal environmental impact.",
  },
  "Other Conservation Area": {
    label: "Other Conservation Area",
    color: "#D3D3D3", // Light gray
    description: "Protected area that does not fit standard IUCN categories.",
  },
  Unknown: {
    label: "Unknown",
    color: "#D3D3D3", // Light gray
    description: "Category unknown or not classified.",
  },
};

export const ProtectedAreaDetails: FC<ProtectedAreaDetailsProps> = ({
  lat,
  lng,
}) => {
  const [protectedArea, setProtectedArea] = useState<any>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!lat || !lng) return;

    const fetchProtectedArea = async () => {
      setLoading(true);
      setError(null);
      setProtectedArea(null);

      const x = (lng * 20037508.34) / 180;
      const y =
        Math.log(Math.tan(((90 + lat) * Math.PI) / 360)) / (Math.PI / 180);
      const yMeters = (y * 20037508.34) / 180;

      const url = `https://services.arcgis.com/v01gqwM5QqNysAAi/arcgis/rest/services/PADUS_Protected_Areas_National/FeatureServer/0/query?f=json&geometry=${x},${yMeters}&outFields=*&returnGeometry=false&spatialRel=esriSpatialRelIntersects&geometryType=esriGeometryPoint&inSR=102100`;

      try {
        const resp = await fetch(url);
        const data = await resp.json();
        if (data.features?.length) {
          setProtectedArea(data.features[0].attributes);
        } else {
          setProtectedArea(null);
        }
      } catch (err) {
        setError("Error fetching protected area data.");
      }

      setLoading(false);
    };

    fetchProtectedArea();
  }, [lat, lng]);

  if (loading) return <Spin size="small" />;
  if (error) return <Text type="danger">{error}</Text>;
  if (!protectedArea) return <Text type="secondary">None</Text>;

  const gap = GAP_STATUS_INFO[protectedArea.GAP_Sts];
  const iucn =
    IUCN_CATEGORY_INFO[protectedArea.IUCN_Cat] ||
    IUCN_CATEGORY_INFO[protectedArea.IUCN_Cat_Reclass];
  const access = PUBLIC_ACCESS_INFO[protectedArea.Pub_Access];

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
      <div>
        <Text strong>Unit Name:</Text>{" "}
        <Text>{protectedArea.Unit_Nm || "Unknown"}</Text>
      </div>

      <div>
        <Text strong>GAP Status:</Text>{" "}
        {gap && <Tag color={gap.color}>{gap.label}</Tag>}{" "}
        <Text type="secondary">{gap?.description || "No description"}</Text>
      </div>

      <div>
        <Text strong>IUCN Category:</Text>{" "}
        {iucn && <Tag color={iucn.color}>{iucn.label}</Tag>}{" "}
        <Text type="secondary">{iucn?.description || "No description"}</Text>
      </div>

      <div>
        <Text strong>Public Access:</Text>{" "}
        {access && <Tag color={access.color}>{access.label}</Tag>}{" "}
        <Text type="secondary">{access?.description || "No description"}</Text>
      </div>

      <div>
        <Text strong>Manager Type:</Text>{" "}
        <Text>{protectedArea.MngTp_Desc || "Unknown"}</Text>
      </div>

      <div>
        <Text strong>Manager Name:</Text>{" "}
        <Text>{protectedArea.MngNm_Desc || "Unknown"}</Text>
      </div>

      <div>
        <Text strong>Designation Type:</Text>{" "}
        <Text>{protectedArea.DesTp_Desc || "Unknown"}</Text>
      </div>

      <div>
        <Text strong>Total Acres:</Text>{" "}
        <Text>
          {Math.round(
            protectedArea.GIS_AcrsDb || protectedArea.GIS_Acres || 0
          ).toLocaleString()}
        </Text>
      </div>

      <div style={{ marginTop: 4 }}>
        <a
          href={`https://www.google.com/search?q=${encodeURIComponent(
            protectedArea.Unit_Nm
          )}`}
          target="_blank"
          rel="noopener noreferrer"
        >
          Search on Google
        </a>
      </div>

      <div style={{ marginTop: 12 }}>
        <Text type="secondary" style={{ fontSize: 12 }}>
          Note: This information is not an indication of whether caving is or
          isn’t permitted on this land.
        </Text>
      </div>
    </div>
  );
};
