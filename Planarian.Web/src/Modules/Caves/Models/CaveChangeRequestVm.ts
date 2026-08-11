import { CaveRevisionDiffVm } from "./CaveRevisionVm";

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
  baseRevisionId: string;
  currentRevisionId?: string;
  currentProposalVersionId: string;
  isStale: boolean;
  approvedRevisionId?: string;
  canEdit: boolean;
}

export interface CaveProposalVersionVm {
  id: string;
  previousProposalVersionId?: string;
  createdByUserId?: string;
  createdByName?: string;
  createdOn: string;
  isCurrent: boolean;
}

export interface CaveChangeRequestDetailVm {
  request: CaveChangeRequestSummaryVm;
  base: Record<string, unknown>;
  proposed: Record<string, unknown>;
  current: Record<string, unknown>;
  diff: CaveRevisionDiffVm;
  publishedSinceBase?: CaveRevisionDiffVm;
  versions: CaveProposalVersionVm[];
}

export interface CaveChangeRequestDecisionVm {
  result: "Approved" | "Rejected" | "Conflict";
  requestId: string;
  publishedRevisionId?: string;
  currentRevisionId?: string;
}
