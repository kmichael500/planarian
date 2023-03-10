import { RcFile } from "antd/lib/upload";
import { HttpClient } from "../../..";
import { InviteMember } from "../../../Shared/Models/InviteMember";
import { isNullOrWhiteSpace } from "../../../Shared/Helpers/StringHelpers";
import { SelectListItem } from "../../../Shared/Models/SelectListItem";
import { CreateLeadVm } from "../../Lead/Models/CreateLeadVm";
import { LeadVm } from "../../Lead/Models/LeadVm";
import { TripVm } from "../Models/TripVm";
import { TripPhotoUpload } from "../Models/TripPhotoUpload";
import { PhotoVm } from "../../Photo/Models/PhotoVm";

const baseUrl = "api/trips";
const TripService = {
  //#region Trips
  async GetTrip(id: string): Promise<TripVm> {
    const response = await HttpClient.get<TripVm>(`${baseUrl}/${id}`);
    return response.data;
  },
  async GetTags(id: string): Promise<SelectListItem<string>[]> {
    const response = await HttpClient.get<SelectListItem<string>[]>(
      `${baseUrl}/${id}/tags`
    );
    return response.data;
  },
  async AddTripTag(
    tagTypeId: string,
    tripId: string
  ): Promise<SelectListItem<string>[]> {
    const response = await HttpClient.post<SelectListItem<string>[]>(
      `${baseUrl}/${tripId}/tags/${tagTypeId}`,
      {}
    );
    return response.data;
  },
  async DeleteTripTag(
    tagTypeId: string,
    tripId: string
  ): Promise<SelectListItem<string>[]> {
    const response = await HttpClient.delete<SelectListItem<string>[]>(
      `${baseUrl}/${tripId}/tags/${tagTypeId}`
    );
    return response.data;
  },

  async AddOrUpdateTripReport(
    tripId: string,
    tripReport: string
  ): Promise<void> {
    await HttpClient.post(`${baseUrl}/${tripId}/trip-report`, tripReport, {
      headers: { "Content-Type": "application/json" },
    });
  },

  async UpdateTripName(name: string, tripId: string): Promise<void> {
    await HttpClient.post(`${baseUrl}/${tripId}/name`, name, {
      headers: { "Content-Type": "application/json" },
    });
  },
  async UpdateTripDescription(
    description: string,
    tripId: string
  ): Promise<void> {
    await HttpClient.post(`${baseUrl}/${tripId}/description`, description, {
      headers: { "Content-Type": "application/json" },
    });
  },
  //#endregion

  //#region Photos
  async UploadPhotos(values: TripPhotoUpload[], tripId: string): Promise<void> {
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
      `${baseUrl}/${tripId}/photos`,
      formData,
      { headers: { "Content-Type": "multipart/form-data" } }
    );
    return response.data;
  },

  async GetTripPhotos(tripId: string): Promise<PhotoVm[]> {
    const response = await HttpClient.get<PhotoVm[]>(
      `${baseUrl}/${tripId}/photos`
    );
    return response.data;
  },

  //#endregion

  //#region Trip Members
  async GetTripMembers(tripId: string): Promise<SelectListItem<string>[]> {
    const response = await HttpClient.get<SelectListItem<string>[]>(
      `${baseUrl}/${tripId}/members`
    );
    return response.data;
  },
  async AddTripMember(
    userId: string,
    tripId: string
  ): Promise<SelectListItem<string>[]> {
    const response = await HttpClient.post<SelectListItem<string>[]>(
      `${baseUrl}/${tripId}/members/${userId}`,
      {}
    );
    return response.data;
  },
  async AddTripMembers(
    userIds: string[],
    tripId: string
  ): Promise<SelectListItem<string>[]> {
    const response = await HttpClient.post<SelectListItem<string>[]>(
      `${baseUrl}/${tripId}/members`,
      userIds
    );
    return response.data;
  },

  async DeleteTripMember(
    userId: string,
    tripId: string
  ): Promise<SelectListItem<string>[]> {
    const response = await HttpClient.delete<SelectListItem<string>[]>(
      `${baseUrl}/${tripId}/members/${userId}`,
      {}
    );
    return response.data;
  },
  //#endregion

  //#region Leads
  async GetLeads(tripId: string): Promise<LeadVm[]> {
    const response = await HttpClient.get<LeadVm[]>(
      `${baseUrl}/${tripId}/leads`
    );
    return response.data;
  },

  async AddLeads(leads: CreateLeadVm[], tripId: string): Promise<void> {
    await HttpClient.post<void>(`${baseUrl}/${tripId}/leads`, leads);
  },

  //#endregion

  //#region

  async InviteTripMembers(values: InviteMember, tripId: string): Promise<void> {
    const response = await HttpClient.post<void>(
      `${baseUrl}/${tripId}/members/invite`,
      values
    );
    return response.data;
  },
  //#endregion
};

export { TripService };
