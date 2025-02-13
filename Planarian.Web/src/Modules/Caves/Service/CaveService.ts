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
  async AddCave(values: AddCaveVm): Promise<string> {
    const response = await HttpClient.post<string>(`${baseUrl}`, values);
    return response.data;
  },
  async UpdateCave(values: AddCaveVm): Promise<void> {
    await HttpClient.put<string>(`${baseUrl}/`, values);
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
    pageSize: number = 10
  ): Promise<PagedResult<CaveSearchVm>> {
    if (!name) {
      return {
        results: [],
        pageNumber: 1,
        pageSize: pageSize,
        totalCount: 0,
        totalPages: 0,
      };
    }

    const qb = new QueryBuilder<CaveSearchParamsVm>("", false);
    qb.filterBy("name", QueryOperator.Contains, name as any);
    qb.changePage(pageNumber, pageSize);

    return await CaveService.GetCaves(qb);
  },
};
export { CaveService };
