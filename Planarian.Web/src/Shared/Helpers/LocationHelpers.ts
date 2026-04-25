import type { MessageInstance } from "antd/es/message/interface";
import { formatCoordinateNumber } from "./StringHelpers";

// Singleton to ensure only one location request happens at a time
let locationRequestPromise: Promise<{ latitude: number; longitude: number; } | null> | null = null;

export const LocationHelpers = {
    getDirectionsUrl: (
        latitude: string | number,
        longitude: string | number
    ): string =>
        `https://www.google.com/maps/dir/?api=1&destination=${latitude},${longitude}&travelmode=car`,

    getDistanceFeet: (
        fromLatitude: number,
        fromLongitude: number,
        toLatitude: number,
        toLongitude: number
    ): number => {
        const earthRadiusMiles = 3958.8;
        const toRadians = (degrees: number) => degrees * (Math.PI / 180);
        const latitudeDelta = toRadians(toLatitude - fromLatitude);
        const longitudeDelta = toRadians(toLongitude - fromLongitude);
        const fromLatitudeRadians = toRadians(fromLatitude);
        const toLatitudeRadians = toRadians(toLatitude);

        const haversine =
            Math.sin(latitudeDelta / 2) * Math.sin(latitudeDelta / 2) +
            Math.cos(fromLatitudeRadians) *
            Math.cos(toLatitudeRadians) *
            Math.sin(longitudeDelta / 2) *
            Math.sin(longitudeDelta / 2);

        const distanceMiles =
            earthRadiusMiles *
            2 *
            Math.atan2(Math.sqrt(haversine), Math.sqrt(1 - haversine));

        return distanceMiles * 5280;
    },

    getUsersLocation: async (message?: MessageInstance): Promise<{ latitude: number; longitude: number; } | null> => {
        if (locationRequestPromise) {
            return locationRequestPromise;
        }

        if (!navigator?.geolocation) {
            message?.error("Geolocation is not supported in this browser.");
            return null;
        }

        locationRequestPromise = new Promise<{ latitude: number; longitude: number; } | null>(async (resolve) => {
            try {
                const position = await new Promise<GeolocationPosition>((resolvePos, rejectPos) => {
                    navigator.geolocation.getCurrentPosition(
                        (position) => {
                            resolvePos(position);
                        },
                        (error) => {
                            rejectPos(error);
                        },
                        {
                            enableHighAccuracy: true,
                            timeout: 15000,
                            maximumAge: 0,
                        }
                    );
                });

                const { latitude, longitude } = position.coords;
                const result = { latitude: formatCoordinateNumber(latitude), longitude: formatCoordinateNumber(longitude) };
                resolve(result);
            } catch (error: any) {
                switch (error.code) {
                    case GeolocationPositionError.PERMISSION_DENIED:
                        message?.error("Location access denied by user.");
                        break;
                    case GeolocationPositionError.POSITION_UNAVAILABLE:
                        message?.error("Location information is unavailable.");
                        break;
                    case GeolocationPositionError.TIMEOUT:
                        message?.error("Location request timed out.");
                        break;
                    default:
                        message?.error(error.message || "Unable to fetch current location.");
                        break;
                }
                resolve(null);
            }
        });

        const result = await locationRequestPromise;

        locationRequestPromise = null;

        return result;
    },
};
