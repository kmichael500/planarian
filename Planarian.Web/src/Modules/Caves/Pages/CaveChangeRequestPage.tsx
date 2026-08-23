import { useContext, useEffect, useState } from "react";
import { CheckOutlined, CloseOutlined, EditOutlined, EyeOutlined, FolderOpenOutlined } from "@ant-design/icons";
import { Alert, Card, Collapse, Divider, Grid, Input, message, Space, Spin, Tag, theme, Typography } from "antd";
import { useNavigate, useParams } from "react-router-dom";
import { AppContext } from "../../../Configuration/Context/AppContext";
import { ClientRoutes } from "../../../Configuration/Routing/ClientRoutes.generated";
import { BackButtonComponent } from "../../../Shared/Components/Buttons/BackButtonComponent";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
import { PlanarianModal } from "../../../Shared/Components/Buttons/PlanarianModal";
import { formatDateTime } from "../../../Shared/Helpers/StringHelpers";
import { downloadFile } from "../../../Shared/Helpers/FileHelpers";
import { CaveRevisionDiff, CaveRevisionDiffAssetActions } from "../Components/CaveRevisionDiff";
import { CaveLinePlotViewer } from "../Components/CaveLinePlotViewer";
import { useLikelyCountyNumber } from "../Components/useLikelyCountyNumber";
import { formatChangeScopeSummary } from "../Components/CaveChangeRequestList";
import { CaveStaleReviewContext, CaveStaleReviewNotice } from "../Components/CaveStaleReviewContext";
import { FileViewer } from "../../Files/Components/FileViewerComponent";
import { FileService } from "../../Files/Services/FileService";
import { getFileType } from "../../Files/Services/FileHelpers";
import { CaveChangeRequestDetailVm, CaveChangeRequestSummaryVm, CaveProposalVersionDetailVm } from "../Models/CaveChangeRequestVm";
import { CaveSnapshotVm } from "../Models/CaveRevisionVm";
import { CaveService } from "../Service/CaveService";

export const CaveAvailability = ({ request }: { request: CaveChangeRequestSummaryVm }) => {
  const navigate = useNavigate();
  return request.caveExists
    ? <PlanarianButton icon={<FolderOpenOutlined />} alwaysShowChildren aria-label="Open Cave"
      onClick={() => navigate(`/caves/${request.caveId}`)}>Open Cave</PlanarianButton>
    : <Typography.Text type="secondary">Cave no longer available</Typography.Text>;
};

export const UnavailableProposalFilesAlert = ({ fileIds, snapshots }: {
  fileIds: string[];
  snapshots: Array<CaveSnapshotVm | undefined>;
}) => fileIds.length > 0 ? <Alert type="warning" showIcon message="Historical attachment unavailable"
  description={fileIds.map(fileId => {
    const file = snapshots.flatMap(snapshot => snapshot?.files ?? [])
      .find(candidate => candidate.id === fileId);
    return file ? `${file.name}${file.extension}` : fileId;
  }).join(", ")} /> : null;

export const ProposalVersionComparison = ({ detail }: { detail: CaveProposalVersionDetailVm }) => <Space
  direction="vertical" size="middle" style={{ width: "100%" }}>
  <UnavailableProposalFilesAlert fileIds={detail.unavailableStagedFileIds}
    snapshots={[detail.proposed, detail.previousProposed]} />
  {detail.baseRevisionChanged &&
    <Alert type="info" showIcon message="This version was revised against a newer published Cave."
      description="The comparison below shows the effective requested changes for this version." />}
  {detail.diffFromPreviousVersion && detail.previousProposed ? <>
    <Typography.Title level={5} style={{ margin: 0 }}>Changes in this version</Typography.Title>
    <CaveRevisionDiff diff={detail.diffFromPreviousVersion}
      previous={detail.previousProposed} current={detail.proposed}
      countyNumberIntent={detail.countyNumberIntent}
      proposalCountyNumberChange={detail.countyNumberChange} likelyCountyNumber={detail.likelyCountyNumber} />
    <Collapse ghost items={[{
      key: "base",
      label: "Compare this version with the published Cave",
      children: <CaveRevisionDiff diff={detail.diffFromBase}
        previous={detail.base} current={detail.proposed}
        countyNumberIntent={detail.countyNumberIntent} likelyCountyNumber={detail.likelyCountyNumber} />,
    }]} />
  </> : <>
    <Typography.Title level={5} style={{ margin: 0 }}>Initial requested changes</Typography.Title>
    <CaveRevisionDiff diff={detail.diffFromBase}
      previous={detail.base} current={detail.proposed}
      countyNumberIntent={detail.countyNumberIntent} likelyCountyNumber={detail.likelyCountyNumber} />
  </>}
