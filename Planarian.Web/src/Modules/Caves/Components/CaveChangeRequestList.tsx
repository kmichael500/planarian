import { Empty, List, Space, Tag, Typography } from "antd";
import { Link } from "react-router-dom";
import { ClientRoutes } from "../../../Configuration/Routing/ClientRoutes.generated";
import { formatDateTime } from "../../../Shared/Helpers/StringHelpers";
import { CaveChangeRequestSummaryVm, CaveChangeScopeSummaryVm } from "../Models/CaveChangeRequestVm";

const plural = (count: number, singular: string, pluralValue = `${singular}s`) =>
  `${count} ${count === 1 ? singular : pluralValue}`;

export const formatChangeScopeSummary = (summary: CaveChangeScopeSummaryVm) => {
  const parts: string[] = [];
  if (summary.caveFields) parts.push(plural(summary.caveFields, "cave field"));
  if (summary.entrances.total) parts.push(plural(summary.entrances.total, "entrance"));
  if (summary.files.total) parts.push(plural(summary.files.total, "file"));
  if (summary.linePlots.total) parts.push(plural(summary.linePlots.total, "line plot"));
  if (summary.otherChanges) parts.push(plural(summary.otherChanges, "other change"));
  return parts.length ? parts.join(" · ") : "No visible changes";
};

const statusTag = (request: CaveChangeRequestSummaryVm, review: boolean) => {
  if (request.isStale) return <Tag color="warning">Needs re-review</Tag>;
  if (review) return null;
  if (request.status === "Approved") return <Tag color="success">Approved</Tag>;
  if (request.status === "Rejected") return <Tag color="error">Rejected</Tag>;
  return <Tag color="processing">Pending review</Tag>;
};

const EmptyState = ({ review }: { review: boolean }) => (
  <Empty
    image={Empty.PRESENTED_IMAGE_SIMPLE}
    description={
      <Space direction="vertical" size={2}>
        <Typography.Text strong>{review ? "Nothing is waiting for review" : "No submissions yet"}</Typography.Text>
        <Typography.Text type="secondary">
          {review ? "New submissions you can review will appear here." : "Changes you submit for review will appear here."}
        </Typography.Text>
      </Space>
    }
  />
);

export const CaveChangeRequestList = ({ requests, review = false }: {
  requests: CaveChangeRequestSummaryVm[];
  review?: boolean;
}) => {
  if (!requests.length) return <EmptyState review={review} />;

  return (
    <List
      bordered
      dataSource={requests}
      renderItem={(request) => (
        <List.Item key={request.id} style={{ padding: 0 }}>
          <Link
            to={ClientRoutes.caveChangeRequest.get(request.id)}
            style={{ display: "block", width: "100%", padding: "12px 16px", color: "inherit" }}
          >
            <div style={{ display: "flex", gap: 12, justifyContent: "space-between", alignItems: "flex-start", flexWrap: "wrap" }}>
              <Space direction="vertical" size={2} style={{ minWidth: 0, flex: "1 1 260px" }}>
                <Typography.Text strong style={{ fontSize: 15 }}>{request.caveName}</Typography.Text>
                <Typography.Text type="secondary">{formatChangeScopeSummary(request.changeSummary)}</Typography.Text>
                {review && (
                  <Typography.Text type="secondary">
                    {request.submitterName ?? "Unknown user"} · Submitted {formatDateTime(request.submittedOn)}
                  </Typography.Text>
                )}
                {!review && request.reviewerNotes && request.status !== "Pending" && (
                  <Typography.Text type="secondary" ellipsis title={request.reviewerNotes}>
                    {request.reviewerNotes}
                  </Typography.Text>
                )}
              </Space>
              <Space direction="vertical" size={4} align="end" style={{ marginLeft: "auto" }}>
                {statusTag(request, review)}
                <Typography.Text type="secondary" style={{ fontSize: 12, whiteSpace: "nowrap" }}>
                  Updated {formatDateTime(request.updatedOn ?? request.submittedOn)}
                </Typography.Text>
              </Space>
            </div>
          </Link>
        </List.Item>
      )}
    />
  );
};
