import { useContext, useEffect, useState } from "react";
import { Alert, Card, Form, Grid, message, Space, theme, Typography } from "antd";
import { useNavigate, useParams } from "react-router-dom";
import { AppContext } from "../../../Configuration/Context/AppContext";
import { BackButtonComponent } from "../../../Shared/Components/Buttons/BackButtonComponent";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
import { UnsavedChangesModal } from "../../../Shared/Components/UnsavedChangesModal";
import { useUnsavedChangesGuard } from "../../../Shared/Hooks/useUnsavedChangesGuard";
import { AddCaveComponent } from "../Components/AddCaveComponent";
import { CaveRevisionDiff } from "../Components/CaveRevisionDiff";
import { caveToForm } from "../Helpers/CaveFormMapper";
import { AddCaveVm } from "../Models/AddCaveVm";
import { CaveChangePreviewVm } from "../Models/CaveChangeRequestVm";
import { CaveVm } from "../Models/CaveVm";
import { CaveService } from "../Service/CaveService";

export const SuggestCaveChangesPage = () => {
  const { caveId } = useParams();
  const navigate = useNavigate();
  const { setHeaderTitle, setHeaderButtons } = useContext(AppContext);
  const screens = Grid.useBreakpoint();
  const { token } = theme.useToken();
  const [form] = Form.useForm<AddCaveVm>();
  const [cave, setCave] = useState<CaveVm>();
  const [expectedBaseRevisionId, setExpectedBaseRevisionId] = useState<string>();
  const [draft, setDraft] = useState<AddCaveVm>();
  const [previewResult, setPreviewResult] = useState<CaveChangePreviewVm>();
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const unsaved = useUnsavedChangesGuard();

  useEffect(() => {
    if (!caveId) return;
    setHeaderTitle(["Suggest Changes"]);
    setHeaderButtons([<BackButtonComponent to={`/caves/${caveId}`} />]);
    CaveService.GetProposalAuthoringContext(caveId).then((context) => {
      setCave(context.cave);
      setExpectedBaseRevisionId(context.expectedBaseRevisionId);
      form.setFieldsValue(caveToForm(context.cave, context.linePlots));
    }).catch(() => message.error("The Cave could not be loaded."))
      .finally(() => setLoading(false));
  }, [caveId]);

  if (!caveId) return null;

  const preview = async (values: AddCaveVm) => {
    setLoading(true);
    try {
      setDraft(values);
      setPreviewResult(await CaveService.PreviewChanges(caveId, values, expectedBaseRevisionId!));
    } catch (error: any) {
      if (error?.conflictKind === "PublishedCaveChanged")
        message.warning("The Cave changed while you were editing. Reload and review the current Cave before continuing.");
      else message.error("The proposed changes could not be previewed.");
    } finally {
      setLoading(false);
    }
  };

  const submit = async () => {
    if (!draft || !previewResult?.hasMeaningfulChanges) return;
    setSubmitting(true);
    try {
      const id = await CaveService.SubmitChanges(caveId, draft, expectedBaseRevisionId!);
      message.success("Your changes were submitted for review.");
      unsaved.markClean();
      navigate(`/caves/requests/${id}`);
    } catch (error: any) {
      if (error?.conflictKind === "PublishedCaveChanged")
        message.warning("The Cave changed after your preview. Reload and review it before submitting.");
      else message.error("The change request could not be submitted.");
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <Space direction="vertical" style={{ width: "100%" }}>
      <UnsavedChangesModal
        open={unsaved.isBlocked}
        onKeepEditing={unsaved.keepEditing}
        onDiscardChanges={unsaved.discardChanges}
      />
      {previewResult && draft && (
        <section aria-labelledby="review-changes-title" style={{
          background: token.colorBgContainer,
          borderRadius: token.borderRadiusLG,
          padding: screens.md ? token.paddingLG : token.padding,
        }}>
          <Typography.Title id="review-changes-title" level={4} style={{ marginTop: 0, marginBottom: token.marginSM }}>
            Review your changes
          </Typography.Title>
          {!previewResult.hasMeaningfulChanges &&
            <Alert message="Make at least one meaningful change before submitting this proposal." type="warning" showIcon
              style={{ marginBottom: token.marginMD }} />}
          <CaveRevisionDiff diff={previewResult.diff} previous={previewResult.base} current={previewResult.proposed}
            countyNumberIntent={previewResult.countyNumberIntent} />
          <div role="group" aria-label="Review actions" style={{ borderTop: `1px solid ${token.colorSplit}`,
            marginTop: token.marginLG, paddingTop: token.paddingMD }}>
            <Typography.Paragraph type="secondary" style={{ marginBottom: token.marginSM }}>
              Changes are published only after a reviewer approves them.
            </Typography.Paragraph>
            <Space wrap>
              <PlanarianButton icon={undefined} type="primary" onClick={submit} loading={submitting}
                disabled={!previewResult.hasMeaningfulChanges}>Submit for review</PlanarianButton>
              <PlanarianButton icon={undefined} onClick={() => { setPreviewResult(undefined); setDraft(undefined); }}>Keep editing</PlanarianButton>
            </Space>
          </div>
        </section>
      )}
      <Card loading={loading} style={{ display: previewResult ? "none" : undefined }}>
        {cave && <Form form={form} layout="vertical" onFinish={preview} onValuesChange={unsaved.markDirty}>
          <Typography.Paragraph type="secondary">Make the changes you want reviewed. Everything else will stay as it is.</Typography.Paragraph>
          <AddCaveComponent isEditing form={form} cave={cave} submitLabel="Review changes"
            onAuthoringChange={unsaved.markDirty} stickySubmit />
        </Form>}
      </Card>
    </Space>
  );
};
