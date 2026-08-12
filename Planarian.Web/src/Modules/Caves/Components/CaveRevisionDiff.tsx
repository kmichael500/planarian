import { ReactNode } from "react";
import { Alert, Card, Descriptions, Grid, Space, Tag, theme, Typography } from "antd";
import { CaveRevisionDiffVm, CaveSnapshotVm, SnapshotTagReference } from "../Models/CaveRevisionVm";
import { CountyNumberIntent } from "../Models/CaveChangeRequestVm";
import {
  AffectedEntrancePresentation,
  AffectedFilePresentation,
  buildCaveRevisionDiffPresentation,
  ChangedFieldPresentation,
  DiffValueFormat,
  ReferenceMetadataPresentation,
  roleLabels,
  TagChangeGroup,
} from "./CaveRevisionDiffPresentation";
import { CaveTextDiff } from "./CaveTextDiff";
import {
  defaultIfEmpty,
  DistanceFormat,
  formatCoordinates,
  formatDate,
  formatDistance,
  formatNumber,
} from "../../../Shared/Helpers/StringHelpers";

const formatValue = (value: unknown, format: DiffValueFormat): ReactNode => {
  if (format === "coordinates") {
    const coordinates = Array.isArray(value) ? value : [];
    return defaultIfEmpty(formatCoordinates(coordinates[0] as number | undefined, coordinates[1] as number | undefined));
  }
  if (value === null || value === undefined || value === "") return "—";
  if (format === "boolean") return value ? "Yes" : "No";
  if (format === "distance") return defaultIfEmpty(formatDistance(Number(value), DistanceFormat.feet));
  if (format === "number" && typeof value === "number") return formatNumber(value) ?? "—";
  if (format === "date") return formatDate(String(value)) ?? "—";
  if (Array.isArray(value)) return value.length ? value.join(", ") : "—";
  return String(value);
};

const MetadataChanges = ({ changes }: { changes: ReferenceMetadataPresentation[] }) => changes.length ? <Space direction="vertical" size={2}>
  {changes.map(change => <div key={`${change.path}-${change.property}`}>
    <Typography.Text>Label updated</Typography.Text>
    <div><Typography.Text type="secondary">{change.previousLabel ?? "—"} → {change.currentLabel ?? "—"}</Typography.Text></div>
    <Typography.Text type="secondary" style={{ fontSize: 12 }}>Same referenced value</Typography.Text>
  </div>)}
</Space> : null;

const ChangedValue = ({ field, name }: { field: ChangedFieldPresentation; name: string }) => {
  const { token } = theme.useToken();
  if (field.textDiff) return <CaveTextDiff name={name} previous={field.previous == null ? "" : String(field.previous)} proposed={field.current == null ? "" : String(field.current)} />;
  return <Space direction="vertical" size={4} style={{ width: "100%" }}>
    {(field.previous !== undefined || field.current !== undefined) && <>
      <div style={{ background: token.colorErrorBg, border: `1px solid ${token.colorErrorBorder}`, borderRadius: token.borderRadiusSM, padding: `${token.paddingXXS}px ${token.paddingXS}px` }}>
        <Typography.Text type="danger">− Previous</Typography.Text> <span>{formatValue(field.previous, field.format)}</span>
      </div>
      <div style={{ background: token.colorSuccessBg, border: `1px solid ${token.colorSuccessBorder}`, borderRadius: token.borderRadiusSM, padding: `${token.paddingXXS}px ${token.paddingXS}px` }}>
        <Typography.Text type="success">+ Proposed</Typography.Text> <span>{formatValue(field.current, field.format)}</span>
      </div>
    </>}
    <MetadataChanges changes={field.metadata} />
  </Space>;
};

const TagChanges = ({ group }: { group: TagChangeGroup }) => <Space direction="vertical" size={4}>
  {group.removed.map(tag => <Tag color="error" key={`removed-${tag.role}-${tag.tagTypeId}`}>− Removed&nbsp;&nbsp;{tag.nameAtRevision}</Tag>)}
  {group.added.map(tag => <Tag color="success" key={`added-${tag.role}-${tag.tagTypeId}`}>+ Added&nbsp;&nbsp;{tag.nameAtRevision}</Tag>)}
  <MetadataChanges changes={group.metadata} />
</Space>;

