import { LeadClassification } from "./LeadClassification";

export interface LeadVm {
  id: string;
  description: string;
  classification: LeadClassification;
  closestStation: string;
}
