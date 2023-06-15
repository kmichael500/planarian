import { HttpClient } from "../../..";
import { PagedResult } from "../../Search/Models/PagedResult";
import { QueryBuilder } from "../../Search/Services/QueryBuilder";
import { AddCaveVm } from "../Models/AddCaveVm";
import { CaveVm } from "../Models/CaveVm";

const baseUrl = "api/caves";
const CaveService = {
  async GetCaves(
    queryBuilder: QueryBuilder<CaveVm>
  ): Promise<PagedResult<CaveVm>> {
    const response = await HttpClient.get<PagedResult<CaveVm>>(
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
};
export { CaveService };
