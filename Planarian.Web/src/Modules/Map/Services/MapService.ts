import type { FeatureCollection } from "geojson";
import { HttpClient } from "../../../Shared/Http/HttpClient";

const baseUrl = "api/map";

const MapService = {
  async getMapCenter() {
    const response = await HttpClient.get<CoordinateDto>(`${baseUrl}/center`);
    return response.data;
  },

  async getLinePlotIds(
    north: number,
    south: number,
    east: number,
    west: number,
    zoom: number
  ) {
    const params = new URLSearchParams({
      north: north.toString(),
      south: south.toString(),
      east: east.toString(),
      west: west.toString(),
      zoom: zoom.toString(),
    });
    const response = await HttpClient.get<string[]>(`${baseUrl}/lineplots/ids?${params}`);
    return response.data;
  },


  async getLinePlot(linePlotId: string) {
    const response = await HttpClient.get<FeatureCollection>(`${baseUrl}/lineplots/${linePlotId}`);
    return response.data;
  },

  async getGeologicMaps(latitude: number, longitude: number) {
    const params = new URLSearchParams({
      latitude: latitude.toString(),
      longitude: longitude.toString(),
    });
    const response = await HttpClient.get<GeologicMapResult[]>(`${baseUrl}/geologic-maps?${params}`);
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
