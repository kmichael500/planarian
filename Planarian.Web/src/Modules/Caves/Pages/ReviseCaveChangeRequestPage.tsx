import { useContext, useEffect, useState } from "react";
import { Alert, Card, Form, message, Space, Spin, Typography } from "antd";
import { useNavigate, useParams } from "react-router-dom";
import { AppContext } from "../../../Configuration/Context/AppContext";
import { ClientRoutes } from "../../../Configuration/Routing/ClientRoutes.generated";
import { BackButtonComponent } from "../../../Shared/Components/Buttons/BackButtonComponent";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
import { UnsavedChangesModal } from "../../../Shared/Components/UnsavedChangesModal";
import { useUnsavedChangesGuard } from "../../../Shared/Hooks/useUnsavedChangesGuard";
import { AddCaveComponent } from "../Components/AddCaveComponent";
import { CaveRevisionDiff, CaveRevisionDiffAssetActions } from "../Components/CaveRevisionDiff";
import { CaveLinePlotViewer } from "../Components/CaveLinePlotViewer";
import { CaveStaleReviewContext, CaveStaleReviewNotice } from "../Components/CaveStaleReviewContext";
import { FileViewer } from "../../Files/Components/FileViewerComponent";
import { FileService } from "../../Files/Services/FileService";
import { getFileType } from "../../Files/Services/FileHelpers";
import { downloadFile } from "../../../Shared/Helpers/FileHelpers";
import { caveToForm, snapshotToForm } from "../Helpers/CaveFormMapper";
import { getCaveAuthoringSubmissionValues } from "../Helpers/CaveAuthoringFormValues";
import { AddCaveVm } from "../Models/AddCaveVm";
import { CaveChangePreviewVm, CaveChangeRequestDetailVm } from "../Models/CaveChangeRequestVm";
import { CaveService } from "../Service/CaveService";