const SnapshotTagGroups = ({ tags }: { tags: SnapshotTagReference[] }) => {
  const groups = tags.reduce<Record<string, SnapshotTagReference[]>>((result, tag) => {
    (result[tag.role] ??= []).push(tag);
    return result;
  }, {});
  const roleOrder = ["EntranceStatus", "FieldIndication", "EntranceHydrology", "EntranceReportedBy", "EntranceOther"];
  return <>{Object.keys(groups).sort((a, b) => {
    const ai = roleOrder.indexOf(a); const bi = roleOrder.indexOf(b);
    return (ai < 0 ? Number.MAX_SAFE_INTEGER : ai) - (bi < 0 ? Number.MAX_SAFE_INTEGER : bi) || a.localeCompare(b);
  }).map(role => <Descriptions.Item label={roleLabels[role] ?? role} key={role}>
    <Space wrap>{groups[role].sort((a, b) => a.nameAtRevision.localeCompare(b.nameAtRevision)).map(tag => <Tag key={tag.tagTypeId}>{tag.nameAtRevision}</Tag>)}</Space>
  </Descriptions.Item>)}</>;
};

const statusLabel = { added: "Added", removed: "Removed", changed: "Changed" } as const;

const Entrance = ({ entrance, layout }: { entrance: AffectedEntrancePresentation; layout: "horizontal" | "vertical" }) => {
  const snapshot = entrance.snapshot;
  const statusColor = entrance.status === "added" ? "success" : entrance.status === "removed" ? "error" : "processing";
  return <Card size="small" title={<Space><span>{entrance.heading.toLocaleUpperCase()}</span><Tag color={statusColor}>{statusLabel[entrance.status]}</Tag></Space>}>
    {!entrance.detailsAvailable && <Alert type="warning" showIcon message="Entrance details unavailable" description={`Entrance ID: ${entrance.id}`} />}
    {entrance.detailsAvailable && entrance.status === "changed" && <Descriptions bordered column={1} size="small" layout={layout}>
      {entrance.fields.map(field => <Descriptions.Item label={field.label} key={field.key}>
        <ChangedValue field={field} name={`entrance-${entrance.id}-${field.key}`} />
      </Descriptions.Item>)}
      {entrance.tagGroups.map(group => <Descriptions.Item label={group.label} key={group.role}><TagChanges group={group} /></Descriptions.Item>)}
    </Descriptions>}
    {entrance.detailsAvailable && entrance.status !== "changed" && snapshot && <Descriptions bordered column={1} size="small" layout={layout}>
      <Descriptions.Item label="Coordinates">{defaultIfEmpty(formatCoordinates(snapshot.latitude, snapshot.longitude))}</Descriptions.Item>
      <Descriptions.Item label="Description"><Typography.Paragraph style={{ whiteSpace: "pre-wrap", overflowWrap: "anywhere", marginBottom: 0 }}>{defaultIfEmpty(snapshot.description)}</Typography.Paragraph></Descriptions.Item>
      <Descriptions.Item label="Elevation">{defaultIfEmpty(formatDistance(snapshot.elevation, DistanceFormat.feet))}</Descriptions.Item>
      <Descriptions.Item label="Location Quality">{defaultIfEmpty(snapshot.locationQualityNameAtRevision)}</Descriptions.Item>
      <Descriptions.Item label="Name">{defaultIfEmpty(snapshot.name)}</Descriptions.Item>
      <Descriptions.Item label="Primary">{snapshot.isPrimary ? "Yes" : "No"}</Descriptions.Item>
      <Descriptions.Item label="Reported On">{formatDate(snapshot.reportedOn) ?? "—"}</Descriptions.Item>
      <Descriptions.Item label="Reported By">{defaultIfEmpty(snapshot.reportedByNameAtRevision ?? snapshot.reportedByUserId)}</Descriptions.Item>
      <Descriptions.Item label="Pit Depth">{defaultIfEmpty(formatDistance(snapshot.pitDepthFeet, DistanceFormat.feet))}</Descriptions.Item>
      <Descriptions.Item label="Coordinate Reference System">{formatNumber(snapshot.srid) ?? "—"}</Descriptions.Item>
      <SnapshotTagGroups tags={snapshot.tags} />
    </Descriptions>}
  </Card>;
};

