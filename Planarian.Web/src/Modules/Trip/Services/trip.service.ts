import { HttpClient } from "../../..";
import { SelectListItem } from "../../../Shared/Models/SelectListItem";
import { CreateOrEditTripObjectiveVm } from "../../Objective/Models/CreateOrEditTripObjectiveVm";
import { TripObjectiveVm } from "../../Objective/Models/TripObjectiveVm";
import { CreateOrEditTripVm } from "../Models/CreateOrEditTrip";
import { TripVm } from "../Models/TripVm";

const baseUrl = "api/trips";
const TripSerice = {
  async GetTrip(tripId: string): Promise<TripVm> {
    const response = await HttpClient.get<TripVm>(`${baseUrl}/${tripId}`);
    return response.data;
  },

  async AddTrip(values: CreateOrEditTripVm): Promise<TripVm> {
    const response = await HttpClient.post<TripVm>(`${baseUrl}`, values);
    return response.data;
  },

  async DeleteTrip(tripId: string): Promise<void> {
    await HttpClient.delete(`${baseUrl}/${tripId}`, {});
  },

  //#region Update

  async UpdateDate(tripId: string, date: Date): Promise<void> {
    await HttpClient.put(`${baseUrl}/${tripId}/date`, date);
  },

  async UpdateTripName(name: string, tripId: string): Promise<void> {
    await HttpClient.put(`${baseUrl}/${tripId}/name`, name, {
      headers: { "Content-Type": "application/json" },
    });
  },

  //#endregion

  //#region Objectives

  async GetObjectives(tripId: string): Promise<TripObjectiveVm[]> {
    const response = await HttpClient.get<TripObjectiveVm[]>(
      `${baseUrl}/${tripId}/objectives`
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

  //#endregion
};

export { TripSerice };
