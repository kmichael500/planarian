import { CaveProposalCountyNumberChangeVm, CountyNumberIntent } from "../Models/CaveChangeRequestVm";
import {
  CaveEntranceSnapshotVm,
  CaveFileSnapshotVm,
  CaveRevisionDiffVm,
  CaveScalarChangeVm,
  CaveSnapshotVm,
  SnapshotTagReference,
} from "../Models/CaveRevisionVm";

export type DiffValueFormat = "text" | "distance" | "number" | "date" | "boolean" | "coordinates" | "reference";

export interface ReferenceMetadataPresentation {
  path: string;
  property: string;
  previousLabel?: string;
  currentLabel?: string;
}

export interface TagChangeGroup {
  role: string;
  label: string;
  removed: SnapshotTagReference[];
  added: SnapshotTagReference[];
  metadata: ReferenceMetadataPresentation[];
}

export interface ChangedFieldPresentation {
  key: string;
  label: string;
  previous: unknown;
  current: unknown;
  format: DiffValueFormat;
  metadata: ReferenceMetadataPresentation[];
  textDiff?: boolean;
}

export interface CaveInformationFieldPresentation {
  key: string;
  label: string;
  change?: ChangedFieldPresentation;
  tags?: TagChangeGroup;
  metadata: ReferenceMetadataPresentation[];
}

export interface AffectedEntrancePresentation {
  id: string;
  status: "added" | "removed" | "changed";
  heading: string;
  snapshot?: CaveEntranceSnapshotVm;
  fields: ChangedFieldPresentation[];
  tagGroups: TagChangeGroup[];
  metadata: ReferenceMetadataPresentation[];
  detailsAvailable: boolean;
}

export interface AffectedFilePresentation {
  id: string;
  status: "added" | "removed" | "changed";
  heading: string;
  snapshot?: CaveFileSnapshotVm;
  fields: ChangedFieldPresentation[];
  metadata: ReferenceMetadataPresentation[];
  detailsAvailable: boolean;
}

export interface CaveRevisionDiffPresentation {
  caveInformation: CaveInformationFieldPresentation[];
  entrances: AffectedEntrancePresentation[];
  narrative?: ChangedFieldPresentation;
  files: AffectedFilePresentation[];
  fallbackScalars: ChangedFieldPresentation[];
  fallbackMetadata: ReferenceMetadataPresentation[];
}

const caveScalarDefinitions: Record<string, { label: string; format: DiffValueFormat }> = {
  Name: { label: "Name", format: "text" },
  AlternateNames: { label: "Alternative Names", format: "text" },
  "State.Id": { label: "State", format: "reference" },
  "County.Id": { label: "County", format: "reference" },
  CountyNumber: { label: "County Number", format: "number" },
  LengthFeet: { label: "Length", format: "distance" },
  DepthFeet: { label: "Depth", format: "distance" },
  MaxPitDepthFeet: { label: "Max Pit Depth", format: "distance" },
  NumberOfPits: { label: "Number of Pits", format: "number" },
  ReportedOn: { label: "Reported On", format: "date" },
  ReportedByUserId: { label: "Reported By", format: "text" },
  IsArchived: { label: "Archived", format: "boolean" },
};

export const roleLabels: Record<string, string> = {
  Geology: "Geology",
  GeologicAge: "Geologic Age",
  PhysiographicProvince: "Physiographic Province",
  Biology: "Biology",
  Archeology: "Archeology",
  MapStatus: "Map Status",
  Cartographer: "Cartographers",
  CaveReportedBy: "Reported By",
  CaveOther: "Other",
  EntranceStatus: "Status",
  FieldIndication: "Field Indication",
  EntranceHydrology: "Hydrology",
  EntranceReportedBy: "Reported By",
  EntranceOther: "Other",
};

