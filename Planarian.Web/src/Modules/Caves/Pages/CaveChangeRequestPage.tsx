import { useContext, useEffect, useState } from "react";
import { Alert, Card, Input, message, Space, Spin, Tag, Typography, Upload } from "antd";
import { UploadOutlined } from "@ant-design/icons";
import { RcFile } from "antd/lib/upload";
import { Link, useParams } from "react-router-dom";
import { AppContext } from "../../../Configuration/Context/AppContext";
import { BackButtonComponent } from "../../../Shared/Components/Buttons/BackButtonComponent";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
import { CaveRevisionDiff } from "../Components/CaveRevisionDiff";
import { CaveChangeRequestDetailVm } from "../Models/CaveChangeRequestVm";
import { CaveService } from "../Service/CaveService";

export const CaveChangeRequestPage = () => {
  const { requestId } = useParams();
  const { setHeaderTitle, setHeaderButtons } = useContext(AppContext);
  const [detail, setDetail] = useState<CaveChangeRequestDetailVm>();
  const [notes, setNotes] = useState("");
  const [loading, setLoading] = useState(true);
  const [deciding, setDeciding] = useState(false);

  const stagedFiles = detail ? detail.proposed.files.filter(file =>
    !detail.base.files.some(baseFile => baseFile.id === file.id)) : [];

  useEffect(() => {
    setHeaderTitle(["Cave Change Request"]);
    setHeaderButtons([<BackButtonComponent to="/caves/requests" />]);
    if (!requestId) return;
    CaveService.GetChangeRequest(requestId).then(setDetail)
      .catch(() => message.error("The change request could not be loaded."))
      .finally(() => setLoading(false));
  }, [requestId]);

  const decide = async (approve: boolean) => {
    if (!requestId) return;
    setDeciding(true);
    try {
      const result = approve ? await CaveService.ApproveChangeRequest(requestId, notes)
        : await CaveService.RejectChangeRequest(requestId, notes);
      if (result.result === "Conflict") {
        message.warning("The Cave changed again. Revise the proposal against the current Cave before approval.");
        setDetail(await CaveService.GetChangeRequest(requestId));
      } else {
        message.success(approve ? "Changes approved and published." : "Change request rejected.");
        setDetail(await CaveService.GetChangeRequest(requestId));
      }
    } catch (error: any) {
      if (error?.response?.status === 409) {
        message.warning("This request is stale and cannot be approved over the current Cave.");
        setDetail(await CaveService.GetChangeRequest(requestId));
      } else message.error("The review decision could not be saved.");
    } finally { setDeciding(false); }
  };

  return <Spin spinning={loading}>{detail && <Space direction="vertical" style={{ width: "100%" }}>
    {detail.request.isStale && <Alert type="warning" showIcon message="This proposal version is based on an older Cave revision." description="Review both comparisons, then create a new proposal version from the current published Cave." />}
    <Card title={detail.request.caveName} extra={<Link to={`/caves/${detail.request.caveId}`}>Open Cave</Link>}>
      <Space direction="vertical">
        <Space><Tag>{detail.request.status}</Tag>{detail.request.isStale && <Tag color="warning">Conflict</Tag>}</Space>
        <Typography.Text>Submitted by {detail.request.submitterName ?? "Unknown user"}</Typography.Text>
        {detail.request.reviewerName && <Typography.Text>Reviewed by {detail.request.reviewerName}</Typography.Text>}
        {detail.request.reviewerNotes && <Alert message={detail.request.reviewerNotes} type={detail.request.status === "Rejected" ? "error" : "info"} />}
        {detail.request.status === "Pending" && (detail.request.canEdit || detail.request.canReview) &&
          <Link to={`/caves/requests/${detail.request.id}/revise`}>
            <PlanarianButton icon={undefined} type={detail.request.isStale ? "primary" : "default"}>
              {detail.request.isStale ? "Revise against current Cave" : "Revise proposal"}
            </PlanarianButton>
          </Link>}
      </Space>
    </Card>
    <Card title="Base → proposed"><CaveRevisionDiff diff={detail.diff} previous={detail.base} current={detail.proposed} /></Card>
    {detail.request.status === "Pending" && (detail.request.canEdit || detail.request.canReview || stagedFiles.length > 0) && <Card title="Proposal files">
      <Typography.Paragraph type="secondary">Uploaded files remain staged and unpublished until this request is approved.</Typography.Paragraph>
      {stagedFiles.map(file => <div key={file.id}><Typography.Link href={`/api/cave-change-requests/${detail.request.id}/files/${file.id}`}>{file.displayName ?? file.fileName}</Typography.Link></div>)}
      {(detail.request.canEdit || detail.request.canReview) && <Upload showUploadList={false} customRequest={async ({ file, onSuccess, onError, onProgress }) => {
        try {
          await CaveService.StageChangeRequestFile(requestId!, file as RcFile, (file as RcFile).uid, event => {
            onProgress?.({ percent: Math.round(100 * event.loaded / (event.total ?? event.loaded)) });
          });
          onSuccess?.({});
          message.success("File staged for review.");
          setDetail(await CaveService.GetChangeRequest(requestId!));
        } catch (error) {
          onError?.(error as Error);
          message.error("The file could not be staged.");
        }
      }}>
        <PlanarianButton icon={<UploadOutlined />}>Add staged file</PlanarianButton>
      </Upload>}
    </Card>}
    {detail.request.isStale && detail.publishedSinceBase && <Card title="What changed in the published Cave after submission"><CaveRevisionDiff diff={detail.publishedSinceBase} previous={detail.base} current={detail.current} /></Card>}
    {detail.versions.length > 1 && <Card title="Proposal versions">
      <Space direction="vertical">{detail.versions.slice().reverse().map(version =>
        <Typography.Text key={version.id}>
          {version.isCurrent ? "Current: " : ""}{version.id} · based on revision {version.baseRevisionId} · {version.createdByName ?? "Unknown user"} · {new Date(version.createdOn).toLocaleString()}
        </Typography.Text>)}</Space>
    </Card>}
    {detail.request.status === "Pending" && detail.request.canReview && <Card title="Review decision">
      <Input.TextArea value={notes} onChange={(event) => setNotes(event.target.value)} placeholder="Reason or reviewer notes" rows={3} />
      <Space style={{ marginTop: 12 }}>
        <PlanarianButton icon={undefined} type="primary" disabled={detail.request.isStale} loading={deciding} onClick={() => decide(true)}>Approve and publish</PlanarianButton>
        <PlanarianButton icon={undefined} loading={deciding} onClick={() => decide(false)}>Reject</PlanarianButton>
      </Space>
    </Card>}
  </Space>}</Spin>;
};
