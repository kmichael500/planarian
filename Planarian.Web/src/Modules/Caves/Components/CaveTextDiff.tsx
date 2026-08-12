import { useMemo, useState } from "react";
import { Alert, Segmented, Space, theme, Typography } from "antd";
import { Change, diffWordsWithSpace } from "diff";

export const CAVE_TEXT_DIFF_TIMEOUT_MS = 75;
export const CAVE_TEXT_DIFF_MAX_EDIT_LENGTH = 20_000;

export type TextDiffer = (previous: string, proposed: string,
  options: { timeout: number; maxEditLength: number }) => Change[] | undefined;

export const computeCaveTextDiff = (previous: string, proposed: string,
  differ: TextDiffer = diffWordsWithSpace): Change[] | undefined =>
  differ(previous, proposed, { timeout: CAVE_TEXT_DIFF_TIMEOUT_MS, maxEditLength: CAVE_TEXT_DIFF_MAX_EDIT_LENGTH });

const CompleteText = ({ text }: { text: string }) =>
  <Typography.Paragraph style={{ whiteSpace: "pre-wrap", overflowWrap: "anywhere", marginBottom: 0 }}>
    {text || "—"}
  </Typography.Paragraph>;

const BeforeAfterFallback = ({ previous, proposed }: { previous: string; proposed: string }) => <Space direction="vertical" style={{ width: "100%" }}>
  <Alert type="info" showIcon message="Inline changes were too complex to display." />
  <div><Typography.Text type="secondary">− Previous</Typography.Text><CompleteText text={previous} /></div>
  <div><Typography.Text type="secondary">+ Proposed</Typography.Text><CompleteText text={proposed} /></div>
</Space>;

export const CaveTextDiff = ({ previous, proposed, name, differ }: {
  previous?: string | null;
  proposed?: string | null;
  name: string;
  differ?: TextDiffer;
}) => {
  const [view, setView] = useState<string | number>("Changes");
  const { token } = theme.useToken();
  const previousText = previous ?? "";
  const proposedText = proposed ?? "";
  const changes = useMemo(() => computeCaveTextDiff(previousText, proposedText, differ), [previousText, proposedText, differ]);

  return <Space direction="vertical" style={{ width: "100%" }}>
    <Segmented name={name} value={view} onChange={setView} options={["Changes", "Previous", "Proposed"]} />
    {view === "Previous" && <CompleteText text={previousText} />}
    {view === "Proposed" && <CompleteText text={proposedText} />}
    {view === "Changes" && (changes === undefined
      ? <BeforeAfterFallback previous={previousText} proposed={proposedText} />
      : <div style={{ whiteSpace: "pre-wrap", overflowWrap: "anywhere" }}>
        <Space size="middle" style={{ display: "flex", marginBottom: token.marginXS }}>
          <Typography.Text type="danger">− Removed</Typography.Text>
          <Typography.Text type="success">+ Added</Typography.Text>
        </Space>
        {changes.map((change, index) => change.added
          ? <ins key={index} style={{ textDecoration: "none", color: token.colorSuccessText, background: token.colorSuccessBg }}>{change.value}</ins>
          : change.removed
            ? <del key={index} style={{ color: token.colorErrorText, background: token.colorErrorBg }}>{change.value}</del>
            : <span key={index}>{change.value}</span>)}
      </div>)}
  </Space>;
};
