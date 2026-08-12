import { useContext, useEffect, useState } from "react";
import { Alert, Card, Form, message, Space, Spin, Typography } from "antd";
import { useNavigate, useParams } from "react-router-dom";
import { AppContext } from "../../../Configuration/Context/AppContext";
import { BackButtonComponent } from "../../../Shared/Components/Buttons/BackButtonComponent";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
import { AddCaveComponent } from "../Components/AddCaveComponent";
import { CaveRevisionDiff } from "../Components/CaveRevisionDiff";
import { snapshotToForm } from "../Helpers/CaveFormMapper";
import { AddCaveVm } from "../Models/AddCaveVm";
import { CaveChangePreviewVm, CaveChangeRequestDetailVm } from "../Models/CaveChangeRequestVm";
import { CaveService } from "../Service/CaveService";
import { isCaveRevisionDiffEmpty } from "../Helpers/CaveRevisionDiffHelpers";

export const ReviseCaveChangeRequestPage = () => {
  const { requestId } = useParams();
  const navigate = useNavigate();
  const { setHeaderTitle, setHeaderButtons } = useContext(AppContext);
  const [form] = Form.useForm<AddCaveVm>();
  const [detail, setDetail] = useState<CaveChangeRequestDetailVm>();
  const [draft, setDraft] = useState<AddCaveVm>();
  const [preview, setPreview] = useState<CaveChangePreviewVm>();
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    setHeaderTitle(["Revise Cave Change Request"]);
    setHeaderButtons([<BackButtonComponent to={`/caves/requests/${requestId}`} />]);
    if (!requestId) return;
    CaveService.GetChangeRequest(requestId).then((loaded) => {
      setDetail(loaded);
      form.setFieldsValue(loaded.request.isStale
        ? snapshotToForm(loaded.current, undefined, undefined, loaded.activeStagedFiles)
        : snapshotToForm(loaded.proposed, loaded.countyNumberIntent, loaded.requestedCountyNumber));
    }).catch(() => message.error("The change request could not be loaded."))
      .finally(() => setLoading(false));
  }, [requestId, form, setHeaderButtons, setHeaderTitle]);

  if (!requestId) return null;
  const againstCurrent = detail?.request.isStale === true;
  const expectedBaseRevisionId = againstCurrent ? detail?.request.currentRevisionId : detail?.request.proposalBaseRevisionId;
  const expectedProposalVersionId = detail?.request.currentProposalVersionId;

  const previewChanges = async (values: AddCaveVm) => {
    setLoading(true);
    try {
      setDraft(values);
      setPreview(await CaveService.PreviewRevisedChanges(requestId, values, againstCurrent,
        expectedBaseRevisionId!, expectedProposalVersionId!));
    } catch (error: any) {
      if (error?.response?.data?.conflictKind === "ActiveProposalVersionChanged")
        message.warning("Another editor created a newer proposal version. Reload before continuing.");
      else if (error?.response?.data?.conflictKind === "PublishedCaveChanged")
        message.warning("The Cave changed again. Reload this page before revising the proposal.");
      else message.error("The revised proposal could not be previewed.");
    } finally { setLoading(false); }
  };

  const save = async () => {
    if (!draft || !preview || isCaveRevisionDiffEmpty(preview.diff)) return;
    setSaving(true);
    try {
      await CaveService.ReviseChanges(requestId, draft, againstCurrent,
        expectedBaseRevisionId!, expectedProposalVersionId!);
      message.success("A new immutable proposal version was created.");
      navigate(`/caves/requests/${requestId}`);
    } catch (error: any) {
      if (error?.response?.data?.conflictKind === "ActiveProposalVersionChanged")
        message.warning("Another editor created a newer proposal version. Reload before saving.");
      else if (error?.response?.data?.conflictKind === "PublishedCaveChanged")
        message.warning("The Cave changed again. Reload before creating the new proposal version.");
      else message.error("The proposal version could not be saved.");
    } finally { setSaving(false); }
  };

  return <Spin spinning={loading}><Space direction="vertical" style={{ width: "100%" }}>
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
      {isCaveRevisionDiffEmpty(preview.diff) &&
        <Alert type="warning" showIcon message="Make at least one change before saving a proposal version."
          style={{ marginBottom: 16 }} />}
      <CaveRevisionDiff diff={preview.diff} previous={preview.base} current={preview.proposed}
        countyNumberIntent={preview.countyNumberIntent} />
      <Space style={{ marginTop: 16 }}>
        <PlanarianButton icon={undefined} type="primary" loading={saving} onClick={save}
          disabled={isCaveRevisionDiffEmpty(preview.diff)}>Save proposal version</PlanarianButton>
        <PlanarianButton icon={undefined} onClick={() => { setPreview(undefined); setDraft(undefined); }}>Keep editing</PlanarianButton>
      </Space>
    </Card>}
    {detail && detail.request.status !== "Pending" &&
      <Alert type="info" showIcon message={`This request is ${detail.request.status.toLowerCase()} and can no longer be revised.`} />}
    {detail && detail.request.status === "Pending" && !detail.request.canEdit && !detail.request.canReview &&
      <Alert type="error" showIcon message="You do not have permission to revise this request." />}
    {detail && detail.request.status === "Pending" && (detail.request.canEdit || detail.request.canReview) && !preview && <Card>
      <Typography.Paragraph type="secondary">
        {againstCurrent ? "This editor is initialized from the current published Cave." : "This editor is initialized from the active proposal."}
      </Typography.Paragraph>
      <Form form={form} layout="vertical" onFinish={previewChanges}>
        <AddCaveComponent isEditing form={form} cave={againstCurrent
          ? snapshotToForm(detail.current, undefined, undefined, detail.activeStagedFiles)
          : snapshotToForm(detail.proposed, detail.countyNumberIntent, detail.requestedCountyNumber)} />
      </Form>
    </Card>}
  </Space></Spin>;
};
