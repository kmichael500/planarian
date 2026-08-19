import { Empty, List, Space, Tag, Typography, theme } from "antd";
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

const statusTag = (request: CaveChangeRequestSummaryVm) => <Space size={4} wrap>
  {request.status === "Approved" ? <Tag color="success">Approved</Tag>
    : request.status === "Rejected" ? <Tag color="error">Rejected</Tag>
      : <Tag color="processing">Pending review</Tag>}
  {request.isStale && <Tag color="warning">Needs re-review</Tag>}
</Space>;

const EmptyState = ({ review }: { review: boolean }) => (
  <Empty
    image={Empty.PRESENTED_IMAGE_SIMPLE}
    description={
      <Space direction="vertical" size={2}>
        <Typography.Text strong>{review ? "No review submissions found" : "No submissions found"}</Typography.Text>
        <Typography.Text type="secondary">
          {review ? "Try a different status filter, or check back later." : "Try a different status filter, or submit changes to a Cave."}
        </Typography.Text>
      </Space>
    }
  />
);

export const CaveChangeRequestList = ({ requests, review = false }: {
  requests: CaveChangeRequestSummaryVm[];
  review?: boolean;
}) => {
  const { token } = theme.useToken();
  if (!requests.length) return <EmptyState review={review} />;

  return (
    <List
      split={false}
      dataSource={requests}
      renderItem={(request) => (
        <List.Item key={request.id} style={{ padding: 0, marginBottom: token.marginSM,
          border: `1px solid ${token.colorBorderSecondary}`, borderRadius: token.borderRadiusLG,
          background: token.colorBgContainer, overflow: "hidden" }}>
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
                {statusTag(request)}
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