const File = ({ file, layout }: { file: AffectedFilePresentation; layout: "horizontal" | "vertical" }) => {
  const snapshot = file.snapshot;
  const statusColor = file.status === "added" ? "success" : file.status === "removed" ? "error" : "processing";
  return <Card size="small" title={<Space><span>{file.heading}</span><Tag color={statusColor}>{statusLabel[file.status]}</Tag></Space>}>
    {!file.detailsAvailable && <Alert type="warning" showIcon message="File details unavailable" description={`File ID: ${file.id}`} />}
    {file.detailsAvailable && file.status === "changed" && <Descriptions bordered column={1} size="small" layout={layout}>
      {file.fields.map(field => <Descriptions.Item label={field.label} key={field.key}><ChangedValue field={field} name={`file-${file.id}-${field.key}`} /></Descriptions.Item>)}
    </Descriptions>}
    {file.detailsAvailable && file.status !== "changed" && snapshot && <Descriptions bordered column={1} size="small" layout={layout}>
      <Descriptions.Item label="Display Name">{defaultIfEmpty(snapshot.displayName)}</Descriptions.Item>
      <Descriptions.Item label="Filename">{defaultIfEmpty(snapshot.fileName)}</Descriptions.Item>
      <Descriptions.Item label="File Type">{defaultIfEmpty(snapshot.fileTypeNameAtRevision)}</Descriptions.Item>
    </Descriptions>}
  </Card>;
};

const SectionTitle = ({ children }: { children: ReactNode }) => <Typography.Title level={5} style={{ margin: 0 }}>{children}</Typography.Title>;

export const CaveRevisionDiff = ({ diff, previous, current, countyNumberIntent }: {
  diff?: CaveRevisionDiffVm;
  previous?: CaveSnapshotVm;
  current?: CaveSnapshotVm;
  countyNumberIntent?: CountyNumberIntent;
}) => {
  const screens = Grid.useBreakpoint();
  const { token } = theme.useToken();
  const layout = screens.md ? "horizontal" : "vertical";
  if (!diff) return <Typography.Text type="secondary">Initial publication</Typography.Text>;

  const model = buildCaveRevisionDiffPresentation(diff, previous, current, countyNumberIntent);
  const hasChanges = model.caveInformation.length || model.entrances.length || model.narrative || model.files.length ||
    model.fallbackScalars.length || model.fallbackMetadata.length;
  if (!hasChanges) return <Typography.Text type="secondary">No visible field changes</Typography.Text>;

  return <Space direction="vertical" size="large" style={{ width: "100%" }}>
    {!!model.caveInformation.length && <section>
      <SectionTitle>Cave Information</SectionTitle>
      <Descriptions bordered column={1} size="small" layout={layout} style={{ marginTop: token.marginXS }}>
        {model.caveInformation.map(field => <Descriptions.Item label={field.label} key={field.key}>
          {field.change && <ChangedValue field={field.change} name={`cave-${field.key}`} />}
          {field.tags && <TagChanges group={field.tags} />}
          {!field.change && !field.tags && <MetadataChanges changes={field.metadata} />}
        </Descriptions.Item>)}
      </Descriptions>
    </section>}

    {!!model.entrances.length && <section>
      <SectionTitle>Entrances</SectionTitle>
      <Space direction="vertical" style={{ width: "100%", marginTop: token.marginXS }}>
        {model.entrances.map(entrance => <Entrance key={`${entrance.status}-${entrance.id}`} entrance={entrance} layout={layout} />)}
      </Space>
    </section>}

    {model.narrative && <section>
      <SectionTitle>Narrative</SectionTitle>
      <Card size="small" style={{ marginTop: token.marginXS }}><ChangedValue field={model.narrative} name="cave-narrative" /></Card>
    </section>}

    {!!model.files.length && <section>
      <SectionTitle>Files</SectionTitle>
      <Space direction="vertical" style={{ width: "100%", marginTop: token.marginXS }}>
        {model.files.map(file => <File key={`${file.status}-${file.id}`} file={file} layout={layout} />)}
      </Space>
    </section>}

    {(!!model.fallbackScalars.length || !!model.fallbackMetadata.length) && <section>
      <SectionTitle>Other changes</SectionTitle>
      <Descriptions bordered column={1} size="small" layout={layout} style={{ marginTop: token.marginXS }}>
        {model.fallbackScalars.map(field => <Descriptions.Item label={field.label} key={field.key}>
          <ChangedValue field={field} name={`fallback-${field.key}`} />
          <Typography.Text type="secondary" style={{ fontSize: 12 }}>Path: {field.key}</Typography.Text>
        </Descriptions.Item>)}
        {model.fallbackMetadata.map(change => <Descriptions.Item label="Other reference change" key={`${change.path}-${change.property}`}>
          <Typography.Text code>{change.path}</Typography.Text><div>Property: {change.property}</div><MetadataChanges changes={[change]} />
        </Descriptions.Item>)}
      </Descriptions>
    </section>}
  </Space>;
};
