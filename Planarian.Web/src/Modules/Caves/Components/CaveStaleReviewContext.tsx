import { Alert, Collapse, List, Space, Typography } from "antd";
import { CaveChangeRequestDetailVm } from "../Models/CaveChangeRequestVm";
import { CaveRevisionDiffAssetActions, CaveRevisionDiff } from "./CaveRevisionDiff";
import { buildCaveRevisionDiffPresentation, CaveRevisionDiffPresentation } from "./CaveRevisionDiffPresentation";

export type StaleReviewAreaKind = "Cave field" | "Entrance" | "File" | "Line plot" | "Other";

export interface StaleReviewAttentionItem {
  key: string;
  kind: StaleReviewAreaKind;
  label: string;
}

export interface StaleReviewAnalysis {
  attentionItems: StaleReviewAttentionItem[];
  nonOverlappingSubmittedAreas: number;
}
const kindOrder: StaleReviewAreaKind[] = ["Cave field", "Entrance", "File", "Line plot", "Other"];

interface StaleReviewArea extends StaleReviewAttentionItem {
  entityKey: string;
  fieldKey?: string;
}

const collectAreas = (presentation: CaveRevisionDiffPresentation) => {
  const areas = new Map<string, StaleReviewArea>();
  const add = (entityKey: string, fieldKey: string | undefined, kind: StaleReviewAreaKind, label: string) => {
    const key = `${entityKey}:${fieldKey ?? "*"}`;
    areas.set(key, { key, entityKey, fieldKey, kind, label });
  };
  presentation.caveInformation.forEach(field => add("cave", field.key, "Cave field",
    field.change?.label ?? field.tags?.label ?? field.key));
  if (presentation.narrative) add("cave", "Narrative", "Cave field", presentation.narrative.label);
  presentation.fallbackScalars.forEach(field => add("other", field.key, "Other", field.label));
  presentation.fallbackMetadata.forEach(change => add("other", `${change.path}:${change.property}`,
    "Other", change.property));
  presentation.entrances.forEach(entrance => {
    const entityKey = `entrance:${entrance.id}`;
    if (entrance.status !== "changed") add(entityKey, undefined, "Entrance", entrance.heading);
    else {
      entrance.fields.forEach(field => add(entityKey, field.key, "Entrance",
        `${entrance.heading} — ${field.label}`));
      entrance.tagGroups.forEach(group => add(entityKey, group.role, "Entrance",
        `${entrance.heading} — ${group.label}`));
      if (!entrance.fields.length && !entrance.tagGroups.length) add(entityKey, undefined, "Entrance", entrance.heading);
    }
  });
  presentation.files.forEach(file => {
    const entityKey = `file:${file.id}`;
    if (file.status !== "changed") add(entityKey, undefined, "File", file.heading);
    else if (file.fields.length) file.fields.forEach(field => add(entityKey, field.key, "File",
      `${file.heading} — ${field.label}`));
    else add(entityKey, undefined, "File", file.heading);
  });
  presentation.linePlots.forEach(linePlot => {
    const entityKey = `linePlot:${linePlot.id}`;
    if (linePlot.status !== "changed") add(entityKey, undefined, "Line plot", linePlot.heading);
    else if (linePlot.fields.length) linePlot.fields.forEach(field => add(entityKey, field.key, "Line plot",
      `${linePlot.heading} — ${field.label}`));
    else add(entityKey, undefined, "Line plot", linePlot.heading);
  });
  return [...areas.values()];
};

const areasOverlap = (a: StaleReviewArea, b: StaleReviewArea) =>
  a.entityKey === b.entityKey && (!a.fieldKey || !b.fieldKey || a.fieldKey === b.fieldKey);

export const analyzeStaleReview = (detail: CaveChangeRequestDetailVm): StaleReviewAnalysis => {
  const submitted = collectAreas(buildCaveRevisionDiffPresentation(
    detail.diff, detail.base, detail.proposed, detail.countyNumberIntent));
  const published = detail.publishedSinceBase
    ? collectAreas(buildCaveRevisionDiffPresentation(detail.publishedSinceBase, detail.base, detail.current))
    : [];
  const attentionItems = submitted
    .filter(item => published.some(publishedItem => areasOverlap(item, publishedItem)))
    .map(({ key, kind, label }) => ({ key, kind, label }))
    .sort((a, b) => kindOrder.indexOf(a.kind) - kindOrder.indexOf(b.kind) || a.label.localeCompare(b.label));
  return {
    attentionItems,
    nonOverlappingSubmittedAreas: submitted.length - attentionItems.length,
  };
};

export const CaveStaleReviewNotice = () => <Alert
  type="warning"
  showIcon
  message="This cave changed after these edits were submitted."
  description="Review the newer cave changes, then re-review this submission before it can be approved."
/>;
export const CaveStaleReviewContext = ({ detail, assetActions, showOriginalSubmission = false }: {
  detail: CaveChangeRequestDetailVm;
  assetActions?: CaveRevisionDiffAssetActions;
  showOriginalSubmission?: boolean;
}) => {
  const analysis = analyzeStaleReview(detail);
  const overlapCount = analysis.attentionItems.length;
  const overlapMessage = overlapCount
    ? `${overlapCount} ${overlapCount === 1 ? "part" : "parts"} of this submission ${overlapCount === 1 ? "was" : "were"} changed again later`
    : "No parts of this submission were changed again";
  const overlapDescription = overlapCount
    ? "Compare these before deciding what should still be included."
    : "The newer cave updates are in different fields or items.";

  const originalSubmission = <Space direction="vertical" size="small" style={{ width: "100%" }}>
    <Typography.Text type="secondary">What was originally submitted.</Typography.Text>
    <CaveRevisionDiff diff={detail.diff} previous={detail.base} current={detail.proposed}
      countyNumberIntent={detail.countyNumberIntent} assetActions={assetActions} />
  </Space>;
  const changesMadeAfterSubmission = <Space direction="vertical" size="small" style={{ width: "100%" }}>
    <Typography.Text type="secondary">
      What changed on the cave after this submission was sent for review.
    </Typography.Text>
    {detail.publishedSinceBase
      ? <CaveRevisionDiff diff={detail.publishedSinceBase} previous={detail.base} current={detail.current} />
      : <Alert type="info" showIcon message="The newer cave changes are not available to compare." />}
  </Space>;
  const items = [
    ...(showOriginalSubmission
      ? [{ key: "original", label: "Original submission", children: originalSubmission }]
      : []),
    { key: "newer", label: "Changes made after submission", children: changesMadeAfterSubmission },
  ];

  return <Space direction="vertical" size="small" style={{ width: "100%" }}>
    <Space direction="vertical" size={2}>
      <Typography.Text strong>{overlapMessage}</Typography.Text>
      <Typography.Text type="secondary">{overlapDescription}</Typography.Text>
    </Space>
    {overlapCount > 0 && <List size="small" bordered dataSource={analysis.attentionItems}
      renderItem={item => <List.Item>
        <Space size="small">
          <Typography.Text>{item.label}</Typography.Text>
          {item.kind !== "Cave field" && <Typography.Text type="secondary">{item.kind}</Typography.Text>}
        </Space>
      </List.Item>} />}
    {analysis.nonOverlappingSubmittedAreas > 0 && <Typography.Text type="secondary">
      {analysis.nonOverlappingSubmittedAreas === 1
        ? "One other part of this submission was not changed again."
        : `${analysis.nonOverlappingSubmittedAreas} other parts of this submission were not changed again.`}
    </Typography.Text>}
    <Collapse size="small" items={items} />
  </Space>;
};