export const ReviseCaveChangeRequestPage = () => {
  const { requestId } = useParams();
  const navigate = useNavigate();
  const { setHeaderTitle, setHeaderButtons } = useContext(AppContext);
  const [form] = Form.useForm<AddCaveVm>();
  const [detail, setDetail] = useState<CaveChangeRequestDetailVm>();
  const [editorValues, setEditorValues] = useState<AddCaveVm>();
  const [expectedBaseRevisionId, setExpectedBaseRevisionId] = useState<string>();
  const [expectedProposalVersionId, setExpectedProposalVersionId] = useState<string>();
  const [draft, setDraft] = useState<AddCaveVm>();
  const [preview, setPreview] = useState<CaveChangePreviewVm>();
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [activeFileId, setActiveFileId] = useState<string>();
  const [activeLinePlot, setActiveLinePlot] = useState<NonNullable<CaveChangePreviewVm["linePlots"]>[number]>();
  const unsaved = useUnsavedChangesGuard();

  useEffect(() => {
    setHeaderTitle(["Revise changes"]);
    setHeaderButtons(requestId
      ? [<BackButtonComponent to={ClientRoutes.caveChangeRequest.get(requestId)} />]
      : []);
    if (!requestId) {
      setLoading(false);
      return;
    }
    const load = async () => {
      try {
        let loaded = await CaveService.GetChangeRequest(requestId);
        let values: AddCaveVm | undefined;
        let baseRevisionId: string | undefined;

        if (loaded.request.isStale) {
          let context = await CaveService.GetProposalAuthoringContext(loaded.request.caveId);
          if (loaded.request.currentRevisionId !== context.expectedBaseRevisionId) {
            loaded = await CaveService.GetChangeRequest(requestId);
            if (loaded.request.isStale && loaded.request.currentRevisionId !== context.expectedBaseRevisionId)
              context = await CaveService.GetProposalAuthoringContext(loaded.request.caveId);
          }
          if (loaded.request.isStale && loaded.request.currentRevisionId !== context.expectedBaseRevisionId)
            throw new Error("CaveChangedWhileLoading");

          if (loaded.request.isStale) {
            values = caveToForm(context.cave, context.linePlots);
            const existingIds = new Set((values.files ?? []).map((file) => file.id));
            values.files = [
              ...(values.files ?? []),
              ...loaded.activeStagedFiles.filter((file) => !existingIds.has(file.id)).map((file) => ({
                id: file.id,
                name: file.name,
                fileTypeTagId: file.fileTypeTagId,
                fileTypeKey: file.fileTypeNameAtRevision,
                extension: file.extension,
              })),
            ];
            baseRevisionId = context.expectedBaseRevisionId;
          }
        }

        if (!loaded.request.isStale) {
          const version = await CaveService.GetProposalVersion(requestId, loaded.request.currentProposalVersionId);
          values = snapshotToForm(version.proposed, version.countyNumberIntent,
            version.requestedCountyNumber, loaded.activeStagedFiles, version.linePlots);
          baseRevisionId = version.baseRevisionId;
        }

        if (!values || !baseRevisionId) throw new Error("ChangeRequestEditorUnavailable");
        setDetail(loaded);
        setHeaderTitle([loaded.request.status === "ChangesRequested" ? "Revise requested changes" : loaded.request.isStale ? "Re-review changes" : "Revise changes"]);
        setExpectedProposalVersionId(loaded.request.currentProposalVersionId);
        setExpectedBaseRevisionId(baseRevisionId);
        setEditorValues(values);
        form.setFieldsValue(values);
      } catch (error) {
        if (error instanceof Error && error.message === "CaveChangedWhileLoading")
          message.warning("The Cave changed while this page was loading. Reload and try again.");
        else message.error("The change request could not be loaded.");
      } finally {
        setLoading(false);
      }
    };
    void load();
  }, [requestId, form, setHeaderButtons, setHeaderTitle]);

  if (!requestId) return null;
  const isRevisable = detail?.request.status === "Pending" || detail?.request.status === "ChangesRequested";
  const latestChangesRequested = [...(detail?.reviewEvents ?? [])].reverse()
    .find(reviewEvent => reviewEvent.action === "ChangesRequested");
  const againstCurrent = detail?.request.isStale === true;
  const detailFiles = detail
    ? [...new Map([...(detail.base.files ?? []), ...(detail.current.files ?? []), ...(detail.proposed.files ?? [])]
      .map(file => [file.id, file])).values()]
    : [];
  const previewFiles = preview
    ? [...new Map([...(preview.base.files ?? []), ...(preview.proposed.files ?? [])]
      .map(file => [file.id, file])).values()]
    : [];
  const reviewFiles = [...new Map([...detailFiles, ...previewFiles].map(file => [file.id, file])).values()];
  const publishedFileIds = new Set([
    ...(detail?.base.files ?? []), ...(detail?.current.files ?? []), ...(preview?.base.files ?? []),
  ].map(file => file.id));
  const existingStagedFileIds = new Set(detail?.activeStagedFiles.map(file => file.id) ?? []);
  const activeFile = reviewFiles.find(file => file.id === activeFileId);
  const detailLinePlots = [...(detail?.linePlots ?? []), ...(detail?.removedLinePlots ?? [])];
  const previewLinePlots = [...(preview?.linePlots ?? []), ...(preview?.removedLinePlots ?? [])];
  const reviewLinePlots = [...new Map([...detailLinePlots, ...previewLinePlots]
    .filter(linePlot => !!linePlot.id).map(linePlot => [linePlot.id!, linePlot])).values()];
  const assetActions: CaveRevisionDiffAssetActions = {
    files: Object.fromEntries(reviewFiles.map(file => [file.id, {
      onView: () => setActiveFileId(file.id),
      onDownload: () => publishedFileIds.has(file.id) ? FileService.startFileDownload(file.id)
        : existingStagedFileIds.has(file.id)
          ? CaveService.StartStagedChangeRequestFileDownload(requestId, file.id)
          : CaveService.StartAuthoringStagedFileDownload(file.id),
    }])),
    linePlots: Object.fromEntries(reviewLinePlots.map(linePlot => [linePlot.id!, {
      onView: () => setActiveLinePlot(linePlot),
      onDownload: () => downloadFile(`${linePlot.name}.geojson`, linePlot.geoJson),
    }])),
  };

  const previewChanges = async (_validatedValues: AddCaveVm) => {
    const values = getCaveAuthoringSubmissionValues(form);
    setLoading(true);
    try {
      setDraft(values);
      setPreview(await CaveService.PreviewRevisedChanges(requestId, values, againstCurrent,
        expectedBaseRevisionId!, expectedProposalVersionId!));
    } catch (error: any) {
      if (error?.conflictKind === "ActiveProposalVersionChanged")
        message.warning("Another editor created a newer proposal version. Reload before continuing.");
      else if (error?.conflictKind === "PublishedCaveChanged")
        message.warning("The Cave changed again. Reload this page before revising the proposal.");
      else message.error("The revised proposal could not be previewed.");
    } finally { setLoading(false); }
  };

  const save = async () => {
    if (!draft || !preview?.hasMeaningfulChanges) return;
    setSaving(true);
    try {
      await CaveService.ReviseChanges(requestId, draft, againstCurrent,
        expectedBaseRevisionId!, expectedProposalVersionId!);
      message.success(detail?.request.status === "ChangesRequested" ? "A new draft proposal version was saved. Resubmit it for review when you are ready." : "A new proposal version was saved.");
      unsaved.markClean();
      navigate(ClientRoutes.caveChangeRequest.get(requestId));
    } catch (error: any) {
      if (error?.conflictKind === "ActiveProposalVersionChanged")
        message.warning("Another editor created a newer proposal version. Reload before saving.");
      else if (error?.conflictKind === "PublishedCaveChanged")
        message.warning("The Cave changed again. Reload before creating the new proposal version.");
      else message.error("The proposal version could not be saved.");
    } finally { setSaving(false); }
  };

  return <Spin spinning={loading}><Space direction="vertical" style={{ width: "100%" }}>
    <UnsavedChangesModal
      open={unsaved.isBlocked}
      onKeepEditing={unsaved.keepEditing}
      onDiscardChanges={unsaved.discardChanges}
    />
    {detail?.request.status === "ChangesRequested" && latestChangesRequested &&
      <Alert type="warning" showIcon message="Reviewer requested changes" description={latestChangesRequested.message} />}
    {detail?.request.isStale && <>
      <CaveStaleReviewNotice />
      <CaveStaleReviewContext detail={detail} assetActions={assetActions} showOriginalSubmission />
    </>}
    {preview && draft && <Card title="Review revised proposal">
      {!preview.hasMeaningfulChanges &&
        <Alert type="warning" showIcon message="This would not create a meaningful new proposal version. Change the proposal before saving."
          style={{ marginBottom: 16 }} />}
      <CaveRevisionDiff diff={preview.diff} previous={preview.base} current={preview.proposed}
        countyNumberIntent={preview.countyNumberIntent} assetActions={assetActions} />
      <Space style={{ marginTop: 16 }}>
        <PlanarianButton icon={undefined} type="primary" loading={saving} onClick={save}
          disabled={!preview.hasMeaningfulChanges}>{detail?.request.status === "ChangesRequested" ? "Save draft version" : "Save proposal version"}</PlanarianButton>
        <PlanarianButton icon={undefined} onClick={() => { setPreview(undefined); setDraft(undefined); }}>Keep editing</PlanarianButton>
      </Space>
    </Card>}
    {detail && !isRevisable &&
      <Alert type="info" showIcon message={`This request is ${detail.request.status.toLowerCase()} and can no longer be revised.`} />}
    {detail && isRevisable && !detail.request.canEdit && !detail.request.canReview &&
      <Alert type="error" showIcon message="You do not have permission to revise this request." />}
    {detail && editorValues && isRevisable &&
      (detail.request.canEdit || detail.request.canReview) && !preview && <Card>
      <Typography.Paragraph type="secondary">
        {detail.request.status === "ChangesRequested"
          ? detail.request.isStale
            ? "Start with the latest cave information and reapply the requested edits you still want to keep. Saving creates a draft version; it will not return to review until you explicitly resubmit it."
            : "Update the proposal to address the reviewer feedback. Saving creates a draft version; it will not return to review until you explicitly resubmit it."
          : detail.request.isStale
            ? "The form below starts with the latest cave information. Reapply only the edits you still want to keep; use the comparisons above if you need to check the original submission or newer cave changes."
            : "Update the proposed changes, then review them before saving a new version."}
      </Typography.Paragraph>
      <Form form={form} layout="vertical" onFinish={previewChanges} onValuesChange={unsaved.markDirty}>
        <AddCaveComponent isEditing form={form} cave={editorValues} submitLabel="Review changes"
          onAuthoringChange={unsaved.markDirty} stagedFileRequestId={requestId}
          existingRequestStagedFileIds={detail.activeStagedFiles.map(file => file.id)} stickySubmit />
      </Form>
    </Card>}
    <FileViewer open={!!activeFile} onCancel={() => setActiveFileId(undefined)}
      fileId={activeFile?.id} name={activeFile ? `${activeFile.name}${activeFile.extension ?? ""}` : undefined}
      fileType={getFileType(activeFile?.extension)}
      viewUrl={activeFile && !publishedFileIds.has(activeFile.id)
        ? existingStagedFileIds.has(activeFile.id)
          ? CaveService.GetStagedChangeRequestFileUrl(requestId, activeFile.id, "view")
          : CaveService.GetAuthoringStagedFileUrl(activeFile.id, "view") : undefined}
      onDownload={activeFile && !publishedFileIds.has(activeFile.id)
        ? () => existingStagedFileIds.has(activeFile.id)
          ? CaveService.StartStagedChangeRequestFileDownload(requestId, activeFile.id)
          : CaveService.StartAuthoringStagedFileDownload(activeFile.id) : undefined} />
    <CaveLinePlotViewer linePlot={activeLinePlot} open={!!activeLinePlot}
      onClose={() => setActiveLinePlot(undefined)} />
  </Space></Spin>;
};
