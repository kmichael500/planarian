import { Alert, Card, Empty, Space, Tag, Typography } from "antd";
import { Link } from "react-router-dom";
import { CaveChangeRequestSummaryVm } from "../Models/CaveChangeRequestVm";
import { formatDateTime } from "../../../Shared/Helpers/StringHelpers";

export const CaveChangeRequestList = ({ requests, review = false }: {
  requests: CaveChangeRequestSummaryVm[];
  review?: boolean;
}) => {
  if (!requests.length) return <Empty description={review ? "No changes to review." : "You have not submitted any changes."} />;
  return <Space direction="vertical" style={{ width: "100%" }}>
    {requests.map((request) => <Card key={request.id} title={<Link to={`/caves/requests/${request.id}`}>{request.caveName}</Link>}>
      <Space direction="vertical">
        <Space wrap>
          <Tag color={request.status === "Approved" ? "success" : request.status === "Rejected" ? "error" : "processing"}>{request.status}</Tag>
          {request.isStale && <Tag color="warning">Conflict — Cave changed</Tag>}
        </Space>
        {review && <Typography.Text>Submitted by {request.submitterName ?? "Unknown user"}</Typography.Text>}
        <Typography.Text type="secondary">Submitted {formatDateTime(request.submittedOn)}</Typography.Text>
        {request.reviewerNotes && <Alert type={request.status === "Rejected" ? "error" : "info"} message={request.reviewerNotes} />}
      </Space>
    </Card>)}
  </Space>;
};
