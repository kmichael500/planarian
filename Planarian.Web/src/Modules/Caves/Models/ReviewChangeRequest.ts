import { AddCaveVm } from "./AddCaveVm";

export interface ReviewChangeRequest {
  id: string | null;
  approve: boolean;
  cave: AddCaveVm;
  notes: string | null;
}
