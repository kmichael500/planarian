import { Descriptions, Space, Tag, Typography } from "antd";
import { CaveRevisionDiffVm } from "../Models/CaveRevisionVm";
import { ParagraphDisplayComponent } from "../../../Shared/Components/Display/ParagraphDisplayComponent";

const labels: Record<string, string> = {
  Name: "Name",
  AlternateNames: "Alternative Names",
  "State.Id": "State",
  "County.Id": "County",
  CountyNumber: "County Number",
  LengthFeet: "Length",
  DepthFeet: "Depth",
  MaxPitDepthFeet: "Max Pit Depth",
  NumberOfPits: "Number of Pits",
  Narrative: "Narrative",
  ReportedOn: "Reported On",
  IsArchived: "Archived",
};

const displayValue = (path: string, value: unknown) => {
  if (value === null || value === undefined || value === "") return "—";
  if (typeof value === "boolean") return value ? "Yes" : "No";
  if (Array.isArray(value)) return value.join(", ") || "—";
  if (path === "ReportedOn") return new Date(String(value)).toLocaleDateString();
  if (path === "Narrative") return <ParagraphDisplayComponent text={String(value)} />;
  return String(value);
};

type SnapshotEntity = {
  id: string;
  name?: string;
  fileName?: string;
  displayName?: string;
  isPrimary?: boolean;
  latitude?: number;
  longitude?: number;
  elevation?: number;
};

const entities = (snapshot: Record<string, unknown> | undefined, key: "entrances" | "files") =>
  (snapshot?.[key] as SnapshotEntity[] | undefined) ?? [];

const entityLabel = (entity: SnapshotEntity | undefined, fallback: string) =>
  entity?.displayName || entity?.fileName || entity?.name || fallback;

const entranceSummary = (entity: SnapshotEntity | undefined) => {
  if (!entity) return undefined;
  const coordinates = entity.latitude !== undefined && entity.longitude !== undefined
    ? `${entity.latitude.toFixed(5)}, ${entity.longitude.toFixed(5)}` : undefined;
  return [entity.name || "Unnamed entrance", entity.isPrimary ? "Primary" : undefined,
    coordinates, entity.elevation !== undefined ? `${entity.elevation} ft elevation` : undefined]
    .filter(Boolean).join(" · ");
};

export const CaveRevisionDiff = ({ diff, previous, current }: {
  diff?: CaveRevisionDiffVm;
  previous?: Record<string, unknown>;
  current?: Record<string, unknown>;
}) => {
  if (!diff) return <Typography.Text type="secondary">Initial publication</Typography.Text>;

  const collectionChanges = [
    ...diff.addedTags.map((tag) => ({ label: "Tag", value: tag.nameAtRevision, added: true })),
    ...diff.removedTags.map((tag) => ({ label: "Tag", value: tag.nameAtRevision, added: false })),
    ...diff.addedEntrances.map((id) => ({ label: "Entrance", value: entityLabel(entities(current, "entrances").find(item => item.id === id), id), added: true })),
    ...diff.removedEntrances.map((id) => ({ label: "Entrance", value: entityLabel(entities(previous, "entrances").find(item => item.id === id), id), added: false })),
    ...diff.addedFiles.map((id) => ({ label: "File", value: entityLabel(entities(current, "files").find(item => item.id === id), id), added: true })),
    ...diff.removedFiles.map((id) => ({ label: "File", value: entityLabel(entities(previous, "files").find(item => item.id === id), id), added: false })),
  ];

  const changedEntrances = diff.changedEntrances.map((id) => ({
    id,
    previous: entities(previous, "entrances").find(item => item.id === id),
    current: entities(current, "entrances").find(item => item.id === id),
  }));
  const changedFiles = diff.changedFiles.map((id) => ({
    id,
    previous: entities(previous, "files").find(item => item.id === id),
    current: entities(current, "files").find(item => item.id === id),
  }));

  if (!diff.scalars.length && !collectionChanges.length && !changedEntrances.length &&
      !changedFiles.length && !diff.referenceMetadataChanges.length)
    return <Typography.Text type="secondary">No visible field changes</Typography.Text>;

  return (
    <Space direction="vertical" style={{ width: "100%" }}>
      {!!diff.scalars.length && (
        <Descriptions bordered column={1} size="small">
          {diff.scalars.map((change) => (
            <Descriptions.Item label={labels[change.path] ?? change.path} key={change.path}>
              <div>{displayValue(change.path, change.current)}</div>
              <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                Previous: {displayValue(change.path, change.previous)}
              </Typography.Text>
            </Descriptions.Item>
          ))}
        </Descriptions>
      )}
      {!!collectionChanges.length && (
        <Space wrap>
          {collectionChanges.map((change, index) => (
            <Tag color={change.added === true ? "success" : change.added === false ? "error" : "processing"} key={`${change.label}-${change.value}-${index}`}>
              {change.added === true ? "Added" : change.added === false ? "Removed" : "Changed"} {change.label}: {change.value}
            </Tag>
          ))}
        </Space>
      )}
      {!!changedEntrances.length && <Descriptions bordered column={1} size="small" title="Changed entrances">
        {changedEntrances.map((change) => <Descriptions.Item key={change.id} label={entityLabel(change.current, change.id)}>
          <div>{entranceSummary(change.current)}</div>
          <Typography.Text type="secondary" style={{ fontSize: 12 }}>Previous: {entranceSummary(change.previous) ?? "—"}</Typography.Text>
        </Descriptions.Item>)}
      </Descriptions>}
      {!!changedFiles.length && <Descriptions bordered column={1} size="small" title="Changed files">
        {changedFiles.map((change) => <Descriptions.Item key={change.id} label={entityLabel(change.current, change.id)}>
          <div>{entityLabel(change.current, change.id)}</div>
          <Typography.Text type="secondary" style={{ fontSize: 12 }}>Previous: {entityLabel(change.previous, change.id)}</Typography.Text>
        </Descriptions.Item>)}
      </Descriptions>}
      {diff.referenceMetadataChanges.map((change) => (
        <Typography.Text key={`${change.path}-${change.property}`}>
          {change.path}: {change.currentValue ?? "—"}
          <Typography.Text type="secondary"> (previously {change.previousValue ?? "—"})</Typography.Text>
        </Typography.Text>
      ))}
    </Space>
  );
};
