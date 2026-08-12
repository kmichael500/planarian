import { useEffect, useState } from "react";
import { Alert, Collapse, Spin, Tag, Timeline, Typography } from "antd";
import { HistoryOutlined } from "@ant-design/icons";
import { PlanarianModal } from "../../../Shared/Components/Buttons/PlanarianModal";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
import { CaveService } from "../Service/CaveService";
import { CaveRevisionComparisonVm, CaveRevisionHistoryVm, CaveRevisionListItemVm } from "../Models/CaveRevisionVm";
import { CaveRevisionDiff } from "./CaveRevisionDiff";
import { formatDateTime } from "../../../Shared/Helpers/StringHelpers";

const sourceLabel = (revision: CaveRevisionListItemVm) => {
  if (revision.changeRequestId) return "Approved change request";
  if (revision.source === "Import") return "Imported";
  if (revision.source === "SystemBaseline") return "Revision tracking initialized";
  if (revision.operation === "Create") return "Created";
  if (revision.operation === "Archive") return "Archived";
  if (revision.operation === "Unarchive") return "Restored";
  return "Edited";
};

export const CaveHistoryModal = ({ caveId }: { caveId: string }) => {
  const [open, setOpen] = useState(false);
  const [loading, setLoading] = useState(false);
  const [history, setHistory] = useState<CaveRevisionHistoryVm>();
  const [comparisons, setComparisons] = useState<Record<string, CaveRevisionComparisonVm>>({});
  const [error, setError] = useState<string>();

  useEffect(() => {
    if (!open || history) return;
    setLoading(true);
    CaveService.GetRevisionHistory(caveId)
      .then(setHistory)
      .catch(() => setError("Cave history could not be loaded."))
      .finally(() => setLoading(false));
  }, [open, caveId, history]);

  const loadRevision = async (revisionId?: string | string[]) => {
    const id = Array.isArray(revisionId) ? revisionId[0] : revisionId;
    if (!id || comparisons[id]) return;
    try {
      const comparison = await CaveService.GetRevision(caveId, id);
      setComparisons((current) => ({ ...current, [id]: comparison }));
    } catch {
      setError("That revision could not be loaded.");
    }
  };

  return (
    <>
      <PlanarianButton icon={<HistoryOutlined />} onClick={() => setOpen(true)}>History</PlanarianButton>
      <PlanarianModal header="Cave History" open={open} onClose={() => setOpen(false)} width={900}>
        <Spin spinning={loading}>
          {error && <Alert type="error" showIcon message={error} style={{ marginBottom: 16 }} />}
          {!loading && history?.revisions.length === 0 && <Typography.Text type="secondary">Revision tracking has not started yet.</Typography.Text>}
          <Timeline
            items={history?.revisions.map((revision) => ({
              color: revision.isCurrent ? "green" : "blue",
              children: (
                <Collapse onChange={loadRevision} ghost items={[{
                  key: revision.id,
                  label: <div><strong>{sourceLabel(revision)}</strong>{revision.isCurrent && <Tag color="success" style={{ marginLeft: 8 }}>Current</Tag>}<div><Typography.Text type="secondary">Revision {revision.sequence} · {formatDateTime(revision.publishedOn)}{revision.actorName ? ` · ${revision.actorName}` : ""}</Typography.Text></div></div>,
                  children: comparisons[revision.id] ? <CaveRevisionDiff diff={comparisons[revision.id].diff} previous={comparisons[revision.id].previous} current={comparisons[revision.id].current} /> : <Spin size="small" />,
                }]} />
              ),
            })) ?? []}
          />
        </Spin>
      </PlanarianModal>
    </>
  );
};
