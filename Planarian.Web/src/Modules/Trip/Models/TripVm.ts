import { LeadVm } from "../../Lead/Models/LeadVm";

export interface TripVm {
  id: string;
  projectId: string;
  tripTagTypeIds: string[];
  tripMemberIds: string[];
  name: string;
  description: string;
  tripReport: string | null;
  isTripReportCompleted: boolean;
  numberOfPhotos: number;
  modifiedOn: string | null;
  createdOn: string;
  leads: LeadVm[];
}
