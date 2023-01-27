import { RcFile } from "antd/lib/upload";
import { HttpClient } from "../../..";
import { InviteMember } from "../../../Shared/Components/InviteMember";
import { HttpHelpers } from "../../../Shared/Helpers/HttpHelpers";
import { isNullOrWhiteSpace } from "../../../Shared/Helpers/StringHelpers";
import { SelectListItem } from "../../../Shared/Models/SelectListItem";
import { CreateLeadVm } from "../../Leads/Models/CreateLeadVm";
import { LeadVm } from "../../Leads/Models/Lead";
import { CreateOrEditTripObjectiveVm } from "../Models/CreateOrEditTripObjectiveVm";
import { TripObjectiveVm } from "../Models/TripObjectiveVm";
import { TripPhotosUpload, TripPhotoUpload } from "../Models/TripPhotoUpload";
import { TripPhotoVm } from "../Models/TripPhotoVm";

const baseUrl = "api/tripObjectives";
const TripObjectiveService = {
  //#region Objectives
  async GetObjective(id: string): Promise<TripObjectiveVm> {
    const response = await HttpClient.get<TripObjectiveVm>(`${baseUrl}/${id}`);
    return response.data;
  },
  async GetTags(id: string): Promise<SelectListItem<string>[]> {
    const response = await HttpClient.get<SelectListItem<string>[]>(
      `${baseUrl}/${id}/tags`
    );
    return response.data;
  },
  async AddTag(
    tagId: string,
    tripObjectiveId: string
  ): Promise<SelectListItem<string>[]> {
    const response = await HttpClient.post<SelectListItem<string>[]>(
      `${baseUrl}/${tripObjectiveId}/tags/${tagId}`,
      {}
    );
    return response.data;
  },
  async DeleteTag(
    tagId: string,
    tripObjectiveId: string
  ): Promise<SelectListItem<string>[]> {
    const response = await HttpClient.delete<SelectListItem<string>[]>(
      `${baseUrl}/${tripObjectiveId}/tags/${tagId}`
    );
    return response.data;
  },
  async AddTripObjective(
    values: CreateOrEditTripObjectiveVm
  ): Promise<TripObjectiveVm> {
    const response = await HttpClient.post<TripObjectiveVm>(
      `${baseUrl}`,
      values
    );
    return response.data;
  },

  async AddOrUpdateTripReport(
    tripObjectiveId: string,
    tripReport: string
  ): Promise<void> {
    const response = await HttpClient.post(
      `${baseUrl}/${tripObjectiveId}/tripReport`,
      tripReport,
      { headers: { "Content-Type": "application/json" } }
    );
  },

  async UpdateObjectiveName(
    name: string,
    tripObjectiveId: string
  ): Promise<void> {
    const response = await HttpClient.post(
      `${baseUrl}/${tripObjectiveId}/name`,
      name,
      { headers: { "Content-Type": "application/json" } }
    );
  },
  async UpdateObjectiveDescription(
    description: string,
    tripObjectiveId: string
  ): Promise<void> {
    const response = await HttpClient.post(
      `${baseUrl}/${tripObjectiveId}/description`,
      description,
      { headers: { "Content-Type": "application/json" } }
    );
  },
  //#endregion

  //#region Photos
  async UploadPhotos(
    values: TripPhotoUpload[],
    tripObjectiveId: string
  ): Promise<void> {
    const formData = new FormData();

    values.forEach((e, i) => {
      const title = values[i].title;
      const description = values[i].description;
      formData.append(
        `formData[${i}].title`,
        isNullOrWhiteSpace(title) ? "" : (title as string)
      );
      formData.append(
        `formData[${i}].description`,
        isNullOrWhiteSpace(description) ? "" : (description as string)
      );
      formData.append(`formData[${i}].file`, values[i].file as RcFile);
      formData.append(`formData[${i}].uid`, values[i].uid as string);
    });

    const response = await HttpClient.post<void>(
      `${baseUrl}/${tripObjectiveId}/photos`,
      formData,
      { headers: { "Content-Type": "multipart/form-data" } }
    );
    return response.data;
  },

  async GetTripObjectivePhotos(objectiveId: string): Promise<TripPhotoVm[]> {
    const response = await HttpClient.get<TripPhotoVm[]>(
      `${baseUrl}/${objectiveId}/photos`
    );
    return response.data;
  },

  //#endregion

  //#region Trip Objective Members
  async GetTripObjectiveMembers(
    objectiveId: string
  ): Promise<SelectListItem<string>[]> {
    const response = await HttpClient.get<SelectListItem<string>[]>(
      `${baseUrl}/${objectiveId}/members`
    );
    return response.data;
  },
  async AddTripObjectiveMember(
    userId: string,
    objectiveId: string
  ): Promise<SelectListItem<string>[]> {
    const response = await HttpClient.post<SelectListItem<string>[]>(
      `${baseUrl}/${objectiveId}/members/${userId}`,
      {}
    );
    return response.data;
  },
  async AddTripObjectiveMembers(
    userIds: string[],
    objectiveId: string
  ): Promise<SelectListItem<string>[]> {
    const response = await HttpClient.post<SelectListItem<string>[]>(
      `${baseUrl}/${objectiveId}/members`,
      userIds
    );
    return response.data;
  },

  async DeleteTripObjectiveMember(
    userId: string,
    objectiveId: string
  ): Promise<SelectListItem<string>[]> {
    const response = await HttpClient.delete<SelectListItem<string>[]>(
      `${baseUrl}/${objectiveId}/members/${userId}`,
      {}
    );
    return response.data;
  },
  //#endregion

  //#region Leads
  async GetLeads(objectiveId: string): Promise<LeadVm[]> {
    const response = await HttpClient.get<LeadVm[]>(
      `${baseUrl}/${objectiveId}/leads`
    );
    return response.data;
  },

  async AddLeads(leads: CreateLeadVm[], objectiveId: string): Promise<void> {
    const response = await HttpClient.post<void>(
      `${baseUrl}/${objectiveId}/leads`,
      leads
    );
  },

  //#endregion

  //#region

  async InviteTripObjectiveMembers(
    values: InviteMember,
    objectiveId: string
  ): Promise<void> {
    const response = await HttpClient.post<void>(
      `${baseUrl}/${objectiveId}/members/invite`,
      values
    );
    return response.data;
  },
  //#endregion
};

export { TripObjectiveService };
