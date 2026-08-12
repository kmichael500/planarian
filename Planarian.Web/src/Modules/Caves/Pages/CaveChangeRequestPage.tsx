import { useContext, useEffect, useState } from "react";
import { Alert, Card, Collapse, Input, message, Space, Spin, Tag, Typography, Upload } from "antd";
import { UploadOutlined } from "@ant-design/icons";
import { RcFile } from "antd/lib/upload";
import { Link, useParams } from "react-router-dom";
import { AppContext } from "../../../Configuration/Context/AppContext";
import { BackButtonComponent } from "../../../Shared/Components/Buttons/BackButtonComponent";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
import { CaveRevisionDiff } from "../Components/CaveRevisionDiff";
import { CaveChangeRequestDetailVm, CaveChangeRequestSummaryVm, CaveProposalVersionDetailVm } from "../Models/CaveChangeRequestVm";
import { CaveService } from "../Service/CaveService";

export const CaveAvailability = ({ request }: { request: CaveChangeRequestSummaryVm }) =>
  request.caveExists
    ? <Link to={`/caves/${request.caveId}`}>Open Cave</Link>
    : <Typography.Text type="secondary">Cave no longer available</Typography.Text>;

export const ProposalVersionComparison = ({ detail }: { detail: CaveProposalVersionDetailVm }) => <>
  {detail.unavailableStagedFileIds.length > 0 &&
    <Alert type="warning" showIcon message="Historical attachment unavailable"
      description={detail.unavailableStagedFileIds.map(fileId => {
        const file = detail.proposed.files.find(candidate => candidate.id === fileId)
          ?? detail.previousProposed?.files.find(candidate => candidate.id === fileId);
        return file?.displayName ?? file?.fileName ?? fileId;
      }).join(", ")} />}
  {detail.baseRevisionChanged &&
    <Alert type="info" showIcon message="This proposal version is based on a newer published Cave revision."
      description={`Previous base: ${detail.previousBaseRevisionId}. This base: ${detail.baseRevisionId}. The comparison below shows effective proposal state, not author attribution.`} />}
  {detail.diffFromPreviousVersion && detail.previousProposed ? <>
    <Typography.Title level={5}>Changes from previous proposal version</Typography.Title>
    <CaveRevisionDiff diff={detail.diffFromPreviousVersion}
      previous={detail.previousProposed} current={detail.proposed}
      countyNumberIntent={detail.countyNumberIntent} />
    <Collapse ghost items={[{
      key: "base",
      label: "This version vs published base",
      children: <CaveRevisionDiff diff={detail.diffFromBase}
        previous={detail.base} current={detail.proposed}
        countyNumberIntent={detail.countyNumberIntent} />,
    }]} />
  </> : <>
    <Typography.Title level={5}>Initial proposal</Typography.Title>
    <Typography.Text type="secondary">Published base → proposal</Typography.Text>
    <CaveRevisionDiff diff={detail.diffFromBase}
      previous={detail.base} current={detail.proposed}
      countyNumberIntent={detail.countyNumberIntent} />
  </>}
</>;

export const CaveChangeRequestPage = () => {
  const { requestId } = useParams();
  const { setHeaderTitle, setHeaderButtons } = useContext(AppContext);
  const [detail, setDetail] = useState<CaveChangeRequestDetailVm>();
  const [notes, setNotes] = useState("");
  const [loading, setLoading] = useState(true);
  const [deciding, setDeciding] = useState(false);
  const [versionDetails, setVersionDetails] = useState<Record<string, CaveProposalVersionDetailVm>>({});
  const [loadingVersionId, setLoadingVersionId] = useState<string>();

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
      const expectedProposalVersionId = detail!.request.currentProposalVersionId;
      if (approve) await CaveService.ApproveChangeRequest(requestId, expectedProposalVersionId, notes);
      else await CaveService.RejectChangeRequest(requestId, expectedProposalVersionId, notes);
      message.success(approve ? "Changes approved and published." : "Change request rejected.");
      setDetail(await CaveService.GetChangeRequest(requestId));
    } catch (error: any) {
      if (error?.response?.data?.conflictKind === "ActiveProposalVersionChanged") {
        message.warning("The proposal changed while you were reviewing it. Review the current version before making a decision.");
        setDetail(await CaveService.GetChangeRequest(requestId));
      } else if (error?.response?.data?.conflictKind === "PublishedCaveChanged") {
        message.warning("The published Cave changed. Revise the proposal against the current Cave before approval.");
        setDetail(await CaveService.GetChangeRequest(requestId));
      } else message.error("The review decision could not be saved.");
    } finally { setDeciding(false); }
  };

  return <Spin spinning={loading}>{detail && <Space direction="vertical" style={{ width: "100%" }}>
    {detail.request.isStale && <Alert type="warning" showIcon message="This proposal version is based on an older Cave revision." description="Review both comparisons, then create a new proposal version from the current published Cave." />}
    <Card title={detail.request.caveName} extra={<CaveAvailability request={detail.request} />}>
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
    <Card title="Base → proposed"><CaveRevisionDiff diff={detail.diff} previous={detail.base} current={detail.proposed}
      countyNumberIntent={detail.countyNumberIntent} /></Card>
    {detail.request.status === "Pending" && (detail.request.canEdit || detail.request.canReview || stagedFiles.length > 0) && <Card title="Proposal files">
      <Typography.Paragraph type="secondary">Uploaded files remain staged and unpublished until this request is approved.</Typography.Paragraph>
      {stagedFiles.map(file => <div key={file.id}><Typography.Link href={CaveService.GetStagedChangeRequestFileUrl(detail.request.id, file.id)}>{file.displayName ?? file.fileName}</Typography.Link></div>)}
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
      <Space direction="vertical" style={{ width: "100%" }}>{detail.versions.slice().reverse().map(version =>
        <Card key={version.id} size="small" title={`${version.isCurrent ? "Current: " : ""}${version.id}`}
          extra={<PlanarianButton icon={undefined} loading={loadingVersionId === version.id} onClick={async () => {
            if (versionDetails[version.id]) {
              setVersionDetails(current => { const next = { ...current }; delete next[version.id]; return next; });
              return;
            }
            setLoadingVersionId(version.id);
            try {
              const loadedVersion = await CaveService.GetProposalVersion(requestId!, version.id);
              setVersionDetails(current => ({ ...current, [version.id]: loadedVersion }));
            } catch { message.error("That proposal version could not be loaded."); }
            finally { setLoadingVersionId(undefined); }
          }}>{versionDetails[version.id] ? "Hide version details" : version.previousProposalVersionId ? "View changes from previous version" : "View initial proposal"}</PlanarianButton>}>
          <Typography.Text type="secondary">Based on revision {version.baseRevisionId} · {version.createdByName ?? "Unknown user"} · {new Date(version.createdOn).toLocaleString()}</Typography.Text>
          {versionDetails[version.id] && <ProposalVersionComparison detail={versionDetails[version.id]} />}
        </Card>)}</Space>
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
