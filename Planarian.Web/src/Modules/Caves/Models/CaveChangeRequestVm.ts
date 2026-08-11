import { CaveRevisionDiffVm, CaveSnapshotVm } from "./CaveRevisionVm";

export type CountyNumberIntent = "AutomaticNext" | "FirstAvailable" | "Manual";

export type CaveChangeRequestStatus = "Pending" | "Approved" | "Rejected";

export interface CaveChangeRequestSummaryVm {
  id: string;
  caveId: string;
  caveName: string;
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
  activeStagedFiles: CaveSnapshotVm["files"];
}

export interface CaveChangeRequestDecisionVm {
  result: "Approved" | "Rejected" | "Conflict";
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
}

export interface CaveProposalAuthoringConflictVm {
  conflictKind: "PublishedCaveChanged" | "ActiveProposalVersionChanged";
  expectedId: string;
  actualId?: string;
}
