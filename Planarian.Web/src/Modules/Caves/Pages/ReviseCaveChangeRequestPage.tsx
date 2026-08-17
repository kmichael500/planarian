import { useContext, useEffect, useState } from "react";
import { Alert, Card, Form, message, Space, Spin, Typography } from "antd";
import { useNavigate, useParams } from "react-router-dom";
import { AppContext } from "../../../Configuration/Context/AppContext";
import { BackButtonComponent } from "../../../Shared/Components/Buttons/BackButtonComponent";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
import { UnsavedChangesModal } from "../../../Shared/Components/UnsavedChangesModal";
import { useUnsavedChangesGuard } from "../../../Shared/Hooks/useUnsavedChangesGuard";
import { AddCaveComponent } from "../Components/AddCaveComponent";
import { CaveRevisionDiff } from "../Components/CaveRevisionDiff";
import { caveToForm, snapshotToForm } from "../Helpers/CaveFormMapper";
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
  const unsaved = useUnsavedChangesGuard();

  useEffect(() => {
    setHeaderTitle(["Revise changes"]);
    setHeaderButtons([<BackButtonComponent to={`/caves/requests/${requestId}`} />]);
    if (!requestId) return;
    const load = async () => {
      try {
        const loaded = await CaveService.GetChangeRequest(requestId);
        setDetail(loaded);
        setExpectedProposalVersionId(loaded.request.currentProposalVersionId);

        let values: AddCaveVm;
        if (loaded.request.isStale) {
          const context = await CaveService.GetProposalAuthoringContext(loaded.request.caveId);
          values = caveToForm(context.cave, context.linePlots);
          const existingIds = new Set((values.files ?? []).map((file) => file.id));
          values.files = [
            ...(values.files ?? []),
            ...loaded.activeStagedFiles.filter((file) => !existingIds.has(file.id)).map((file) => ({
              id: file.id,
              displayName: file.displayName ?? null,
              fileTypeTagId: file.fileTypeTagId,
              fileTypeKey: file.fileTypeNameAtRevision,
            })),
          ];
          setExpectedBaseRevisionId(context.expectedBaseRevisionId);
        } else {
          const version = await CaveService.GetProposalVersion(requestId, loaded.request.currentProposalVersionId);
          values = snapshotToForm(version.proposed, version.countyNumberIntent,
            version.requestedCountyNumber, loaded.activeStagedFiles, version.linePlots);
          setExpectedBaseRevisionId(version.baseRevisionId);
        }
        setEditorValues(values);
        form.setFieldsValue(values);
      } catch {
        message.error("The change request could not be loaded.");
      } finally {
        setLoading(false);
      }
    };
    void load();
  }, [requestId, form, setHeaderButtons, setHeaderTitle]);

  if (!requestId) return null;
  const againstCurrent = detail?.request.isStale === true;

  const previewChanges = async (values: AddCaveVm) => {
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
      message.success("A new proposal version was saved.");
      unsaved.markClean();
      navigate(`/caves/requests/${requestId}`);
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
    {detail?.request.isStale && <>
      <Alert type="warning" showIcon message="Revise against the current published Cave"
        description="The editor starts from the current Cave. Reapply the changes you still want; the older proposal remains unchanged in the version history." />
      <Card title="Original proposal changes">
        <CaveRevisionDiff diff={detail.diff} previous={detail.base} current={detail.proposed}
          countyNumberIntent={detail.countyNumberIntent} />
      </Card>
      {detail.publishedSinceBase && <Card title="Published changes since that proposal">
        <CaveRevisionDiff diff={detail.publishedSinceBase} previous={detail.base} current={detail.current} />
      </Card>}
    </>}
    {preview && draft && <Card title="Review revised proposal">
      {!preview.hasMeaningfulChanges &&
        <Alert type="warning" showIcon message="This would not create a meaningful new proposal version. Change the proposal before saving."
          style={{ marginBottom: 16 }} />}
      <CaveRevisionDiff diff={preview.diff} previous={preview.base} current={preview.proposed}
        countyNumberIntent={preview.countyNumberIntent} />
      <Space style={{ marginTop: 16 }}>
        <PlanarianButton icon={undefined} type="primary" loading={saving} onClick={save}
          disabled={!preview.hasMeaningfulChanges}>Save proposal version</PlanarianButton>
        <PlanarianButton icon={undefined} onClick={() => { setPreview(undefined); setDraft(undefined); }}>Keep editing</PlanarianButton>
      </Space>
    </Card>}
    {detail && detail.request.status !== "Pending" &&
      <Alert type="info" showIcon message={`This request is ${detail.request.status.toLowerCase()} and can no longer be revised.`} />}
    {detail && detail.request.status === "Pending" && !detail.request.canEdit && !detail.request.canReview &&
      <Alert type="error" showIcon message="You do not have permission to revise this request." />}
    {detail && editorValues && detail.request.status === "Pending" &&
      (detail.request.canEdit || detail.request.canReview) && !preview && <Card>
      <Typography.Paragraph type="secondary">
        Update the proposed changes, then review them before saving a new version.
      </Typography.Paragraph>
      <Form form={form} layout="vertical" onFinish={previewChanges} onValuesChange={unsaved.markDirty}>
        <AddCaveComponent isEditing form={form} cave={editorValues} submitLabel="Review changes"
          onAuthoringChange={unsaved.markDirty} stickySubmit />
      </Form>
    </Card>}
  </Space></Spin>;
};
