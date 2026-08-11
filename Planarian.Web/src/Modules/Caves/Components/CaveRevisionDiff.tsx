import { ReactNode } from "react";
import { Card, Descriptions, Space, Tag, Typography } from "antd";
import {
  CaveEntranceSnapshotVm,
  CaveFileSnapshotVm,
  CaveRevisionDiffVm,
  CaveSnapshotVm,
  SnapshotTagReference,
} from "../Models/CaveRevisionVm";
import { ParagraphDisplayComponent } from "../../../Shared/Components/Display/ParagraphDisplayComponent";
import { CountyNumberIntent } from "../Models/CaveChangeRequestVm";

const labels: Record<string, string> = {
  Name: "Name", AlternateNames: "Alternative Names", "State.Id": "State", "County.Id": "County",
  CountyNumber: "County Number", ReportedByUserId: "Reported By", LengthFeet: "Length",
  DepthFeet: "Depth", MaxPitDepthFeet: "Max Pit Depth", NumberOfPits: "Number of Pits",
  Narrative: "Narrative", ReportedOn: "Reported On", IsArchived: "Archived",
};

const roleLabels: Record<string, string> = {
  Geology: "Geology", GeologicAge: "Geologic Age", MapStatus: "Map Status",
  PhysiographicProvince: "Physiographic Province", Archeology: "Archaeology", Biology: "Biology",
  CaveOther: "Other", Cartographer: "Cartographer", CaveReportedBy: "Reported By",
  EntranceStatus: "Entrance Status", EntranceHydrology: "Hydrology", FieldIndication: "Field Indication",
  EntranceReportedBy: "Reported By", EntranceOther: "Other",
};

const displayValue = (path: string, value: unknown): ReactNode => {
  if (value === null || value === undefined || value === "") return "—";
  if (typeof value === "boolean") return value ? "Yes" : "No";
  if (Array.isArray(value)) return value.join(", ") || "—";
  if (path.toLowerCase().includes("reportedon")) return new Date(String(value)).toLocaleDateString();
  if (path === "Narrative" || path === "Description") return <ParagraphDisplayComponent text={String(value)} />;
  return String(value);
};

const referenceLabel = (snapshot: CaveSnapshotVm | undefined, path: string) => {
  const reference = path === "State.Id" ? snapshot?.state : snapshot?.county;
  if (!reference) return undefined;
  const code = path === "State.Id" ? reference.abbreviationAtRevision : reference.displayIdAtRevision;
  return code ? `${reference.nameAtRevision} (${code})` : reference.nameAtRevision;
};

const tagKey = (tag: SnapshotTagReference) => `${tag.role}:${tag.tagTypeId}`;
const tagsByRole = (tags: SnapshotTagReference[]) => new Map(Object.entries(
  tags.reduce<Record<string, SnapshotTagReference[]>>((groups, tag) => {
    (groups[tag.role] ??= []).push(tag);
    return groups;
  }, {})
));

const changedValue = (label: string, previous: unknown, current: unknown, path = label) =>
  previous === current ? null : <Descriptions.Item label={label} key={label}>
  <div>{displayValue(path, current)}</div>
  <Typography.Text type="secondary" style={{ fontSize: 12 }}>Previous: {displayValue(path, previous)}</Typography.Text>
</Descriptions.Item>;

const EntranceChanges = ({ previous, current }: {
  previous: CaveEntranceSnapshotVm; current: CaveEntranceSnapshotVm;
}) => {
  const oldTags = new Map(previous.tags.map(tag => [tagKey(tag), tag]));
  const newTags = new Map(current.tags.map(tag => [tagKey(tag), tag]));
  const roles = new Set([...tagsByRole(previous.tags).keys(), ...tagsByRole(current.tags).keys()]);
  return <Card size="small" title={current.name || previous.name || "Unnamed entrance"}>
    <Descriptions bordered column={1} size="small">
      {changedValue("Name", previous.name, current.name)}
      {changedValue("Primary", previous.isPrimary, current.isPrimary)}
      {changedValue("Description", previous.description, current.description, "Description")}
      {changedValue("Reported By",
        previous.reportedByNameAtRevision ?? previous.reportedByUserId,
        current.reportedByNameAtRevision ?? current.reportedByUserId)}
      {changedValue("Latitude", previous.latitude, current.latitude)}
      {changedValue("Longitude", previous.longitude, current.longitude)}
      {changedValue("Elevation", previous.elevation, current.elevation)}
      {changedValue("Coordinate Reference System", previous.srid, current.srid)}
      {changedValue("Location Quality", previous.locationQualityNameAtRevision, current.locationQualityNameAtRevision)}
      {changedValue("Reported On", previous.reportedOn, current.reportedOn, "ReportedOn")}
      {changedValue("Pit Depth", previous.pitDepthFeet, current.pitDepthFeet)}
      {[...roles].sort().map(role => {
        const removed = previous.tags.filter(tag => tag.role === role && !newTags.has(tagKey(tag)));
        const added = current.tags.filter(tag => tag.role === role && !oldTags.has(tagKey(tag)));
        if (!removed.length && !added.length) return null;
        return <Descriptions.Item label={roleLabels[role] ?? role} key={role}>
          <Space wrap>
            {removed.map(tag => <Tag color="error" key={`removed-${tagKey(tag)}`}>Removed: {tag.nameAtRevision}</Tag>)}
            {added.map(tag => <Tag color="success" key={`added-${tagKey(tag)}`}>Added: {tag.nameAtRevision}</Tag>)}
          </Space>
        </Descriptions.Item>;
      })}
    </Descriptions>
  </Card>;
};

