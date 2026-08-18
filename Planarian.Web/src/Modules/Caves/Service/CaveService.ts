import { RcFile } from "antd/lib/upload";
import { HttpClient } from "../../../Shared/Http/HttpClient";
import { PagedResult } from "../../Search/Models/PagedResult";
import {
  QueryBuilder,
  QueryOperator,
} from "../../Search/Services/QueryBuilder";
import { AddCaveVm } from "../Models/AddCaveVm";
import { CaveVm } from "../Models/CaveVm";
import { CaveSearchParamsVm } from "../Models/CaveSearchParamsVm";
import { FileVm } from "../../Files/Models/FileVm";
import { AxiosProgressEvent } from "axios";
import { CaveSearchVm } from "../Models/CaveSearchVm";
import { PermissionKey } from "../../Authentication/Models/PermissionKey";
import { isNullOrWhiteSpace } from "../../../Shared/Helpers/StringHelpers";
import { FavoriteVm } from "../Models/FavoriteCaveVm";
import {
  CaveRevisionComparisonVm,
  CaveRevisionHistoryVm,
} from "../Models/CaveRevisionVm";
import {
  CaveChangeRequestDecisionVm,
  CaveChangeRequestDetailVm,
  CaveChangeRequestSummaryVm,
  CaveChangePreviewVm,
  CaveEditAuthoringContextVm,
  CaveProposalAuthoringContextVm,
  CaveProposalVersionDetailVm,
} from "../Models/CaveChangeRequestVm";
import { FeatureKey } from "../../Account/Models/FeatureSettingVm";
import { HttpHelpers } from "../../../Shared/Helpers/HttpHelpers";

