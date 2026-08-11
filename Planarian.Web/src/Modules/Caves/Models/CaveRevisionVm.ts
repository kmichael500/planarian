export interface CaveRevisionListItemVm {
  id: string;
  caveId: string;
  sequence: number;
  publishedOn: string;
  actorUserId?: string;
  actorName?: string;
  source: "SystemBaseline" | "ManagerEdit" | "UserSubmission" | "Import" | "System";
  operation: "Create" | "Update" | "Archive" | "Unarchive" | "Delete";
  changeRequestId?: string;
  importBatchId?: string;
  isCurrent: boolean;
}

export interface CaveRevisionHistoryVm {
  caveId: string;
  currentRevisionId?: string;
  revisions: CaveRevisionListItemVm[];
}

export interface SnapshotTagReference {
  role: string;
  tagTypeId: string;
  nameAtRevision: string;
}

export interface CaveScalarChangeVm {
  path: string;
  previous: unknown;
  current: unknown;
}

export interface CaveRevisionDiffVm {
  scalars: CaveScalarChangeVm[];
  addedTags: SnapshotTagReference[];
  removedTags: SnapshotTagReference[];
  addedEntrances: string[];
  removedEntrances: string[];
  changedEntrances: string[];
  addedFiles: string[];
  removedFiles: string[];
  changedFiles: string[];
  referenceMetadataChanges: Array<{
    path: string;
    stableId: string;
    property: string;
    previousValue?: string;
    currentValue?: string;
  }>;
}

export interface CaveRevisionComparisonVm {
  revision: CaveRevisionListItemVm;
  previousRevision?: CaveRevisionListItemVm;
  previous?: Record<string, unknown>;
  current: Record<string, unknown>;
  diff?: CaveRevisionDiffVm;
}
