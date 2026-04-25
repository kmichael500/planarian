import { CarOutlined } from "@ant-design/icons";
import { useEffect, useState } from "react";
import { LocationHelpers } from "../../Helpers/LocationHelpers";
import { formatDistance } from "../../Helpers/StringHelpers";
import { PlanarianButton } from "../Buttons/PlanarianButtton";
import "./DistanceFromMeComponent.scss";

interface DistanceFromMeComponentProps {
  latitude: number | null | undefined;
  longitude: number | null | undefined;
  className?: string;
}

const DistanceFromMeComponent = ({
  latitude,
  longitude,
  className,
}: DistanceFromMeComponentProps) => {
  const [distanceFeet, setDistanceFeet] = useState<number | null>(null);

  useEffect(() => {
    let isMounted = true;

    if (
      latitude === null ||
      latitude === undefined ||
      longitude === null ||
      longitude === undefined
    ) {
      setDistanceFeet(null);
      return;
    }

    const loadDistance = async () => {
      const userLocation = await LocationHelpers.getUsersLocation();

      if (!isMounted) {
        return;
      }

      if (!userLocation) {
        setDistanceFeet(null);
        return;
      }

      setDistanceFeet(
        LocationHelpers.getDistanceFeet(
          userLocation.latitude,
          userLocation.longitude,
          latitude,
          longitude
        )
      );
    };

    loadDistance();

    return () => {
      isMounted = false;
    };
  }, [latitude, longitude]);

  if (
    latitude === null ||
    latitude === undefined ||
    longitude === null ||
    longitude === undefined
  ) {
    return null;
  }

  const label =
    distanceFeet !== null
      ? `${formatDistance(distanceFeet)} from me`
      : "Directions";

  return (
    <PlanarianButton
      alwaysShowChildren
      aria-label="Get directions"
      className={["distance-from-me", className].filter(Boolean).join(" ")}
      href={LocationHelpers.getDirectionsUrl(latitude, longitude)}
      icon={<CarOutlined />}
      rel="noreferrer"
      size="small"
      target="_blank"
      type="link"
    >
      {label}
    </PlanarianButton>
  );
};

export { DistanceFromMeComponent };
