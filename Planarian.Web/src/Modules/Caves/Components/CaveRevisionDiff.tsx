import { Fragment, ReactNode } from "react";
import { Alert, Card, Grid, Space, Tag, theme, Typography } from "antd";
import { CaveRevisionDiffVm, CaveSnapshotVm, SnapshotTagReference } from "../Models/CaveRevisionVm";
import { CaveProposalCountyNumberChangeVm, CountyNumberIntent } from "../Models/CaveChangeRequestVm";
import {
  AffectedEntrancePresentation,
  AffectedFilePresentation,
  AffectedLinePlotPresentation,
  buildCaveRevisionDiffPresentation,
  CaveInformationFieldPresentation,
  CaveRevisionDiffSummaryPresentation,
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

const formatValue = (value: unknown, format: DiffValueFormat): string => {
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

const MetadataChanges = ({ changes }: { changes: ReferenceMetadataPresentation[] }) => changes.length ?
  <Space direction="vertical" size={2}>
    {changes.map(change => <Typography.Text type="secondary" style={{ fontSize: 12 }}
      key={`${change.path}-${change.property}`}>
      <strong>Label updated:</strong> {change.previousLabel ?? "—"} → {change.currentLabel ?? "—"}
      <span> · Same referenced value</span>
    </Typography.Text>)}
  </Space> : null;

const SignedValue = ({ kind, value }: { kind: "before" | "after"; value: string }) => {
  const before = kind === "before";
  return <Typography.Text type={before ? "danger" : "success"}
    aria-label={`${before ? "Before" : "After"}: ${value}`}>
    {before ? "−" : "+"} {value}
  </Typography.Text>;
};
const TagTokens = ({ tags, kind }: { tags: SnapshotTagReference[]; kind: "added" | "removed" }) => {
  if (!tags.length) return <>—</>;
  const added = kind === "added";
  return <Space wrap size={[4, 4]}>
    {tags.map(tag => <Tag color={added ? "success" : "error"} key={`${kind}-${tag.role}-${tag.tagTypeId}`}
      aria-label={`${added ? "Added" : "Removed"} ${tag.nameAtRevision}`}
      style={{ whiteSpace: "normal", overflowWrap: "anywhere", maxWidth: "100%" }}>
      <span aria-hidden="true">{added ? "+" : "−"} </span>{tag.nameAtRevision}
    </Tag>)}
  </Space>;
};

const StringTokens = ({ values, kind }: { values: string[]; kind: "added" | "removed" }) => {
  if (!values.length) return <>—</>;
  const added = kind === "added";
  return <Space wrap size={[4, 4]}>
    {values.map(value => <Tag color={added ? "success" : "error"} key={`${kind}-${value}`}
      aria-label={`${added ? "Added" : "Removed"} ${value}`}
      style={{ whiteSpace: "normal", overflowWrap: "anywhere", maxWidth: "100%" }}>
      <span aria-hidden="true">{added ? "+" : "−"} </span>{value}
    </Tag>)}
  </Space>;
};

interface ComparisonItem {
  key: string;
  label: string;
  before: ReactNode;
  after: ReactNode;
  mobile: ReactNode;
  metadata?: ReactNode;
}

const scalarComparisonItem = (field: ChangedFieldPresentation): ComparisonItem => {
  const metadata = field.metadata.length ? <MetadataChanges changes={field.metadata} /> : undefined;
  if (field.format === "stringList") {
    const previous = Array.isArray(field.previous) ? field.previous.filter((value): value is string => typeof value === "string") : [];
    const current = Array.isArray(field.current) ? field.current.filter((value): value is string => typeof value === "string") : [];
    const removed = previous.filter(value => !current.includes(value));
    const added = current.filter(value => !previous.includes(value));
    return {
      key: field.key,
      label: field.label,
      before: <StringTokens values={removed} kind="removed" />,
      after: <StringTokens values={added} kind="added" />,
      mobile: <Space direction="vertical" size={4}>
        {!!removed.length && <StringTokens values={removed} kind="removed" />}
        {!!added.length && <StringTokens values={added} kind="added" />}
        {metadata}
      </Space>,
      metadata,
    };
  }

  const before = formatValue(field.previous, field.format);
  const after = formatValue(field.current, field.format);
  return {
    key: field.key,
    label: field.label,
    before,
    after,
    mobile: <Space direction="vertical" size={2}>
      <SignedValue kind="before" value={before} />
      <SignedValue kind="after" value={after} />
      {metadata}
    </Space>,
    metadata,
  };
};
const tagComparisonItem = (group: TagChangeGroup): ComparisonItem => {
  const metadata = group.metadata.length ? <MetadataChanges changes={group.metadata} /> : undefined;
  return {
    key: group.role,
    label: group.label,
    before: <TagTokens tags={group.removed} kind="removed" />,
    after: <TagTokens tags={group.added} kind="added" />,
    mobile: <Space direction="vertical" size={4}>
      {!!group.removed.length && <TagTokens tags={group.removed} kind="removed" />}
      {!!group.added.length && <TagTokens tags={group.added} kind="added" />}
      {metadata}
    </Space>,
    metadata,
  };
};

const metadataComparisonItem = (key: string, label: string,
  metadata: ReferenceMetadataPresentation[]): ComparisonItem => ({
    key,
    label,
    before: <>—</>,
    after: <>—</>,
    mobile: <MetadataChanges changes={metadata} />,
    metadata: <MetadataChanges changes={metadata} />,
  });

const caveInformationItem = (field: CaveInformationFieldPresentation): ComparisonItem => {
  if (field.change) return scalarComparisonItem(field.change);
  if (field.tags) return tagComparisonItem(field.tags);
  return metadataComparisonItem(field.key, field.label, field.metadata);
};

const ComparisonBlock = ({ items, isDesktop, label }: {
  items: ComparisonItem[];
  isDesktop: boolean;
  label: string;
}) => {
  const { token } = theme.useToken();
  if (!items.length) return null;
  if (!isDesktop) return <Space direction="vertical" size={0} style={{ width: "100%" }}>
    {items.map((item, index) => <div role="group" aria-label={`${item.label} change`} key={item.key} style={{
      padding: `${token.paddingXS}px 0`, borderTop: index ? `1px solid ${token.colorSplit}` : undefined,
    }}>
      <Typography.Text type="secondary" strong>{item.label}</Typography.Text>
      <div style={{ marginTop: token.marginXXS, overflowWrap: "anywhere" }}>{item.mobile}</div>
    </div>)}
  </Space>;

  return <table aria-label={label} style={{ width: "100%", borderCollapse: "collapse", tableLayout: "fixed" }}>
    <colgroup><col style={{ width: "24%" }} /><col style={{ width: "38%" }} /><col style={{ width: "38%" }} /></colgroup>
    <thead>
      <tr>
        <th scope="col" style={{ textAlign: "left", padding: `0 ${token.paddingSM}px ${token.paddingXS}px 0` }}>
          <Typography.Text type="secondary" strong>Field</Typography.Text>
        </th>
        <th scope="col" style={{ textAlign: "left", padding: `0 ${token.paddingSM}px ${token.paddingXS}px` }}>
          <Typography.Text type="secondary" strong>Before</Typography.Text>
        </th>
        <th scope="col" style={{ textAlign: "left", padding: `0 0 ${token.paddingXS}px ${token.paddingSM}px` }}>
          <Typography.Text type="secondary" strong>After</Typography.Text>
        </th>
      </tr>
    </thead>
    <tbody>
      {items.map(item => <Fragment key={item.key}>
        <tr style={{ borderTop: `1px solid ${token.colorSplit}` }}>
          <th scope="row" style={{ textAlign: "left", verticalAlign: "top", fontWeight: 500,
            padding: `${token.paddingXS}px ${token.paddingSM}px ${token.paddingXS}px 0`, overflowWrap: "anywhere" }}>
            {item.label}
          </th>
          <td style={{ verticalAlign: "top", padding: `${token.paddingXS}px ${token.paddingSM}px`,
            overflowWrap: "anywhere" }}>{item.before}</td>
          <td style={{ verticalAlign: "top", padding: `${token.paddingXS}px 0 ${token.paddingXS}px ${token.paddingSM}px`,
            overflowWrap: "anywhere" }}>{item.after}</td>
        </tr>
        {item.metadata && <tr key={`${item.key}-metadata`}>
          <td />
          <td colSpan={2} style={{ padding: `0 0 ${token.paddingXS}px ${token.paddingSM}px` }}>{item.metadata}</td>
        </tr>}
      </Fragment>)}
    </tbody>
  </table>;
};

interface SnapshotItem {
  key: string;
  label: string;
  value: ReactNode;
  fullWidth?: boolean;
}

const SnapshotDetails = ({ items, isDesktop, columns = 2 }: {
  items: SnapshotItem[];
  isDesktop: boolean;
  columns?: number;
}) => {
  const { token } = theme.useToken();
  return <div style={{ display: "grid", gridTemplateColumns: `repeat(${isDesktop ? columns : 1}, minmax(0, 1fr))`,
    gap: `${token.marginSM}px ${token.marginLG}px` }}>
    {items.map(item => <div key={item.key} style={{ minWidth: 0,
      gridColumn: item.fullWidth && isDesktop ? "1 / -1" : undefined }}>
      <Typography.Text type="secondary" style={{ fontSize: 12, fontWeight: 600 }}>{item.label}</Typography.Text>
      <div style={{ marginTop: 2, overflowWrap: "anywhere", whiteSpace: "pre-wrap" }}>{item.value}</div>
    </div>)}
  </div>;
};

const snapshotTagItems = (tags: SnapshotTagReference[]): SnapshotItem[] => {
  const groups = tags.reduce<Record<string, SnapshotTagReference[]>>((result, tag) => {
    (result[tag.role] ??= []).push(tag);
    return result;
  }, {});
  const roleOrder = ["EntranceStatus", "FieldIndication", "EntranceHydrology", "EntranceReportedBy", "EntranceOther"];
  return Object.keys(groups).sort((a, b) => {
    const ai = roleOrder.indexOf(a); const bi = roleOrder.indexOf(b);
    return (ai < 0 ? Number.MAX_SAFE_INTEGER : ai) - (bi < 0 ? Number.MAX_SAFE_INTEGER : bi) || a.localeCompare(b);
  }).map(role => ({
    key: `tag-${role}`,
    label: roleLabels[role] ?? role,
    fullWidth: true,
    value: <Space wrap size={[4, 4]}>{groups[role].sort((a, b) => a.nameAtRevision.localeCompare(b.nameAtRevision))
      .map(tag => <Tag key={tag.tagTypeId} style={{ whiteSpace: "normal", overflowWrap: "anywhere", maxWidth: "100%" }}>
        {tag.nameAtRevision}
      </Tag>)}</Space>,
  }));
};

const statusLabel = { added: "Added", removed: "Removed", changed: "Changed" } as const;
const statusColor = (status: "added" | "removed" | "changed") =>
  status === "added" ? "success" : status === "removed" ? "error" : "processing";

const EntityTitle = ({ heading, status }: { heading: string; status: "added" | "removed" | "changed" }) =>
  <Space size={8} wrap>
    <Typography.Text strong>{heading}</Typography.Text>
    <Tag color={statusColor(status)}>{statusLabel[status]}</Tag>
  </Space>;

const LongTextField = ({ field, name }: { field: ChangedFieldPresentation; name: string }) => {
  const { token } = theme.useToken();
  return <div role="group" aria-label={`${field.label} change`} style={{
    paddingTop: token.paddingXS, borderTop: `1px solid ${token.colorSplit}`,
  }}>
    <Typography.Text strong>{field.label}</Typography.Text>
    <div style={{ marginTop: token.marginXS }}>
      <CaveTextDiff name={name} previous={field.previous == null ? "" : String(field.previous)}
        proposed={field.current == null ? "" : String(field.current)} />
    </div>
    <MetadataChanges changes={field.metadata} />
  </div>;
};

const Entrance = ({ entrance, isDesktop }: { entrance: AffectedEntrancePresentation; isDesktop: boolean }) => {
  const snapshot = entrance.snapshot;
  const textFields = entrance.fields.filter(field => field.textDiff);
  const comparisonItems = [
    ...entrance.fields.filter(field => !field.textDiff).map(scalarComparisonItem),
    ...entrance.tagGroups.map(tagComparisonItem),
  ];

  return <Card size="small" title={<EntityTitle heading={entrance.heading} status={entrance.status} />}>
    {!entrance.detailsAvailable && <Alert type="warning" showIcon message="Entrance details unavailable"
      description={`Entrance ID: ${entrance.id}`} />}
    {entrance.detailsAvailable && entrance.status === "changed" && <Space direction="vertical" size="middle"
      style={{ width: "100%" }}>
      <ComparisonBlock items={comparisonItems} isDesktop={isDesktop} label={`${entrance.heading} changes`} />
      {textFields.map(field => <LongTextField key={field.key} field={field}
        name={`entrance-${entrance.id}-${field.key}`} />)}
    </Space>}
    {entrance.detailsAvailable && entrance.status !== "changed" && snapshot && <SnapshotDetails isDesktop={isDesktop}
      columns={3} items={[
        { key: "coordinates", label: "Coordinates", value: defaultIfEmpty(formatCoordinates(snapshot.latitude, snapshot.longitude)) },
        { key: "elevation", label: "Elevation", value: defaultIfEmpty(formatDistance(snapshot.elevation, DistanceFormat.feet)) },
        { key: "location-quality", label: "Location Quality", value: defaultIfEmpty(snapshot.locationQualityNameAtRevision) },
        { key: "primary", label: "Primary", value: snapshot.isPrimary ? "Yes" : "No" },
        { key: "reported-on", label: "Reported On", value: formatDate(snapshot.reportedOn) ?? "—" },
        { key: "pit-depth", label: "Pit Depth", value: defaultIfEmpty(formatDistance(snapshot.pitDepthFeet, DistanceFormat.feet)) },
        { key: "srid", label: "Coordinate Reference System", value: formatNumber(snapshot.srid) ?? "—" },
        { key: "description", label: "Description", value: defaultIfEmpty(snapshot.description), fullWidth: true },
        ...snapshotTagItems(snapshot.tags),
      ]} />}
  </Card>;
};

const File = ({ file, isDesktop }: { file: AffectedFilePresentation; isDesktop: boolean }) => {
  const snapshot = file.snapshot;
  return <Card size="small" title={<EntityTitle heading={file.heading} status={file.status} />}>
    {!file.detailsAvailable && <Alert type="warning" showIcon message="File details unavailable"
      description={`File ID: ${file.id}`} />}
    {file.detailsAvailable && file.status === "changed" &&
      <ComparisonBlock items={file.fields.map(scalarComparisonItem)} isDesktop={isDesktop}
        label={`${file.heading} changes`} />}
    {file.detailsAvailable && file.status !== "changed" && snapshot && <SnapshotDetails isDesktop={isDesktop}
      columns={2} items={[
        { key: "extension", label: "Extension", value: defaultIfEmpty(snapshot.extension) },
        { key: "file-type", label: "File Type", value: defaultIfEmpty(snapshot.fileTypeNameAtRevision) },
      ]} />}
  </Card>;
};

const LinePlot = ({ linePlot, isDesktop }: { linePlot: AffectedLinePlotPresentation; isDesktop: boolean }) => {
  const snapshot = linePlot.snapshot;
  return <Card size="small" title={<EntityTitle heading={linePlot.heading} status={linePlot.status} />}>
    {!linePlot.detailsAvailable && <Alert type="warning" showIcon message="Line plot details unavailable"
      description={`Line plot ID: ${linePlot.id}`} />}
    {linePlot.detailsAvailable && linePlot.status === "changed" &&
      <ComparisonBlock items={linePlot.fields.map(scalarComparisonItem)} isDesktop={isDesktop}
        label={`${linePlot.heading} changes`} />}
    {linePlot.detailsAvailable && linePlot.status !== "changed" && snapshot && <SnapshotDetails isDesktop={isDesktop}
      columns={1} items={[{ key: "content", label: "Content", value: "GeoJSON content" }]} />}
  </Card>;
};

const countLabel = (count: number, singular: string, plural = `${singular}s`) =>
  `${count} ${count === 1 ? singular : plural}`;

const ScopeSummary = ({ summary }: { summary: CaveRevisionDiffSummaryPresentation }) => {
  const parts: string[] = [];
  if (summary.caveFields) parts.push(countLabel(summary.caveFields, "cave field"));
  if (summary.entrances.changed) parts.push(`${countLabel(summary.entrances.changed, "entrance")} changed`);
  if (summary.entrances.added) parts.push(`${countLabel(summary.entrances.added, "entrance")} added`);
  if (summary.entrances.removed) parts.push(`${countLabel(summary.entrances.removed, "entrance")} removed`);
  if (summary.files.total) parts.push(countLabel(summary.files.total, "file"));
  if (summary.linePlots.total) parts.push(countLabel(summary.linePlots.total, "line plot"));
  if (summary.otherChanges) parts.push(countLabel(summary.otherChanges, "other change"));
  return <Typography.Text type="secondary" style={{ fontWeight: 600 }}>{parts.join(" · ")}</Typography.Text>;
};

const SectionTitle = ({ children }: { children: ReactNode }) =>
  <Typography.Title level={5} style={{ margin: 0 }}>{children}</Typography.Title>;

export const CaveRevisionDiff = ({ diff, previous, current, countyNumberIntent, proposalCountyNumberChange }: {
  diff?: CaveRevisionDiffVm;
  previous?: CaveSnapshotVm;
  current?: CaveSnapshotVm;
  countyNumberIntent?: CountyNumberIntent;
  proposalCountyNumberChange?: CaveProposalCountyNumberChangeVm;
}) => {
  const screens = Grid.useBreakpoint();
  const { token } = theme.useToken();
  const isDesktop = !!screens.md;
  if (!diff) return <Typography.Text type="secondary">Initial publication</Typography.Text>;

  const model = buildCaveRevisionDiffPresentation(diff, previous, current, countyNumberIntent,
    proposalCountyNumberChange);
  const hasChanges = model.caveInformation.length || model.entrances.length || model.narrative || model.files.length ||
    model.linePlots.length || model.fallbackScalars.length || model.fallbackMetadata.length;
  if (!hasChanges) return <Typography.Text type="secondary">No visible field changes</Typography.Text>;
  const fallbackItems: ComparisonItem[] = [
    ...model.fallbackScalars.map(field => ({
      ...scalarComparisonItem(field),
      metadata: <Space direction="vertical" size={2}>
        <Typography.Text type="secondary" style={{ fontSize: 12 }}>Path: {field.key}</Typography.Text>
        <MetadataChanges changes={field.metadata} />
      </Space>,
    })),
    ...model.fallbackMetadata.map(change => metadataComparisonItem(
      `${change.path}-${change.property}`, "Other reference change", [change])),
  ];

  return <Space direction="vertical" size="large" style={{ width: "100%" }}>
    <ScopeSummary summary={model.summary} />

    {!!model.caveInformation.length && <section>
      <SectionTitle>Cave Information</SectionTitle>
      <div style={{ marginTop: token.marginSM }}>
        <ComparisonBlock items={model.caveInformation.map(caveInformationItem)} isDesktop={isDesktop}
          label="Cave information changes" />
      </div>
    </section>}

    {!!model.entrances.length && <section>
      <SectionTitle>Entrances</SectionTitle>
      <Space direction="vertical" size="small" style={{ width: "100%", marginTop: token.marginSM }}>
        {model.entrances.map(entrance => <Entrance key={`${entrance.status}-${entrance.id}`}
          entrance={entrance} isDesktop={isDesktop} />)}
      </Space>
    </section>}
    {model.narrative && <section>
      <SectionTitle>Narrative</SectionTitle>
      <div style={{ marginTop: token.marginSM }}>
        <CaveTextDiff name="cave-narrative" previous={model.narrative.previous == null ? "" : String(model.narrative.previous)}
          proposed={model.narrative.current == null ? "" : String(model.narrative.current)} />
      </div>
    </section>}

    {!!model.files.length && <section>
      <SectionTitle>Files</SectionTitle>
      <Space direction="vertical" size="small" style={{ width: "100%", marginTop: token.marginSM }}>
        {model.files.map(file => <File key={`${file.status}-${file.id}`} file={file} isDesktop={isDesktop} />)}
      </Space>
    </section>}

    {!!model.linePlots.length && <section>
      <SectionTitle>Line Plots</SectionTitle>
      <Space direction="vertical" size="small" style={{ width: "100%", marginTop: token.marginSM }}>
        {model.linePlots.map(linePlot => <LinePlot key={`${linePlot.status}-${linePlot.id}`}
          linePlot={linePlot} isDesktop={isDesktop} />)}
      </Space>
    </section>}

    {!!fallbackItems.length && <section>
      <SectionTitle>Other changes</SectionTitle>
      <div style={{ marginTop: token.marginSM }}>
        <ComparisonBlock items={fallbackItems} isDesktop={isDesktop} label="Other changes" />
      </div>
    </section>}
  </Space>;
};