const caveOrder = [
  "Name", "AlternateNames", "State.Id", "County.Id", "CountyNumber", "LengthFeet", "DepthFeet",
  "MaxPitDepthFeet", "NumberOfPits", "ReportedOn", "ReportedByUserId", "CaveReportedBy", "Geology", "GeologicAge",
  "PhysiographicProvince", "Biology", "Archeology", "MapStatus", "Cartographer",
  "CaveOther", "IsArchived",
];

const entranceDefinitions: Record<string, { label: string; format: DiffValueFormat; textDiff?: boolean }> = {
  Coordinates: { label: "Coordinates", format: "coordinates" },
  Description: { label: "Description", format: "text", textDiff: true },
  Elevation: { label: "Elevation", format: "distance" },
  LocationQualityTagId: { label: "Location Quality", format: "reference" },
  Name: { label: "Name", format: "text" },
  IsPrimary: { label: "Primary", format: "boolean" },
  ReportedOn: { label: "Reported On", format: "date" },
  ReportedByUserId: { label: "Reported By", format: "text" },
  PitDepthFeet: { label: "Pit Depth", format: "distance" },
  Srid: { label: "Coordinate Reference System", format: "number" },
};

const entranceOrder = [
  "Coordinates", "Srid", "Description", "Elevation", "LocationQualityTagId", "Name", "IsPrimary", "ReportedOn",
  "ReportedByUserId", "PitDepthFeet", "EntranceStatus", "FieldIndication", "EntranceHydrology",
  "EntranceReportedBy", "EntranceOther",
];

const fileDefinitions: Record<string, { label: string; format: DiffValueFormat }> = {
  DisplayName: { label: "Display Name", format: "text" },
  FileName: { label: "Filename", format: "text" },
  FileTypeTagId: { label: "File Type", format: "reference" },
};
const fileOrder = ["DisplayName", "FileName", "FileTypeTagId"];

const compareOrder = (order: string[]) => (a: { key: string }, b: { key: string }) => {
  const ai = order.indexOf(a.key);
  const bi = order.indexOf(b.key);
  return (ai < 0 ? Number.MAX_SAFE_INTEGER : ai) - (bi < 0 ? Number.MAX_SAFE_INTEGER : bi) || a.key.localeCompare(b.key);
};

const safeLabel = (path: string) => {
  const segment = path.split(/[./]/).filter(Boolean).pop() ?? path;
  return segment.replace(/([a-z0-9])([A-Z])/g, "$1 $2").replace(/[_-]+/g, " ") || "Unknown change";
};

const metadataPresentation = (change: CaveRevisionDiffVm["referenceMetadataChanges"][number]): ReferenceMetadataPresentation => ({
  path: change.path,
  property: change.property,
  previousLabel: change.previousValue,
  currentLabel: change.currentValue,
});

type ParsedMetadata =
  | { kind: "caveField"; key: string }
  | { kind: "entranceField"; id: string; key: string }
  | { kind: "fileField"; id: string; key: string }
  | { kind: "unknown" };

export const parseReferenceMetadataPath = (path: string): ParsedMetadata => {
  if (path === "State") return { kind: "caveField", key: "State.Id" };
  if (path === "County") return { kind: "caveField", key: "County.Id" };
  let match = /^Tags\/([^/]+)$/.exec(path);
  if (match) return { kind: "caveField", key: match[1] };
  match = /^Entrances\/([^/]+)\/LocationQuality$/.exec(path);
  if (match) return { kind: "entranceField", id: match[1], key: "LocationQualityTagId" };
  match = /^Entrances\/([^/]+)\/Tags\/([^/]+)$/.exec(path);
  if (match) return { kind: "entranceField", id: match[1], key: match[2] };
  match = /^Files\/([^/]+)\/FileType$/.exec(path);
  if (match) return { kind: "fileField", id: match[1], key: "FileTypeTagId" };
  return { kind: "unknown" };
};

const referenceLabel = (snapshot: CaveSnapshotVm | undefined, path: string) => {
  const reference = path === "State.Id" ? snapshot?.state : snapshot?.county;
  if (!reference) return undefined;
  const code = path === "State.Id" ? reference.abbreviationAtRevision : reference.displayIdAtRevision;
  return code ? `${reference.nameAtRevision} (${code})` : reference.nameAtRevision;
};

