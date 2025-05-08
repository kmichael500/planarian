import { HttpClient } from "../../..";
import { FeatureCollection } from "geojson";
const baseUrl = "api/map";
const cacheDuration = 24 * 60 * 60 * 1000; // 1 day in milliseconds

const MapService = {
  // getMapData: async (
  //   north: number,
  //   south: number,
  //   east: number,
  //   west: number,
  //   zoom: number
  // ) => {
  //   const accountId = AuthenticationService.GetAccountId();
  //   if (!accountId) {
  //     throw new PlanarianError("No account id found");
  //   }
  //   const cacheKey = `caves-${accountId}`;
  //   const cachedData = localStorage.getItem(cacheKey);
  //   const now = Date.now();

  //   if (cachedData) {
  //     const { data, timestamp } = JSON.parse(cachedData);
  //     if (now - timestamp < cacheDuration) {
  //       return data;
  //     }
  //   }

  //   const response = await HttpClient.get<MapData[]>(`${baseUrl}`, {
  //     params: { north, south, east, west, zoom },
  //   });
  //   localStorage.setItem(
  //     cacheKey,
  //     JSON.stringify({ data: response.data, timestamp: now })
  //   );
  //   return response.data;
  // },

  getMapCenter: async () => {
    const response = await HttpClient.get<CoordinateDto>(`${baseUrl}/center`);

    return response.data;
  },
  getLinePlotIds: async (
    tileNorth: number,
    tileSouth: number,
    tileEast: number,
    tileWest: number,
    zoom: number
  ) => {
    const params = new URLSearchParams();
    params.append("north", tileNorth.toString());
    params.append("south", tileSouth.toString());
    params.append("east", tileEast.toString());
    params.append("west", tileWest.toString());
    params.append("zoom", zoom.toString());
    const response = await HttpClient.get<string[]>(
      `${baseUrl}/lineplots/ids?${params}`
    );

    return response.data;
  },

  getLinePlot: async (linePlotId: string) => {
    const response = await HttpClient.get<FeatureCollection>(
      `${baseUrl}/lineplots/${linePlotId}`
    );

    return response.data;
  },

  getGeologicMaps: async (latitude: number, longitude: number) => {
    const params = new URLSearchParams();
    params.append("latitude", latitude.toString());
    params.append("longitude", longitude.toString());
    const response = await HttpClient.get<GeologicMapResult[]>(
      `${baseUrl}/geologic-maps?${params}`
    );

    return response.data;
  },
};

export interface CoordinateDto {
  latitude: number;
  longitude: number;
}

export interface GeologicMapResult {
  id: number;
  title: string;
  authors: string;
  publisher: string;
  series: string;
  year: number;
  scale: number;
  include: number;
  bed_surf: number;
  gis?: number;
  thumbnail?: string;
  north: string;
  south: string;
  east: string;
  west: string;
  mv?: number;
}

export { MapService };
