import { GeoJsonUploadVm } from "./GeoJsonUploadVm";
import { CaveRevisionDiffVm, CaveSnapshotVm } from "./CaveRevisionVm";

export type CountyNumberIntent = "AutomaticNext" | "FirstAvailable" | "Manual";

export type CaveChangeRequestStatus = "Pending" | "ChangesRequested" | "Approved" | "Rejected";
export type CaveChangeRequestSortBy = "UpdatedOn" | "SubmittedOn" | "CaveName";

export interface DiffEntitySummaryVm {
  added: number;
  removed: number;
  changed: number;
  total: number;
}

export interface CaveChangeScopeSummaryVm {
  caveFields: number;
  entrances: DiffEntitySummaryVm;
  files: DiffEntitySummaryVm;
  linePlots: DiffEntitySummaryVm;
  otherChanges: number;
}

export interface CaveChangeRequestSummaryVm {
  id: string;
  caveId: string;
  caveName: string;
  caveExists: boolean;
  status: CaveChangeRequestStatus;
  submitterUserId: string;
  submitterName?: string;
  submittedOn: string;
  updatedOn?: string;
  reviewedOn?: string;
  reviewerUserId?: string;
  reviewerName?: string;
  reviewerNotes?: string;
  originalBaseRevisionId: string;
  proposalBaseRevisionId: string;
  currentRevisionId?: string;
  currentProposalVersionId: string;
  isStale: boolean;
  approvedRevisionId?: string;
  canEdit: boolean;
  canReview: boolean;
  changeSummary: CaveChangeScopeSummaryVm;
}

export interface CaveProposalVersionVm {
  id: string;
  previousProposalVersionId?: string;
  baseRevisionId: string;
  createdByUserId?: string;
  createdByName?: string;
  createdOn: string;
  isCurrent: boolean;
}

export interface CaveProposalAuthoringContextVm {
  cave: import("./CaveVm").CaveVm;
  expectedBaseRevisionId: string;
  linePlots: GeoJsonUploadVm[];
}

export interface CaveEditAuthoringContextVm {
  cave: import("./CaveVm").CaveVm;
  linePlots: GeoJsonUploadVm[];
}

export interface CaveProposalVersionDetailVm {
  base: CaveSnapshotVm;
  proposed: CaveSnapshotVm;
  diffFromBase: CaveRevisionDiffVm;
  previousProposed?: CaveSnapshotVm;
  diffFromPreviousVersion?: CaveRevisionDiffVm;
  countyNumberChange?: CaveProposalCountyNumberChangeVm;
  baseRevisionChanged: boolean;
  previousBaseRevisionId?: string;
  baseRevisionId: string;
  countyNumberIntent: CountyNumberIntent;
  requestedCountyNumber?: number;
  likelyCountyNumber?: number;
  linePlots: GeoJsonUploadVm[];
  unavailableStagedFileIds: string[];
}

export interface CaveProposalCountyNumberChangeVm {
  previous: CaveProposalCountyNumberStateVm;
  current: CaveProposalCountyNumberStateVm;
}

export interface CaveProposalCountyNumberStateVm {
  mode: "PreserveExisting" | "Manual" | "FirstAvailable" | "AutomaticNext";
  number?: number;
}

export interface CaveChangeRequestDetailVm {
  request: CaveChangeRequestSummaryVm;
  base: CaveSnapshotVm;
  proposed: CaveSnapshotVm;
  current: CaveSnapshotVm;
  diff: CaveRevisionDiffVm;
  publishedSinceBase?: CaveRevisionDiffVm;
  versions: CaveProposalVersionVm[];
  countyNumberIntent: CountyNumberIntent;
  requestedCountyNumber?: number;
  likelyCountyNumber?: number;
  linePlots?: GeoJsonUploadVm[];
  removedLinePlots?: GeoJsonUploadVm[];
  activeStagedFiles: CaveSnapshotVm["files"];
  unavailableStagedFileIds: string[];
  reviewEvents?: CaveChangeRequestReviewEventVm[];
}

export type CaveChangeRequestReviewAction = "ChangesRequested" | "Resubmitted" | "Approved" | "Rejected";

export interface CaveChangeRequestReviewEventVm {
  id: string;
  proposalVersionId: string;
  action: CaveChangeRequestReviewAction;
  actorUserId: string;
  actorName?: string;
  message?: string;
  createdOn: string;
}

export interface CaveChangeRequestDecisionVm {
  result: "ChangesRequested" | "Resubmitted" | "Approved" | "Rejected" | "Conflict";
  requestId: string;
  publishedRevisionId?: string;
  currentRevisionId?: string;
}

export interface CaveChangePreviewVm {
  base: CaveSnapshotVm;
  proposed: CaveSnapshotVm;
  diff: CaveRevisionDiffVm;
  countyNumberIntent: CountyNumberIntent;
  requestedCountyNumber?: number;
  likelyCountyNumber?: number;
  linePlots?: GeoJsonUploadVm[];
  removedLinePlots?: GeoJsonUploadVm[];
  hasMeaningfulChanges: boolean;
}