const FileChanges = ({ previous, current }: { previous: CaveFileSnapshotVm; current: CaveFileSnapshotVm }) =>
  <Card size="small" title={current.displayName || current.fileName}>
    <Descriptions bordered column={1} size="small">
      {changedValue("Display Name", previous.displayName, current.displayName)}
      {changedValue("Filename", previous.fileName, current.fileName)}
      {changedValue("File Type", previous.fileTypeNameAtRevision, current.fileTypeNameAtRevision)}
    </Descriptions>
  </Card>;

const referenceMetadataLabel = (path: string, property: string) => {
  if (path === "State") return "State label";
  if (path === "County") return "County label";
  if (path.includes("Entrances") && property.includes("LocationQuality")) return "Entrance Location Quality";
  if (path.includes("Files") && property.includes("FileType")) return "File Type";
  const role = Object.keys(roleLabels).find(candidate => path.includes(candidate));
  return role ? `${roleLabels[role]} label` : "Historical reference label";
};

export const CaveRevisionDiff = ({ diff, previous, current, countyNumberIntent }: {
  diff?: CaveRevisionDiffVm;
  previous?: CaveSnapshotVm;
  current?: CaveSnapshotVm;
  countyNumberIntent?: CountyNumberIntent;
}) => {
  if (!diff) return <Typography.Text type="secondary">Initial publication</Typography.Text>;

  const previousEntrances = new Map(previous?.entrances.map(item => [item.id, item]) ?? []);
  const currentEntrances = new Map(current?.entrances.map(item => [item.id, item]) ?? []);
  const previousFiles = new Map(previous?.files.map(item => [item.id, item]) ?? []);
  const currentFiles = new Map(current?.files.map(item => [item.id, item]) ?? []);
  const hasChanges = diff.scalars.length || diff.addedTags.length || diff.removedTags.length ||
    diff.addedEntrances.length || diff.removedEntrances.length || diff.changedEntrances.length ||
    diff.addedFiles.length || diff.removedFiles.length || diff.changedFiles.length || diff.referenceMetadataChanges.length;
  if (!hasChanges) return <Typography.Text type="secondary">No visible field changes</Typography.Text>;

  return <Space direction="vertical" style={{ width: "100%" }}>
    {!!diff.scalars.length && <Descriptions bordered column={1} size="small">
      {diff.scalars.map(change => {
        const isLocation = change.path === "State.Id" || change.path === "County.Id";
        const countyIntent = change.path === "CountyNumber" && countyNumberIntent !== undefined &&
          countyNumberIntent !== "Manual"
          ? countyNumberIntent === "FirstAvailable" ? "First available on approval" : "Auto-assigned on approval"
          : undefined;
        return <Descriptions.Item label={labels[change.path] ?? change.path} key={change.path}>
          <div>{isLocation ? referenceLabel(current, change.path) : countyIntent ?? displayValue(change.path, change.current)}</div>
          <Typography.Text type="secondary" style={{ fontSize: 12 }}>
            Previous: {isLocation ? referenceLabel(previous, change.path) : displayValue(change.path, change.previous)}
          </Typography.Text>
        </Descriptions.Item>;
      })}
    </Descriptions>}

    {(!!diff.addedTags.length || !!diff.removedTags.length) && <Space wrap>
      {diff.removedTags.map(tag => <Tag color="error" key={`removed-${tagKey(tag)}`}>Removed {roleLabels[tag.role] ?? tag.role}: {tag.nameAtRevision}</Tag>)}
      {diff.addedTags.map(tag => <Tag color="success" key={`added-${tagKey(tag)}`}>Added {roleLabels[tag.role] ?? tag.role}: {tag.nameAtRevision}</Tag>)}
    </Space>}

    {(!!diff.addedEntrances.length || !!diff.removedEntrances.length) && <Space wrap>
      {diff.removedEntrances.map(id => <Tag color="error" key={`removed-${id}`}>Removed entrance: {previousEntrances.get(id)?.name || "Unnamed entrance"}</Tag>)}
      {diff.addedEntrances.map(id => <Tag color="success" key={`added-${id}`}>Added entrance: {currentEntrances.get(id)?.name || "Unnamed entrance"}</Tag>)}
    </Space>}
    {diff.changedEntrances.map(id => {
      const oldEntrance = previousEntrances.get(id); const newEntrance = currentEntrances.get(id);
      return oldEntrance && newEntrance ? <EntranceChanges key={id} previous={oldEntrance} current={newEntrance} /> : null;
    })}

    {(!!diff.addedFiles.length || !!diff.removedFiles.length) && <Space wrap>
      {diff.removedFiles.map(id => <Tag color="error" key={`removed-${id}`}>Removed file: {previousFiles.get(id)?.displayName || previousFiles.get(id)?.fileName || "File"}</Tag>)}
      {diff.addedFiles.map(id => <Tag color="success" key={`added-${id}`}>Added file: {currentFiles.get(id)?.displayName || currentFiles.get(id)?.fileName || "File"}</Tag>)}
    </Space>}
    {diff.changedFiles.map(id => {
      const oldFile = previousFiles.get(id); const newFile = currentFiles.get(id);
      return oldFile && newFile ? <FileChanges key={id} previous={oldFile} current={newFile} /> : null;
    })}

    {diff.referenceMetadataChanges.map(change => <Typography.Text key={`${change.path}-${change.property}`}>
      {referenceMetadataLabel(change.path, change.property)} updated: {change.currentValue ?? "—"}
      <Typography.Text type="secondary"> (previously {change.previousValue ?? "—"})</Typography.Text>
    </Typography.Text>)}
  </Space>;
};
