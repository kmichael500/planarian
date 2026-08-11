import { useContext, useEffect, useState } from "react";
import { Alert, Card, Form, message, Space, Typography } from "antd";
import { useNavigate, useParams } from "react-router-dom";
import { AppContext } from "../../../Configuration/Context/AppContext";
import { BackButtonComponent } from "../../../Shared/Components/Buttons/BackButtonComponent";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
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
  const [form] = Form.useForm<AddCaveVm>();
  const [cave, setCave] = useState<CaveVm>();
  const [draft, setDraft] = useState<AddCaveVm>();
  const [previewResult, setPreviewResult] = useState<CaveChangePreviewVm>();
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    if (!caveId) return;
    setHeaderTitle(["Suggest Changes"]);
    setHeaderButtons([<BackButtonComponent to={`/caves/${caveId}`} />]);
    CaveService.GetCave(caveId).then((loaded) => {
      setCave(loaded);
      form.setFieldsValue(caveToForm(loaded));
    }).catch(() => message.error("The Cave could not be loaded."))
      .finally(() => setLoading(false));
  }, [caveId]);

  if (!caveId) return null;

  const preview = async (values: AddCaveVm) => {
    setLoading(true);
    try {
      setDraft(values);
      setPreviewResult(await CaveService.PreviewChanges(caveId, values, cave!.currentRevisionId));
    } catch (error: any) {
      if (error?.response?.data?.conflictKind === "PublishedCaveChanged")
        message.warning("The Cave changed while you were editing. Reload and review the current Cave before continuing.");
      else message.error("The proposed changes could not be previewed.");
    } finally {
      setLoading(false);
    }
  };

  const submit = async () => {
    if (!draft) return;
    setSubmitting(true);
    try {
      const id = await CaveService.SubmitChanges(caveId, draft, cave!.currentRevisionId);
      message.success("Your changes were submitted for review.");
      navigate(`/caves/requests/${id}`);
    } catch (error: any) {
      if (error?.response?.data?.conflictKind === "PublishedCaveChanged")
        message.warning("The Cave changed after your preview. Reload and review it before submitting.");
      else message.error("The change request could not be submitted.");
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <Space direction="vertical" style={{ width: "100%" }}>
      {previewResult && draft && (
        <Card title="Review your changes">
          <Alert message="These changes are not published until a reviewer approves them." type="info" showIcon style={{ marginBottom: 16 }} />
          <CaveRevisionDiff diff={previewResult.diff} previous={previewResult.base} current={previewResult.proposed}
            countyNumberIntent={previewResult.countyNumberIntent} />
          <Space style={{ marginTop: 16 }}>
            <PlanarianButton icon={undefined} type="primary" onClick={submit} loading={submitting}>Submit for review</PlanarianButton>
            <PlanarianButton icon={undefined} onClick={() => { setPreviewResult(undefined); setDraft(undefined); }}>Keep editing</PlanarianButton>
          </Space>
        </Card>
      )}
      <Card loading={loading} style={{ display: previewResult ? "none" : undefined }}>
        {cave && <Form form={form} layout="vertical" onFinish={preview}>
          <Typography.Paragraph type="secondary">Use the normal Cave editor. You will review the field-level changes before submission.</Typography.Paragraph>
          <AddCaveComponent isEditing form={form} cave={cave} />
        </Form>}
      </Card>
    </Space>
  );
};
