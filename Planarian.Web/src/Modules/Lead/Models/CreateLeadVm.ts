import { LeadClassification } from "./LeadClassification";

export interface CreateLeadVm {
  description: string;
  classification: LeadClassification;
  closestStation: string;
}
