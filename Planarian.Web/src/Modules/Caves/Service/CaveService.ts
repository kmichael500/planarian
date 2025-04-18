import { RcFile } from "antd/lib/upload";
import { HttpClient } from "../../..";
import { PagedResult } from "../../Search/Models/PagedResult";
import {
  QueryBuilder,
  QueryOperator,
} from "../../Search/Services/QueryBuilder";
import { AddCaveVm } from "../Models/AddCaveVm";
import { CaveVm } from "../Models/CaveVm";
import { CaveSearchParamsVm } from "../Models/CaveSearchParamsVm";
import { FileVm } from "../../Files/Models/FileVm";
import { AxiosProgressEvent, AxiosRequestConfig } from "axios";
import { CaveSearchVm } from "../Models/CaveSearchVm";
import { PermissionKey } from "../../Authentication/Models/PermissionKey";
import { isNullOrWhiteSpace } from "../../../Shared/Helpers/StringHelpers";
import { FavoriteVm } from "../Models/FavoriteCaveVm";
import { GeoJsonUploadVm } from "../Models/GeoJsonUploadVm";
import { ProposeChangeRequestVm } from "../Models/ProposeChangeRequestVm";

const baseUrl = "api/caves";
const CaveService = {
  async GetCaves(
    queryBuilder: QueryBuilder<CaveSearchParamsVm>
  ): Promise<PagedResult<CaveSearchVm>> {
    const response = await HttpClient.get<PagedResult<CaveSearchVm>>(
      `${baseUrl}?${queryBuilder.buildAsQueryString()}`
    );
    return response.data;
  },
  async ExportCavesGpx(
    queryBuilder: QueryBuilder<CaveSearchParamsVm>
  ): Promise<Blob> {
    const response = await HttpClient.get(
      `${baseUrl}/export/gpx?${queryBuilder.buildAsQueryString()}`,
      { responseType: "blob" }
    );
    return response.data;
  },
  async ProposeChange(values: ProposeChangeRequestVm): Promise<void> {
    await HttpClient.post<string>(`${baseUrl}/`, values);
  },
  async GetCave(id: string): Promise<CaveVm> {
    const response = await HttpClient.get<CaveVm>(`${baseUrl}/${id}`);
    return response.data;
  },
  async ArchiveCave(id: string): Promise<void> {
    const response = await HttpClient.post<void>(`${baseUrl}/${id}/archive`);
    return response.data;
  },
  async UnarchiveCave(id: string): Promise<void> {
    const response = await HttpClient.post<void>(`${baseUrl}/${id}/unarchive`);
    return response.data;
  },
  async DeleteCave(id: string): Promise<void> {
    const response = await HttpClient.delete<void>(`${baseUrl}/${id}`);
    return response.data;
  },
  async DeleteAllCaves(): Promise<void> {
    const response = await HttpClient.delete<void>(`${baseUrl}/import`);
    return response.data;
  },
  async AddCaveFile(
    file: string | Blob | RcFile,
    caveId: string,
    uuid: string,
    onProgress: (progressEvent: AxiosProgressEvent) => void
  ): Promise<FileVm> {
    const formData = new FormData();
    formData.append("file", file);

    const config: AxiosRequestConfig = {
      headers: {
        "Content-Type": "multipart/form-data",
      },
      onUploadProgress: onProgress, // Set the onUploadProgress callback
    };

    const response = await HttpClient.post<FileVm>(
      `${baseUrl}/${caveId}/files?uuid=${uuid}`,
      formData,
      config
    );
    return response.data;
  },

  async SearchCavesPaged(
    name: string,
    pageNumber: number = 1,
    pageSize: number = 10,
    permissionKey: PermissionKey | null = null
  ): Promise<PagedResult<CaveSearchVm>> {
    const qb = new QueryBuilder<CaveSearchParamsVm>("", false);
    qb.filterBy("name", QueryOperator.Contains, name as any);
    qb.changePage(pageNumber, pageSize);

    const queryString = !isNullOrWhiteSpace(permissionKey)
      ? `&permissionKey=${permissionKey}`
      : "";

    const response = await HttpClient.get<PagedResult<CaveSearchVm>>(
      `${baseUrl}/search?${qb.buildAsQueryString()}${queryString}`
    );
    return response.data;
  },

  async FavoriteCave(id: string): Promise<void> {
    const response = await HttpClient.post<void>(`${baseUrl}/${id}/favorite`);
    return response.data;
  },
  async UnfavoriteCave(id: string): Promise<void> {
    const response = await HttpClient.delete<void>(`${baseUrl}/${id}/favorite`);
    return response.data;
  },
  async GetFavoriteCaveVm(caveId: string) {
    const response = await HttpClient.get<FavoriteVm | null>(
      `${baseUrl}/${caveId}/favorite`
    );
    return response.data;
  },
  async uploadCaveGeoJson(
    caveId: string,
    geoJsonUploads: GeoJsonUploadVm[]
  ): Promise<void> {
    const response = await HttpClient.post<void>(
      `${baseUrl}/${caveId}/geojson`,
      geoJsonUploads
    );
    return response.data;
  },
};

export { CaveService };
