import { HttpClient } from "../../..";
import { PlanarianError } from "../../../Shared/Exceptions/PlanarianErrors";
import { AuthenticationService } from "../../Authentication/Services/AuthenticationService";
import { MapData } from "../Components/MapData";
import { Cluster } from "../Models/ClusterPoint";

const baseUrl = "api/map";
const cacheDuration = 24 * 60 * 60 * 1000; // 1 day in milliseconds

const MapService = {
  getMapData: async (
    north: number,
    south: number,
    east: number,
    west: number,
    zoom: number
  ) => {
    const accountId = AuthenticationService.GetAccountId();
    if (!accountId) {
      throw new PlanarianError("No account id found");
    }
    const cacheKey = `caves-${accountId}`;
    const cachedData = localStorage.getItem(cacheKey);
    const now = Date.now();

    if (cachedData) {
      const { data, timestamp } = JSON.parse(cachedData);
      if (now - timestamp < cacheDuration) {
        return data;
      }
    }

    const response = await HttpClient.get<MapData[]>(`${baseUrl}`, {
      params: { north, south, east, west, zoom },
    });
    localStorage.setItem(
      cacheKey,
      JSON.stringify({ data: response.data, timestamp: now })
    );
    return response.data;
  },

  getMapCenter: async () => {
    const accountId = AuthenticationService.GetAccountId();
    if (!accountId) {
      throw new PlanarianError("No account id found");
    }
    const cacheKey = `mapCenter-${accountId}`;
    const cachedData = localStorage.getItem(cacheKey);
    const now = Date.now();

    if (cachedData) {
      const { data, timestamp } = JSON.parse(cachedData);
      if (now - timestamp < cacheDuration) {
        return data;
      }
    }

    const response = await HttpClient.get<CoordinateDto>(`${baseUrl}/center`);
    localStorage.setItem(
      cacheKey,
      JSON.stringify({ data: response.data, timestamp: now })
    );
    return response.data;
  },
};

export interface CoordinateDto {
  latitude: number;
  longitude: number;
}

export { MapService };