const makeTagGroup = (role: string, added: SnapshotTagReference[], removed: SnapshotTagReference[],
  metadata: ReferenceMetadataPresentation[]): TagChangeGroup => ({
    role,
    label: roleLabels[role] ?? safeLabel(role),
    removed: removed.filter(tag => tag.role === role).sort((a, b) => a.nameAtRevision.localeCompare(b.nameAtRevision) || a.tagTypeId.localeCompare(b.tagTypeId)),
    added: added.filter(tag => tag.role === role).sort((a, b) => a.nameAtRevision.localeCompare(b.nameAtRevision) || a.tagTypeId.localeCompare(b.tagTypeId)),
    metadata,
  });

const scalarField = (change: CaveScalarChangeVm, definition: { label: string; format: DiffValueFormat; textDiff?: boolean },
  metadata: ReferenceMetadataPresentation[] = []): ChangedFieldPresentation => ({
    key: change.path, label: definition.label, previous: change.previous, current: change.current,
    format: definition.format, textDiff: definition.textDiff, metadata,
  });

export const buildCaveRevisionDiffPresentation = (
  diff: CaveRevisionDiffVm,
  previous?: CaveSnapshotVm,
  current?: CaveSnapshotVm,
  countyNumberIntent?: CountyNumberIntent,
  proposalCountyNumberChange?: CaveProposalCountyNumberChangeVm,
): CaveRevisionDiffPresentation => {
  const caveMetadata = new Map<string, ReferenceMetadataPresentation[]>();
  const entranceMetadata = new Map<string, Map<string, ReferenceMetadataPresentation[]>>();
  const fileMetadata = new Map<string, Map<string, ReferenceMetadataPresentation[]>>();
  const fallbackMetadata: ReferenceMetadataPresentation[] = [];

  diff.referenceMetadataChanges.forEach(change => {
    const parsed = parseReferenceMetadataPath(change.path);
    const value = metadataPresentation(change);
    if (parsed.kind === "caveField") (caveMetadata.get(parsed.key) ?? caveMetadata.set(parsed.key, []).get(parsed.key)!).push(value);
    else if (parsed.kind === "entranceField") {
      const fields = entranceMetadata.get(parsed.id) ?? new Map<string, ReferenceMetadataPresentation[]>();
      entranceMetadata.set(parsed.id, fields);
      (fields.get(parsed.key) ?? fields.set(parsed.key, []).get(parsed.key)!).push(value);
    } else if (parsed.kind === "fileField") {
      const fields = fileMetadata.get(parsed.id) ?? new Map<string, ReferenceMetadataPresentation[]>();
      fileMetadata.set(parsed.id, fields);
      (fields.get(parsed.key) ?? fields.set(parsed.key, []).get(parsed.key)!).push(value);
    } else fallbackMetadata.push(value);
  });

  let narrative: ChangedFieldPresentation | undefined;
  const fallbackScalars: ChangedFieldPresentation[] = [];
  const caveFields = new Map<string, CaveInformationFieldPresentation>();
  diff.scalars.forEach(change => {
    if (change.path === "CountyNumber" && proposalCountyNumberChange) return;
    if (change.path === "Narrative") {
      narrative = { ...scalarField(change, { label: "Narrative", format: "text", textDiff: true }) };
      return;
    }
    const definition = caveScalarDefinitions[change.path];
    if (!definition) {
      fallbackScalars.push(scalarField(change, { label: safeLabel(change.path), format: "text" }));
      return;
    }
    let previousValue = change.previous;
    let currentValue = change.current;
    if (change.path === "State.Id" || change.path === "County.Id") {
      previousValue = referenceLabel(previous, change.path);
      currentValue = referenceLabel(current, change.path);
    }
    if (change.path === "CountyNumber" && countyNumberIntent && countyNumberIntent !== "Manual")
      currentValue = countyNumberIntent === "FirstAvailable" ? "First available on approval" : "Auto-assigned on approval";
    const metadata = caveMetadata.get(change.path) ?? [];
    caveFields.set(change.path, { key: change.path, label: definition.label,
      change: { ...scalarField(change, definition, metadata), previous: previousValue, current: currentValue }, metadata });
  });
  if (proposalCountyNumberChange) {
    const proposalValue = (intent: CountyNumberIntent, requested?: number) => {
      if (intent === "FirstAvailable") return "First available on approval";
      if (intent === "AutomaticNext") return "Auto-assigned on approval";
      return requested;
    };
    const metadata = caveMetadata.get("CountyNumber") ?? [];
    caveFields.set("CountyNumber", {
      key: "CountyNumber", label: "County Number", metadata,
      change: {
        key: "CountyNumber", label: "County Number", format: "number", metadata,
        previous: proposalValue(proposalCountyNumberChange.previousIntent,
          proposalCountyNumberChange.previousRequestedCountyNumber),
        current: proposalValue(proposalCountyNumberChange.currentIntent,
          proposalCountyNumberChange.currentRequestedCountyNumber),
      },
    });
  }

  const caveRoles = new Set([...diff.addedTags.map(tag => tag.role), ...diff.removedTags.map(tag => tag.role)]);
  caveMetadata.forEach((_changes, key) => { if (!caveScalarDefinitions[key]) caveRoles.add(key); });
  caveRoles.forEach(role => {
    const metadata = caveMetadata.get(role) ?? [];
    const group = makeTagGroup(role, diff.addedTags, diff.removedTags, metadata);
    caveFields.set(role, { key: role, label: group.label, tags: group, metadata });
  });
  caveMetadata.forEach((metadata, key) => {
    if (caveFields.has(key)) return;
    const definition = caveScalarDefinitions[key];
    if (definition) caveFields.set(key, { key, label: definition.label, metadata });
  });

  const previousEntrances = new Map(previous?.entrances.map(item => [item.id, item]) ?? []);
  const currentEntrances = new Map(current?.entrances.map(item => [item.id, item]) ?? []);
  const detailByEntrance = new Map(diff.entranceChanges.map(change => [change.entranceId, change]));
  const entranceIds = [
    ...diff.removedEntrances.map(id => ({ id, status: "removed" as const })),
    ...diff.addedEntrances.map(id => ({ id, status: "added" as const })),
    ...diff.changedEntrances.map(id => ({ id, status: "changed" as const })),
  ];
  const entrances = entranceIds.map(({ id, status }): AffectedEntrancePresentation => {
    const oldEntrance = previousEntrances.get(id);
    const newEntrance = currentEntrances.get(id);
    const snapshot = status === "removed" ? oldEntrance : newEntrance;
    const detail = detailByEntrance.get(id);
    const metadataByField = entranceMetadata.get(id) ?? new Map();
    const fields: ChangedFieldPresentation[] = [];
    if (status === "changed" && detail) {
      const scalarByPath = new Map(detail.scalars.map(change => [change.path, change]));
      if (scalarByPath.has("Latitude") || scalarByPath.has("Longitude")) {
        fields.push({ key: "Coordinates", label: "Coordinates", format: "coordinates",
          previous: [oldEntrance?.latitude, oldEntrance?.longitude], current: [newEntrance?.latitude, newEntrance?.longitude], metadata: [] });
      }
      detail.scalars.forEach(change => {
        if (change.path === "Latitude" || change.path === "Longitude") return;
        const definition = entranceDefinitions[change.path];
        if (!definition) {
          fields.push(scalarField(change, { label: safeLabel(change.path), format: "text" }));
          return;
        }
        let previousValue = change.previous;
        let currentValue = change.current;
        if (change.path === "LocationQualityTagId") {
          previousValue = oldEntrance?.locationQualityNameAtRevision;
          currentValue = newEntrance?.locationQualityNameAtRevision;
        } else if (change.path === "ReportedByUserId") {
          previousValue = oldEntrance?.reportedByNameAtRevision ?? change.previous;
          currentValue = newEntrance?.reportedByNameAtRevision ?? change.current;
        }
        fields.push({ ...scalarField(change, definition, metadataByField.get(change.path) ?? []), previous: previousValue, current: currentValue });
      });
    }
    metadataByField.forEach((metadata, key) => {
      if (roleLabels[key] || fields.some(field => field.key === key)) return;
      const definition = entranceDefinitions[key];
      if (definition) fields.push({ key, label: definition.label, previous: undefined, current: undefined,
        format: definition.format, metadata });
    });
    const roles = new Set([...(detail?.addedTags ?? []).map(tag => tag.role), ...(detail?.removedTags ?? []).map(tag => tag.role)]);
    metadataByField.forEach((_metadata, key) => { if (!entranceDefinitions[key]) roles.add(key); });
    const tagGroups = [...roles].map(role => makeTagGroup(role, detail?.addedTags ?? [], detail?.removedTags ?? [], metadataByField.get(role) ?? []))
      .sort((a, b) => compareOrder(entranceOrder)({ key: a.role }, { key: b.role }));
    return {
      id, status, snapshot, fields: fields.sort(compareOrder(entranceOrder)), tagGroups,
      metadata: [], detailsAvailable: status === "changed" ? !!detail && !!oldEntrance && !!newEntrance : !!snapshot,
      heading: snapshot?.name || oldEntrance?.name || newEntrance?.name || "Unnamed entrance",
    };
  });

  const previousFiles = new Map(previous?.files.map(item => [item.id, item]) ?? []);
  const currentFiles = new Map(current?.files.map(item => [item.id, item]) ?? []);
  const detailByFile = new Map(diff.fileChanges.map(change => [change.fileId, change]));
  const fileIds = [
    ...diff.removedFiles.map(id => ({ id, status: "removed" as const })),
    ...diff.addedFiles.map(id => ({ id, status: "added" as const })),
    ...diff.changedFiles.map(id => ({ id, status: "changed" as const })),
  ];
  const files = fileIds.map(({ id, status }): AffectedFilePresentation => {
    const oldFile = previousFiles.get(id);
    const newFile = currentFiles.get(id);
    const snapshot = status === "removed" ? oldFile : newFile;
    const detail = detailByFile.get(id);
    const metadataByField = fileMetadata.get(id) ?? new Map();
    const fields = status === "changed" && detail ? detail.scalars.map(change => {
      const definition = fileDefinitions[change.path] ?? { label: safeLabel(change.path), format: "text" as const };
      let previousValue = change.previous;
      let currentValue = change.current;
      if (change.path === "FileTypeTagId") {
        previousValue = oldFile?.fileTypeNameAtRevision;
        currentValue = newFile?.fileTypeNameAtRevision;
      }
      return { ...scalarField(change, definition, metadataByField.get(change.path) ?? []), previous: previousValue, current: currentValue };
    }) : [];
    metadataByField.forEach((metadata, key) => {
      if (!fields.some(field => field.key === key) && fileDefinitions[key]) fields.push({
        key, label: fileDefinitions[key].label, previous: undefined, current: undefined,
        format: fileDefinitions[key].format, metadata,
      });
    });
    return {
      id, status, snapshot, fields: fields.sort(compareOrder(fileOrder)), metadata: [],
      detailsAvailable: status === "changed" ? !!detail && !!oldFile && !!newFile : !!snapshot,
      heading: snapshot?.displayName || snapshot?.fileName || oldFile?.displayName || oldFile?.fileName || "File",
    };
  });

  return {
    caveInformation: [...caveFields.values()].sort(compareOrder(caveOrder)),
    entrances,
    narrative,
    files,
    fallbackScalars: fallbackScalars.sort((a, b) => a.key.localeCompare(b.key)),
    fallbackMetadata: fallbackMetadata.sort((a, b) => a.path.localeCompare(b.path) || a.property.localeCompare(b.property)),
  };
};
