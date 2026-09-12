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

  async getNearbyStreamGages(
    origins: StreamGageSearchOrigin[],
    distanceMiles: number
  ) {
    const response = await HttpClient.post<NearbyStreamGage[]>(
      `${baseUrl}/hydrology/gages`,
      { origins, distanceMiles }
    );
    return response.data;
  },

  async getStreamGageObservations(
    siteCode: string,
    startDate: string,
    endDate: string
  ) {
    const params = new URLSearchParams({ startDate, endDate });
    const response = await HttpClient.get<StreamGageParameter[]>(
      `${baseUrl}/hydrology/gages/${siteCode}/observations?${params}`
    );
    return response.data;
  },

  async getStreamGagePeakSummary(siteCode: string) {
    const response = await HttpClient.get<StreamGagePeakSummary>(
      `${baseUrl}/hydrology/gages/${siteCode}/peaks`
    );
    return response.data;
  },

  async getStreamGagesInBounds(
    north: number,
    south: number,
    east: number,
    west: number
  ) {
    const params = new URLSearchParams({
      north: north.toString(),
      south: south.toString(),
      east: east.toString(),
      west: west.toString(),
    });
    const response = await HttpClient.get<StreamGageLocation[]>(
      `${baseUrl}/hydrology/gages/bounds?${params}`
    );
    return response.data;
  },

  async getHydrologyFeaturesInBounds(
    north: number,
    south: number,
    east: number,
    west: number
  ) {
    const params = new URLSearchParams({
      north: north.toString(),
      south: south.toString(),
      east: east.toString(),
      west: west.toString(),
    });
    const response = await HttpClient.get<HydrologyFeature[]>(
      `${baseUrl}/hydrology/features/bounds?${params}`
    );
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

export interface StreamGageSearchOrigin {
  id: string;
  name: string;
  latitude: number;
  longitude: number;
}

export interface HydrologyFeature {
  id: string;
  name?: string | null;
  featureType: string;
  latitude: number;
  longitude: number;
  source: string;
}

export interface StreamGageLocation {
  id: string;
  siteCode: string;
  siteName: string;
  latitude: number;
  longitude: number;
}

export interface StreamGagePoint {
  value: string;
  dateTime: string;
  approvalStatus?: string | null;
}

export interface StreamGageParameter {
  parameterCode: string;
  variableName: string;
  unit: string;
  points: StreamGagePoint[];
}

export interface NearbyStreamGage {
  id: string;
  siteCode: string;
  siteName: string;
  latitude: number;
  longitude: number;
  distanceMiles: number;
  nearestOriginId?: string | null;
  nearestOriginName?: string | null;
  drainageAreaSquareMiles?: number | null;
  contributingDrainageAreaSquareMiles?: number | null;
  parameters: StreamGageParameter[];
}

export interface StreamGageAnnualPeak {
  parameterCode: string;
  variableName: string;
  unit: string;
  value: string;
  date?: string | null;
  waterYear: number;
  qualifiers?: string[];
  qualifier?: string | string[] | null;
}

export interface StreamGageHistoricalPeak extends StreamGageAnnualPeak {
  firstWaterYear: number;
  lastWaterYear: number;
  annualPeakCount: number;
}

export interface StreamGagePeakSummary {
  siteCode: string;
  streamflow?: StreamGageHistoricalPeak | null;
  gageHeight?: StreamGageHistoricalPeak | null;
  streamflowHistory?: StreamGageAnnualPeak[];
  gageHeightHistory?: StreamGageAnnualPeak[];
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