const baseUrl = "api/caves";
const changeRequestUrl = "api/cave-change-requests";
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
    queryBuilder: QueryBuilder<CaveSearchParamsVm>,
    exportFields?: FeatureKey[]
  ): Promise<Blob> {
    const params = new URLSearchParams(queryBuilder.buildAsQueryString());
    exportFields?.forEach((field) => {
      params.append("exportFields", field);
    });

    const paramsString = params.toString();
    const url = paramsString
      ? `${baseUrl}/export/gpx?${paramsString}`
      : `${baseUrl}/export/gpx`;

    const response = await HttpClient.get(url, { responseType: "blob" });
    return response.data;
  },
  async ExportCavesCsv(
    queryBuilder: QueryBuilder<CaveSearchParamsVm>,
    exportFields?: FeatureKey[]
  ): Promise<Blob> {
    const params = new URLSearchParams(queryBuilder.buildAsQueryString());
    exportFields?.forEach((field) => {
      params.append("exportFields", field);
    });

    const paramsString = params.toString();
    const url = paramsString
      ? `${baseUrl}/export/csv?${paramsString}`
      : `${baseUrl}/export/csv`;

    const response = await HttpClient.get<Blob>(url, {
      responseType: "blob",
    });
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
  async GetEditAuthoringContext(id: string): Promise<CaveEditAuthoringContextVm> {
    return (await HttpClient.get<CaveEditAuthoringContextVm>(
      `${baseUrl}/${id}/edit-context`
    )).data;
  },
  async GetProposalAuthoringContext(id: string): Promise<CaveProposalAuthoringContextVm> {
    return (await HttpClient.post<CaveProposalAuthoringContextVm>(
      `${changeRequestUrl}/caves/${id}/authoring-context`
    )).data;
  },
  async GetRevisionHistory(id: string): Promise<CaveRevisionHistoryVm> {
    const response = await HttpClient.get<CaveRevisionHistoryVm>(
      `${baseUrl}/${id}/revisions`
    );
    return response.data;
  },
  async GetRevision(
    caveId: string,
    revisionId: string
  ): Promise<CaveRevisionComparisonVm> {
    const response = await HttpClient.get<CaveRevisionComparisonVm>(
      `${baseUrl}/${caveId}/revisions/${revisionId}`
    );
    return response.data;
  },
  async PreviewChanges(caveId: string, cave: AddCaveVm, expectedBaseRevisionId: string): Promise<CaveChangePreviewVm> {
    const response = await HttpClient.post<CaveChangePreviewVm>(
      `${changeRequestUrl}/caves/${caveId}/preview`, { cave, expectedBaseRevisionId }
    );
    return response.data;
  },
  async SubmitChanges(caveId: string, cave: AddCaveVm, expectedBaseRevisionId: string): Promise<string> {
    const response = await HttpClient.post<string>(
      `${changeRequestUrl}/caves/${caveId}`, { cave, expectedBaseRevisionId }
    );
    return response.data;
  },
  async PreviewRevisedChanges(requestId: string, cave: AddCaveVm, againstCurrent: boolean,
    expectedBaseRevisionId: string, expectedProposalVersionId: string): Promise<CaveChangePreviewVm> {
    const response = await HttpClient.post<CaveChangePreviewVm>(
      `${changeRequestUrl}/${requestId}/versions/preview`,
      { cave, againstCurrent, expectedBaseRevisionId, expectedProposalVersionId }
    );
    return response.data;
  },
  async ReviseChanges(requestId: string, cave: AddCaveVm, againstCurrent: boolean,
    expectedBaseRevisionId: string, expectedProposalVersionId: string): Promise<string> {
    const response = await HttpClient.post<string>(
      `${changeRequestUrl}/${requestId}/versions`,
      { cave, againstCurrent, expectedBaseRevisionId, expectedProposalVersionId }
    );
    return response.data;
  },
  async StageAuthoringFile(
    file: string | Blob | RcFile,
    uuid: string,
    onProgress: (progressEvent: AxiosProgressEvent) => void
  ): Promise<FileVm> {
    const formData = new FormData();
    formData.append("file", file);
    return (await HttpClient.post<FileVm>(
      `api/files/staged?uuid=${encodeURIComponent(uuid)}`,
      formData,
      { headers: { "Content-Type": "multipart/form-data" }, onUploadProgress: onProgress }
    )).data;
  },
  async DeleteAuthoringStagedFile(fileId: string): Promise<void> {
    await HttpClient.delete(`api/files/staged/${encodeURIComponent(fileId)}`);
  },
  GetStagedChangeRequestFileUrl(requestId: string, fileId: string): string {
    return HttpHelpers.BuildAuthenticatedApiUrl(
      `${changeRequestUrl}/${encodeURIComponent(requestId)}/files/${encodeURIComponent(fileId)}`
    );
  },
  async GetMyChangeRequests(pageNumber = 1, pageSize = 10): Promise<PagedResult<CaveChangeRequestSummaryVm>> {
    return (await HttpClient.get<PagedResult<CaveChangeRequestSummaryVm>>(
      `${changeRequestUrl}/mine?pageNumber=${pageNumber}&pageSize=${pageSize}`)).data;
  },
  async GetReviewQueue(pageNumber = 1, pageSize = 10): Promise<PagedResult<CaveChangeRequestSummaryVm>> {
    return (await HttpClient.get<PagedResult<CaveChangeRequestSummaryVm>>(
      `${changeRequestUrl}/review?pageNumber=${pageNumber}&pageSize=${pageSize}`)).data;
  },
  async GetChangeRequest(id: string): Promise<CaveChangeRequestDetailVm> {
    return (await HttpClient.get<CaveChangeRequestDetailVm>(`${changeRequestUrl}/${id}`)).data;
  },
  async GetProposalVersion(requestId: string, versionId: string): Promise<CaveProposalVersionDetailVm> {
    return (await HttpClient.get<CaveProposalVersionDetailVm>(
      `${changeRequestUrl}/${requestId}/versions/${versionId}`
    )).data;
  },
  async ApproveChangeRequest(id: string, expectedProposalVersionId: string,
    notes?: string): Promise<CaveChangeRequestDecisionVm> {
    return (await HttpClient.post<CaveChangeRequestDecisionVm>(`${changeRequestUrl}/${id}/approve`,
      { notes, expectedProposalVersionId })).data;
  },
  async RejectChangeRequest(id: string, expectedProposalVersionId: string,
    notes: string): Promise<CaveChangeRequestDecisionVm> {
    return (await HttpClient.post<CaveChangeRequestDecisionVm>(`${changeRequestUrl}/${id}/reject`,
      { notes, expectedProposalVersionId })).data;
  },
  async GetNextCountyNumber(
    countyId: string,
    useFirstAvailableCountyNumber: boolean = false
  ): Promise<number> {
    const response = await HttpClient.get<number>(
      `${baseUrl}/counties/${countyId}/next-number?useFirstAvailableCountyNumber=${useFirstAvailableCountyNumber}`
    );
    return response.data;
  },
  async IsCountyNumberInUse(
    countyId: string,
    countyNumber: number,
    caveId?: string
  ): Promise<boolean> {
    const queryString = !isNullOrWhiteSpace(caveId) ? `?caveId=${caveId}` : "";
    const response = await HttpClient.get<boolean>(
      `${baseUrl}/counties/${countyId}/county-numbers/${countyNumber}/in-use${queryString}`
    );
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

};

export { CaveService };