</Space>;

export const CaveChangeRequestPage = () => {
  const { requestId } = useParams();
  const navigate = useNavigate();
  const { setHeaderTitle, setHeaderButtons } = useContext(AppContext);
  const screens = Grid.useBreakpoint();
  const { token } = theme.useToken();
  const [detail, setDetail] = useState<CaveChangeRequestDetailVm>();
  const [loading, setLoading] = useState(true);
  const [loadFailed, setLoadFailed] = useState(false);
  const [deciding, setDeciding] = useState(false);
  const [decisionDialog, setDecisionDialog] = useState<"approve" | "requestChanges" | "reject">();
  const [approvalNote, setApprovalNote] = useState("");
  const [requestedChangesMessage, setRequestedChangesMessage] = useState("");
  const [rejectionReason, setRejectionReason] = useState("");
  const [rejectionError, setRejectionError] = useState<string>();
  const [versionDetails, setVersionDetails] = useState<Record<string, CaveProposalVersionDetailVm>>({});
  const [loadingVersionId, setLoadingVersionId] = useState<string>();
  const [activeFileId, setActiveFileId] = useState<string>();
  const [activeLinePlot, setActiveLinePlot] = useState<NonNullable<CaveChangeRequestDetailVm["linePlots"]>[number]>();

  const likelyCountyNumber = useLikelyCountyNumber(detail);

  useEffect(() => {
    setHeaderTitle(["Cave Changes"]);
    setHeaderButtons([<BackButtonComponent key="back" to={ClientRoutes.caveChanges.path} />]);
    if (!requestId) { setLoading(false); setLoadFailed(true); return; }
    CaveService.GetChangeRequest(requestId).then(setDetail)
      .catch(() => { setLoadFailed(true); message.error("The change request could not be loaded."); })
      .finally(() => setLoading(false));
  }, [requestId, setHeaderButtons, setHeaderTitle]);

  useEffect(() => {
    const request = detail?.request;
    if (!request) return;
    setHeaderTitle([`Review ${request.caveName}`]);
    setHeaderButtons([
      <CaveAvailability key="open-cave" request={request} />,
      ...(((request.status === "Pending" && (request.canEdit || request.canReview)) ||
        (request.status === "ChangesRequested" && request.canEdit)) ? [
        <PlanarianButton key="revise" icon={<EditOutlined />} alwaysShowChildren
          aria-label={request.isStale ? "Re-review changes" : "Revise changes"}
          type={request.isStale ? "primary" : "default"}
          onClick={() => navigate(ClientRoutes.caveChangeRequestRevise.get(request.id))}>
          {request.isStale ? "Re-review changes" : "Revise changes"}
        </PlanarianButton>,
      ] : []),
      <BackButtonComponent key="back" to={ClientRoutes.caveChanges.path} />,
    ]);
  }, [detail?.request, navigate, setHeaderButtons, setHeaderTitle]);

  const refreshAfterConflict = async () => {
    if (!requestId) return;
    setDetail(await CaveService.GetChangeRequest(requestId));
    setDecisionDialog(undefined);
  };

  const decide = async (decision: { kind: "approve"; note?: string } |
    { kind: "requestChanges"; message: string } | { kind: "reject"; reason: string }) => {
    if (!requestId || !detail) return;
    setDeciding(true);
    try {
      const expectedProposalVersionId = detail.request.currentProposalVersionId;
      if (decision.kind === "approve")
        await CaveService.ApproveChangeRequest(requestId, expectedProposalVersionId, decision.note);
      else if (decision.kind === "requestChanges")
        await CaveService.RequestChangeRequestChanges(requestId, expectedProposalVersionId, decision.message);
      else await CaveService.RejectChangeRequest(requestId, expectedProposalVersionId, decision.reason);
      message.success(decision.kind === "approve" ? "Changes approved and published."
        : decision.kind === "requestChanges" ? "Changes sent back to the submitter." : "Change request rejected.");
      setDetail(await CaveService.GetChangeRequest(requestId));
      setDecisionDialog(undefined);
    } catch (error: any) {
      if (error?.conflictKind === "ActiveProposalVersionChanged") {
        message.warning("The proposal changed while you were reviewing it. Review the current version before making a decision.");
        await refreshAfterConflict();
      } else if (error?.conflictKind === "PublishedCaveChanged") {
        message.warning("The published Cave changed. Revise the proposal against the current Cave before approval.");
        await refreshAfterConflict();
      } else message.error("The review decision could not be saved.");
    } finally { setDeciding(false); }
  };

  const reject = async () => {
    const reason = rejectionReason.trim();
    if (!reason) {
      setRejectionError("Reason for rejection is required.");
      return;
    }
    setRejectionError(undefined);
    await decide({ kind: "reject", reason });
  };


  const requestChanges = async () => {
    const feedback = requestedChangesMessage.trim();
    if (!feedback) return;
    await decide({ kind: "requestChanges", message: feedback });
  };

  const resubmit = async () => {
    if (!requestId || !detail) return;
    setDeciding(true);
    try {
      await CaveService.ResubmitChangeRequest(requestId, detail.request.currentProposalVersionId);
      message.success("Changes resubmitted for review.");
      setDetail(await CaveService.GetChangeRequest(requestId));
    } catch (error: any) {
      if (error?.conflictKind === "ActiveProposalVersionChanged") {
        message.warning("The proposal changed. Reload and review the latest draft before resubmitting.");
        await refreshAfterConflict();
      } else message.error("The changes could not be resubmitted.");
    } finally { setDeciding(false); }
  };

  const toggleVersion = async (versionId: string) => {
    if (versionDetails[versionId]) {
      setVersionDetails(current => { const next = { ...current }; delete next[versionId]; return next; });
      return;
    }
    if (!requestId) return;
    setLoadingVersionId(versionId);
    try {
      const loadedVersion = await CaveService.GetProposalVersion(requestId, versionId);
      setVersionDetails(current => ({ ...current, [versionId]: loadedVersion }));
    } catch { message.error("That proposal version could not be loaded."); }
    finally { setLoadingVersionId(undefined); }
  };

  if (loading && !detail) return <div role="status" aria-label="Loading change request">
    <Space><Spin size="small" /><Typography.Text type="secondary">Loading change request…</Typography.Text></Space>
  </div>;
  if (!detail) return <Alert type="error" showIcon message="Change request unavailable"
    description={loadFailed ? "The change request could not be loaded." : undefined} />;

  const request = detail.request;
  const submitted = formatDateTime(request.submittedOn) ?? "Unknown time";
  const updated = request.updatedOn ? formatDateTime(request.updatedOn) : undefined;
  const reviewed = request.reviewedOn ? formatDateTime(request.reviewedOn) : undefined;
  const reviewEvents = detail.reviewEvents ?? [];
  const latestChangesRequested = [...reviewEvents].reverse()
    .find(reviewEvent => reviewEvent.action === "ChangesRequested");
  const canResubmit = request.status === "ChangesRequested" && request.canEdit && !!latestChangesRequested &&
    request.currentProposalVersionId !== latestChangesRequested.proposalVersionId;
  const isFinalDecision = request.status === "Approved" || request.status === "Rejected";
  const statusColor = request.status === "Approved" ? "success" : request.status === "Rejected" ? "error"
    : request.status === "ChangesRequested" ? "warning" : "processing";
  const statusLabel = request.status === "Pending" ? "Pending review"
    : request.status === "ChangesRequested" ? "Changes requested" : request.status;
  const versionNumbers = new Map(detail.versions.map((version, index) => [version.id, index + 1]));

  const fileById = new Map([...detail.base.files, ...detail.current.files, ...detail.proposed.files]
    .map(file => [file.id, file]));
  const stagedFileIds = new Set(detail.activeStagedFiles.map(file => file.id));
  const proposalLinePlots = [...(detail.linePlots ?? []), ...(detail.removedLinePlots ?? [])];
  const unavailableFileIds = new Set(detail.unavailableStagedFileIds);
  const activeFile = activeFileId ? fileById.get(activeFileId) : undefined;
  const activeFileIsStaged = !!activeFile && stagedFileIds.has(activeFile.id);
  const assetActions: CaveRevisionDiffAssetActions = {
    files: Object.fromEntries([...fileById.values()].map(file => {
      const unavailable = unavailableFileIds.has(file.id);
      const staged = stagedFileIds.has(file.id);
      return [file.id, {
        unavailable,
        onView: unavailable ? undefined : () => setActiveFileId(file.id),
        onDownload: unavailable ? undefined : () => staged
          ? CaveService.StartStagedChangeRequestFileDownload(request.id, file.id)
          : FileService.startFileDownload(file.id),
      }];
    })),
    linePlots: Object.fromEntries(proposalLinePlots
      .filter(linePlot => !!linePlot.id)
      .map(linePlot => [linePlot.id!, {
        onView: () => setActiveLinePlot(linePlot),
        onDownload: () => downloadFile(`${linePlot.name}.geojson`, linePlot.geoJson),
      }])),
  };

  return <Space direction="vertical" size="middle" style={{ width: "100%" }}>
    <UnavailableProposalFilesAlert fileIds={detail.unavailableStagedFileIds}
      snapshots={[detail.proposed, detail.current, detail.base]} />
    {request.isStale && <CaveStaleReviewNotice />}
    {request.status === "ChangesRequested" && <Alert type="warning" showIcon message="Changes requested"
      description={<Space direction="vertical" size={2}>
        {latestChangesRequested?.message && <Typography.Text>{latestChangesRequested.message}</Typography.Text>}
        <Typography.Text type="secondary">
          {canResubmit ? "A newer draft is saved. Resubmit it when you are ready for another review."
            : "Revise the proposal and save a new draft before resubmitting it for review."}
        </Typography.Text>
      </Space>} />}

    <section aria-label="Change request">
      <Card>
        <Space direction="vertical" size="middle" style={{ width: "100%" }}>
          <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center",
            gap: token.marginSM, flexWrap: "wrap" }}>
            <Space wrap size="small" align="center">
              {request.status !== "Pending" && <Tag color={statusColor}>{statusLabel}</Tag>}
              {request.isStale && <Tag color="warning">Needs re-review</Tag>}
              <Typography.Text type="secondary">{formatChangeScopeSummary(request.changeSummary)}</Typography.Text>
            </Space>
            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
              {request.submitterName ?? "Unknown user"} · submitted {submitted}{updated ? ` · updated ${updated}` : ""}
            </Typography.Text>
          </div>

          {isFinalDecision && reviewEvents.length === 0 && <div>
            <Typography.Text type="secondary">
              {request.reviewerName ?? "Unknown reviewer"}{reviewed ? ` · reviewed ${reviewed}` : ""}
            </Typography.Text>
            {request.reviewerNotes && <div style={{ marginTop: token.marginXS }}>
              <Typography.Text type="secondary">{request.status === "Rejected" ? "Reason for rejection" : "Reviewer note"}</Typography.Text>
              <Typography.Paragraph style={{ marginBottom: 0 }}>{request.reviewerNotes}</Typography.Paragraph>
            </div>}
          </div>}

          {request.isStale && <CaveStaleReviewContext detail={detail} assetActions={assetActions} />}

          <CaveRevisionDiff diff={detail.diff} previous={detail.base} current={detail.proposed}
            countyNumberIntent={detail.countyNumberIntent} likelyCountyNumber={likelyCountyNumber}
            assetActions={assetActions} showSummary={false} />

          {request.status === "Pending" && request.canReview && <>
            <Divider style={{ margin: `${token.marginXS}px 0 0` }} />
            <div role="region" aria-label="Reviewer actions" style={{ display: "flex", justifyContent: "flex-end" }}>
              <Space direction={screens.md ? "horizontal" : "vertical"} size="small" style={{ width: screens.md ? undefined : "100%" }}>
                <PlanarianButton icon={<CheckOutlined />} alwaysShowChildren block={!screens.md} type="primary"
                  aria-label="Approve and publish" disabled={request.isStale} onClick={() => setDecisionDialog("approve")}>
                  Approve and publish
                </PlanarianButton>
                <PlanarianButton icon={<EditOutlined />} alwaysShowChildren block={!screens.md}
                  aria-label="Request changes" onClick={() => setDecisionDialog("requestChanges")}>
                  Request changes
                </PlanarianButton>
                <PlanarianButton icon={<CloseOutlined />} alwaysShowChildren block={!screens.md} danger
                  aria-label="Reject" onClick={() => { setRejectionError(undefined); setDecisionDialog("reject"); }}>
                  Reject
                </PlanarianButton>
              </Space>
            </div>
          </>}

          {request.status === "ChangesRequested" && request.canEdit && <>
            <Divider style={{ margin: `${token.marginXS}px 0 0` }} />
            <div role="region" aria-label="Author actions" style={{ display: "flex", justifyContent: "flex-end" }}>
              <Space direction={screens.md ? "horizontal" : "vertical"} size="small"
                style={{ width: screens.md ? undefined : "100%", alignItems: screens.md ? "center" : undefined }}>
                {!canResubmit && <Typography.Text type="secondary">Save a new draft before resubmitting.</Typography.Text>}
                <PlanarianButton icon={<CheckOutlined />} alwaysShowChildren block={!screens.md} type="primary"
                  aria-label="Resubmit for review" disabled={!canResubmit} loading={deciding} onClick={resubmit}>
                  Resubmit for review
                </PlanarianButton>
              </Space>
            </div>
          </>}
        </Space>
      </Card>
    </section>

    {reviewEvents.length > 0 && <section aria-label="Review history">
      <Card size="small" title="Review history">
        <Space direction="vertical" size="small" style={{ width: "100%" }}>
          {reviewEvents.map((reviewEvent, index) => {
            const label = reviewEvent.action === "ChangesRequested" ? "Changes requested"
              : reviewEvent.action === "Resubmitted" ? "Resubmitted for review" : reviewEvent.action;
            const color = reviewEvent.action === "Approved" ? "success" : reviewEvent.action === "Rejected" ? "error"
              : reviewEvent.action === "ChangesRequested" ? "warning" : "processing";
            return <div key={reviewEvent.id} style={{
              paddingTop: index ? token.paddingXS : 0,
              borderTop: index ? `1px solid ${token.colorSplit}` : undefined,
            }}>
              <Space direction="vertical" size={2}>
                <Space wrap size="small">
                  <Tag color={color}>{label}</Tag>
                  <Typography.Text type="secondary">
                    {reviewEvent.actorName ?? "Unknown user"} · {formatDateTime(reviewEvent.createdOn) ?? "Unknown time"}
                  </Typography.Text>
                </Space>
                {reviewEvent.message && <Typography.Paragraph style={{ marginBottom: 0 }}>
                  {reviewEvent.message}
                </Typography.Paragraph>}
              </Space>
            </div>;
          })}
        </Space>
      </Card>
    </section>}

    {detail.versions.length > 1 && <section aria-label="Proposal history">
      <Card size="small" title="Proposal history">
        {detail.versions.slice().reverse().map((version, index) => {
          const versionNumber = versionNumbers.get(version.id)!;
          const versionDate = formatDateTime(version.createdOn) ?? "Unknown time";
          const expanded = !!versionDetails[version.id];
          return <div key={version.id} style={{
            padding: `${token.paddingSM}px 0`,
            borderTop: index ? `1px solid ${token.colorSplit}` : undefined,
          }}>
            <Space direction="vertical" size="small" style={{ width: "100%" }}>
              <Space wrap align="center" style={{ width: "100%", justifyContent: "space-between" }}>
                <Space wrap size="small">
                  <Typography.Text>Version {versionNumber} · {version.createdByName ?? "Unknown user"} · {versionDate}</Typography.Text>
                  {version.isCurrent && <Tag color="processing">Current</Tag>}
                </Space>
                <PlanarianButton icon={<EyeOutlined />} alwaysShowChildren loading={loadingVersionId === version.id}
                  aria-label={`${expanded ? "Hide changes for" : "View changes for"} Version ${versionNumber}`}
                  onClick={() => toggleVersion(version.id)}>
                  {expanded ? "Hide changes" : "View changes"}
                </PlanarianButton>
              </Space>
              {expanded && <ProposalVersionComparison detail={versionDetails[version.id]} />}
            </Space>
          </div>;
        })}
      </Card>
    </section>}

    <FileViewer
      open={!!activeFile}
      onCancel={() => setActiveFileId(undefined)}
      fileId={activeFile?.id}
      name={activeFile ? `${activeFile.name}${activeFile.extension}` : undefined}
      fileType={getFileType(activeFile?.extension)}
      viewUrl={activeFileIsStaged && activeFile
        ? CaveService.GetStagedChangeRequestFileUrl(request.id, activeFile.id, "view") : undefined}
      onDownload={activeFileIsStaged && activeFile
        ? () => CaveService.StartStagedChangeRequestFileDownload(request.id, activeFile.id) : undefined}
    />
    <CaveLinePlotViewer linePlot={activeLinePlot} open={!!activeLinePlot}
      onClose={() => setActiveLinePlot(undefined)} />

    <PlanarianModal open={decisionDialog === "approve"} onClose={() => setDecisionDialog(undefined)}
      header="Approve and publish?" footer={[
        <PlanarianButton key="cancel" icon={undefined} alwaysShowChildren onClick={() => setDecisionDialog(undefined)}>Cancel</PlanarianButton>,
        <PlanarianButton key="approve" icon={<CheckOutlined />} alwaysShowChildren type="primary" loading={deciding}
          aria-label="Approve and publish" onClick={() => decide({ kind: "approve", note: approvalNote.trim() || undefined })}>
          Approve and publish
        </PlanarianButton>,
      ]}>
      <Space direction="vertical" size="middle" style={{ width: "100%" }}>
        <Typography.Paragraph style={{ marginBottom: 0 }}>
          This will publish the requested changes to the Cave record. Review the changes above before continuing.
        </Typography.Paragraph>
        <div>
          <label htmlFor="approval-note"><Typography.Text>Reviewer note (optional)</Typography.Text></label>
          <Input.TextArea id="approval-note" value={approvalNote} onChange={event => setApprovalNote(event.target.value)} rows={3} />
        </div>
      </Space>
    </PlanarianModal>

    <PlanarianModal open={decisionDialog === "requestChanges"} onClose={() => setDecisionDialog(undefined)}
      header="Request changes" footer={[
        <PlanarianButton key="cancel" icon={undefined} alwaysShowChildren onClick={() => setDecisionDialog(undefined)}>Cancel</PlanarianButton>,
        <PlanarianButton key="request" icon={<EditOutlined />} alwaysShowChildren type="primary" loading={deciding}
          aria-label="Request changes" disabled={!requestedChangesMessage.trim()} onClick={requestChanges}>Request changes</PlanarianButton>,
      ]}>
      <Space direction="vertical" size="middle" style={{ width: "100%" }}>
        <Typography.Paragraph style={{ marginBottom: 0 }}>
          The submitter can revise the proposal and explicitly resubmit it when it is ready for another review.
        </Typography.Paragraph>
        <div>
          <label htmlFor="requested-changes-message"><Typography.Text>Message to submitter</Typography.Text></label>
          <Input.TextArea id="requested-changes-message" value={requestedChangesMessage}
            onChange={event => setRequestedChangesMessage(event.target.value)} rows={3} />
        </div>
      </Space>
    </PlanarianModal>

    <PlanarianModal open={decisionDialog === "reject"} onClose={() => setDecisionDialog(undefined)}
      header="Reject requested changes" footer={[
        <PlanarianButton key="cancel" icon={undefined} alwaysShowChildren onClick={() => setDecisionDialog(undefined)}>Cancel</PlanarianButton>,
        <PlanarianButton key="reject" icon={<CloseOutlined />} alwaysShowChildren danger loading={deciding}
          aria-label="Reject changes" onClick={reject}>Reject changes</PlanarianButton>,
      ]}>
      <Space direction="vertical" size="middle" style={{ width: "100%" }}>
        <Typography.Paragraph style={{ marginBottom: 0 }}>
          The submitter will see this reason. The Cave record will not be changed.
        </Typography.Paragraph>
        <div>
          <label htmlFor="rejection-reason"><Typography.Text>Reason for rejection</Typography.Text></label>
          <Input.TextArea id="rejection-reason" value={rejectionReason}
            aria-invalid={!!rejectionError} aria-describedby={rejectionError ? "rejection-reason-error" : undefined}
            onChange={event => { setRejectionReason(event.target.value); if (event.target.value.trim()) setRejectionError(undefined); }} rows={3} />
          {rejectionError && <div id="rejection-reason-error" role="alert">
            <Typography.Text type="danger">{rejectionError}</Typography.Text>
          </div>}
        </div>
      </Space>
    </PlanarianModal>
  </Space>;
};
