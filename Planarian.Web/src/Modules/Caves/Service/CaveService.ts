import { HttpClient } from "../../..";
import { PagedResult } from "../../Search/Models/PagedResult";
import { QueryBuilder } from "../../Search/Services/QueryBuilder";
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
};
export { CaveService };
