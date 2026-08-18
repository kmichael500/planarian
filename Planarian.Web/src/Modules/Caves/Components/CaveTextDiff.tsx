import { useMemo, useState } from "react";
import { Alert, Button, Segmented, Space, theme, Typography } from "antd";
import { Change, diffWordsWithSpace } from "diff";

export const CAVE_TEXT_DIFF_TIMEOUT_MS = 75;
export const CAVE_TEXT_DIFF_MAX_EDIT_LENGTH = 20_000;
const CAVE_TEXT_DIFF_CONTEXT_CHARS = 240;

export type TextDiffer = (previous: string, proposed: string,
  options: { timeout: number; maxEditLength: number }) => Change[] | undefined;

interface FocusedChange extends Change {
  omitted?: boolean;
}

export const computeCaveTextDiff = (previous: string, proposed: string,
  differ: TextDiffer = diffWordsWithSpace): Change[] | undefined =>
  differ(previous, proposed, { timeout: CAVE_TEXT_DIFF_TIMEOUT_MS, maxEditLength: CAVE_TEXT_DIFF_MAX_EDIT_LENGTH });

const hasEdit = (changes: Change[], start: number, end: number) =>
  changes.slice(start, end).some(change => change.added || change.removed);

const focusChanges = (changes: Change[]): { changes: FocusedChange[]; hasOmittedContext: boolean } => {
  const focused: FocusedChange[] = [];
  let hasOmittedContext = false;
  changes.forEach((change, index) => {
    if (change.added || change.removed || change.value.length <= CAVE_TEXT_DIFF_CONTEXT_CHARS * 2) {
      focused.push(change);
      return;
    }

    const hasEditBefore = hasEdit(changes, 0, index);
    const hasEditAfter = hasEdit(changes, index + 1, changes.length);
    if (!hasEditBefore && !hasEditAfter) {
      focused.push(change);
      return;
    }

    hasOmittedContext = true;
    const before = hasEditBefore ? change.value.slice(0, CAVE_TEXT_DIFF_CONTEXT_CHARS) : "";
    const after = hasEditAfter ? change.value.slice(-CAVE_TEXT_DIFF_CONTEXT_CHARS) : "";
    if (before) focused.push({ value: before });
    focused.push({ value: "", omitted: true });
    if (after && after !== before) focused.push({ value: after });
  });

  return { changes: focused, hasOmittedContext };
};

const CompleteText = ({ text }: { text: string }) =>
  <Typography.Paragraph style={{ whiteSpace: "pre-wrap", overflowWrap: "anywhere", marginBottom: 0 }}>
    {text || "—"}
  </Typography.Paragraph>;
const BeforeAfterFallback = ({ previous, proposed }: { previous: string; proposed: string }) =>
  <Space direction="vertical" style={{ width: "100%" }}>
    <Alert type="info" showIcon message="Inline changes were too complex to display." />
    <div><Typography.Text type="secondary">Before</Typography.Text><CompleteText text={previous} /></div>
    <div><Typography.Text type="secondary">After</Typography.Text><CompleteText text={proposed} /></div>
  </Space>;

const RenderChanges = ({ changes }: { changes: FocusedChange[] }) => {
  const { token } = theme.useToken();
  return <div style={{ whiteSpace: "pre-wrap", overflowWrap: "anywhere" }}>
    {changes.map((change, index) => {
      if (change.omitted) return <span key={`omitted-${index}`} aria-label="Unchanged text omitted" style={{
        display: "block", color: token.colorTextDescription, textAlign: "center", margin: `${token.marginXS}px 0`,
      }}>… unchanged text …</span>;
      if (change.added) return <ins key={index} style={{
        color: token.colorSuccessText, background: token.colorSuccessBg, textDecoration: "underline",
        textDecorationThickness: "1px", textUnderlineOffset: "2px",
      }}>{change.value}</ins>;
      if (change.removed) return <del key={index} style={{
        color: token.colorErrorText, background: token.colorErrorBg,
      }}>{change.value}</del>;
      return <span key={index}>{change.value}</span>;
    })}
  </div>;
};
export const CaveTextDiff = ({ previous, proposed, name, differ }: {
  previous?: string | null;
  proposed?: string | null;
  name: string;
  differ?: TextDiffer;
}) => {
  const [view, setView] = useState<string | number>("Changes");
  const [showFullContext, setShowFullContext] = useState(false);
  const { token } = theme.useToken();
  const previousText = previous ?? "";
  const proposedText = proposed ?? "";
  const changes = useMemo(() => computeCaveTextDiff(previousText, proposedText, differ), [previousText, proposedText, differ]);
  const focused = useMemo(() => changes ? focusChanges(changes) : undefined, [changes]);
  const hasAdded = !!changes?.some(change => change.added);
  const hasRemoved = !!changes?.some(change => change.removed);

  return <Space direction="vertical" size="small" style={{ width: "100%" }}>
    <Segmented name={name} value={view} onChange={setView} options={["Changes", "Before", "After"]} />
    {view === "Before" && <CompleteText text={previousText} />}
    {view === "After" && <CompleteText text={proposedText} />}
    {view === "Changes" && (changes === undefined
      ? <BeforeAfterFallback previous={previousText} proposed={proposedText} />
      : <>
        <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: token.marginXS,
          flexWrap: "wrap" }}>
          <Space size="middle">
            {hasRemoved && <Typography.Text type="danger">− Removed</Typography.Text>}
            {hasAdded && <Typography.Text type="success">+ Added</Typography.Text>}
          </Space>
          {focused?.hasOmittedContext && <Button type="link" size="small" style={{ paddingInline: 0 }}
            aria-expanded={showFullContext} onClick={() => setShowFullContext(value => !value)}>
            {showFullContext ? "Show less context" : "Show full context"}
          </Button>}
        </div>
        <RenderChanges changes={showFullContext || !focused ? changes : focused.changes} />
      </>)}
  </Space>;
};
