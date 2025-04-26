import { AddCaveVm } from "./AddCaveVm";

export interface ProposeChangeRequestVm {
  id?: string | null | undefined;
  cave: AddCaveVm;
}

export interface ChangesForReviewVm {
  id: string;
  caveName: string;
  caveDisplayId: string | null;
  countyId: string;
  isNew: boolean;
  submittedOn: string;
  submittedByUserId: string;
}
