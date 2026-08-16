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

export interface SnapshotReferenceVm {
  id: string;
  nameAtRevision: string;
  displayIdAtRevision?: string;
  abbreviationAtRevision?: string;
}

export interface CaveEntranceSnapshotVm {
  id: string;
  name?: string;
  isPrimary: boolean;
  description?: string;
  latitude?: number;
  longitude?: number;
  elevation?: number;
  srid: number;
  locationQualityTagId: string;
  locationQualityNameAtRevision: string;
  reportedOn?: string;
  pitDepthFeet?: number;
  tags: SnapshotTagReference[];
}

export interface CaveLinePlotSnapshotVm {
  id: string;
  name: string;
  contentHash: string;
}

export interface CaveFileSnapshotVm {
  id: string;
  fileTypeTagId: string;
  fileTypeNameAtRevision: string;
  fileName: string;
  displayName?: string;
}

export interface CaveSnapshotVm {
  caveId: string;
  accountId: string;
  name: string;
  alternateNames: string[];
  state: SnapshotReferenceVm;
  county: SnapshotReferenceVm;
  countyNumber: number;
  lengthFeet?: number | null;
  depthFeet?: number | null;
  maxPitDepthFeet?: number | null;
  numberOfPits?: number | null;
  narrative?: string;
  reportedOn?: string;
  isArchived: boolean;
  tags: SnapshotTagReference[];
  entrances: CaveEntranceSnapshotVm[];
  files: CaveFileSnapshotVm[];
  linePlots: CaveLinePlotSnapshotVm[];
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
  addedLinePlots: string[];
  removedLinePlots: string[];
  changedLinePlots: string[];
  entranceChanges: Array<{
    entranceId: string;
    scalars: CaveScalarChangeVm[];
    addedTags: SnapshotTagReference[];
    removedTags: SnapshotTagReference[];
  }>;
  fileChanges: Array<{
    fileId: string;
    scalars: CaveScalarChangeVm[];
  }>;
  linePlotChanges: Array<{
    linePlotId: string;
    scalars: CaveScalarChangeVm[];
  }>;
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
  previous?: CaveSnapshotVm;
  current: CaveSnapshotVm;
  diff?: CaveRevisionDiffVm;
}
