import { useContext, useEffect, useState } from "react";
import { Alert, Card, Form, message, Space, Typography } from "antd";
import { EditOutlined, SendOutlined } from "@ant-design/icons";
import { useNavigate } from "react-router-dom";
import { AppContext } from "../../../Configuration/Context/AppContext";
import { ClientRoutes } from "../../../Configuration/Routing/ClientRoutes.generated";
import { BackButtonComponent } from "../../../Shared/Components/Buttons/BackButtonComponent";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
import { AppService } from "../../../Shared/Services/AppService";
import { ApiErrorResponse } from "../../../Shared/Models/ApiErrorResponse";
import { PermissionKey } from "../../Authentication/Models/PermissionKey";
import { AddCaveComponent } from "../Components/AddCaveComponent";
import { CaveRevisionDiff } from "../Components/CaveRevisionDiff";
import { useLikelyCountyNumber } from "../Components/useLikelyCountyNumber";
import { getCaveAuthoringSubmissionValues } from "../Helpers/CaveAuthoringFormValues";
import { AddCaveVm } from "../Models/AddCaveVm";
import { CaveChangePreviewVm } from "../Models/CaveChangeRequestVm";
import { CaveService } from "../Service/CaveService";

const initialValues: AddCaveVm = {
  name: "",
  alternateNames: [],
  countyId: null,
  stateId: "",
  countyDisplayId: null,
  countyNumber: null,
  isCountyNumberManuallySet: false,
  useFirstAvailableCountyNumber: false,
  lengthFeet: 0,
  depthFeet: 0,
  maxPitDepthFeet: 0,
  numberOfPits: 0,
  narrative: null,
  reportedOn: null,
  reportedByNameTagIds: [],
  entrances: [{
    isPrimary: true,
    locationQualityTagId: "",
    name: null,
    description: null,
    latitude: 0,
    longitude: 0,
    elevationFeet: 0,
    reportedOn: null,
    reportedByNameTagIds: [],
    pitFeet: null,
    entranceStatusTagIds: [],
    fieldIndicationTagIds: [],
    entranceHydrologyTagIds: [],
    entranceOtherTagIds: [],
  }],
  geologyTagIds: [],
  biologyTagIds: [],
  archeologyTagIds: [],
  cartographerNameTagIds: [],
  mapStatusTagIds: [],
  geologicAgeTagIds: [],
  physiographicProvinceTagIds: [],
  otherTagIds: [],
};

const AddCavesPage: React.FC = () => {
  const { setHeaderTitle, setHeaderButtons } = useContext(AppContext);
  const navigate = useNavigate();
  const [form] = Form.useForm<AddCaveVm>();
  const selectedCountyId = Form.useWatch("countyId", form);
  const [canPublishDirectly, setCanPublishDirectly] = useState(false);
  const [draft, setDraft] = useState<AddCaveVm>();
  const [preview, setPreview] = useState<CaveChangePreviewVm>();
  const [submitting, setSubmitting] = useState(false);
  const likelyCountyNumber = useLikelyCountyNumber(preview);

  useEffect(() => {
    setHeaderButtons([<BackButtonComponent to="./.." />]);
    setHeaderTitle(["Add a Cave"]);
  }, [setHeaderButtons, setHeaderTitle]);

  useEffect(() => {
    let active = true;
    setCanPublishDirectly(false);
    if (!selectedCountyId) return () => { active = false; };

    void AppService.HasCavePermission(PermissionKey.Manager, null, selectedCountyId)
      .then(canManage => {
        if (active) setCanPublishDirectly(canManage);
      })
      .catch(() => {
        if (active) setCanPublishDirectly(false);
      });
    return () => { active = false; };
  }, [selectedCountyId]);

  const handleFormSubmit = async (_validatedValues: AddCaveVm) => {
    const values = getCaveAuthoringSubmissionValues(form);
    if (!values.countyId) return;

    setSubmitting(true);
    try {
      const canManageDestination = await AppService.HasCavePermission(
        PermissionKey.Manager, null, values.countyId
      );
      if (canManageDestination) {
        await CaveService.AddCave(values);
        message.success(`'${values.name}' added successfully`);
        navigate("/caves");
        return;
      }

      // County-number controls are Manager-only. If the user changed from a County they
      // manage to one they do not, Ant Form may still retain the now-hidden manual values.
      // A proposal author should always use authoritative automatic allocation.
      const proposalValues: AddCaveVm = {
        ...values,
        countyNumber: null,
        isCountyNumberManuallySet: false,
        useFirstAvailableCountyNumber: false,
      };
      setDraft(proposalValues);
      setPreview(await CaveService.PreviewNewCave(proposalValues));
    } catch (e: any) {
      const error = e as ApiErrorResponse;
      message.error(error.message ?? "The Cave could not be submitted.");
    } finally {
      setSubmitting(false);
    }
  };

  const submitProposal = async () => {
    if (!draft || !preview?.hasMeaningfulChanges) return;
    setSubmitting(true);
    try {
      const requestId = await CaveService.SubmitNewCave(draft);
      message.success("The new Cave was submitted for review.");
      navigate(ClientRoutes.caveChangeRequest.get(requestId));
    } catch (e: any) {
      const error = e as ApiErrorResponse;
      message.error(error.message ?? "The new-Cave submission could not be saved.");
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <Space direction="vertical" style={{ width: "100%" }}>
      {preview && draft ? (
        <Card title="Review new Cave submission">
          {!preview.hasMeaningfulChanges && (
            <Alert
              type="warning"
              showIcon
              message="The submission does not contain a publishable Cave."
              style={{ marginBottom: 16 }}
            />
          )}
          <Typography.Paragraph type="secondary">
            This Cave will be created only after a Manager for the selected location approves it.
            If you are still a member of this account when it is approved, you will automatically receive View access.
          </Typography.Paragraph>
          <CaveRevisionDiff
            diff={preview.diff}
            previous={preview.base}
            current={preview.proposed}
            countyNumberIntent={preview.countyNumberIntent}
            likelyCountyNumber={likelyCountyNumber}
          />
          <Space wrap style={{ marginTop: 16 }}>
            <PlanarianButton
              icon={<SendOutlined />}
              alwaysShowChildren
              type="primary"
              loading={submitting}
              disabled={!preview.hasMeaningfulChanges}
              onClick={submitProposal}
            >
              Submit for review
            </PlanarianButton>
            <PlanarianButton
              icon={<EditOutlined />}
              alwaysShowChildren
              onClick={() => {
                setDraft(undefined);
                setPreview(undefined);
              }}
            >
              Keep editing
            </PlanarianButton>
          </Space>
        </Card>
      ) : (
        <Card>
          {!canPublishDirectly && selectedCountyId && (
            <Alert
              type="info"
              showIcon
              message="This Cave will be submitted for review"
              description="You do not manage the selected County. A Manager for that location can review and publish the Cave."
              style={{ marginBottom: 16 }}
            />
          )}
          <Form<AddCaveVm>
            layout="vertical"
            initialValues={initialValues}
            onFinish={handleFormSubmit}
            form={form}
          >
            <AddCaveComponent
              form={form}
              submitLabel={canPublishDirectly ? "Add cave" : "Review submission"}
              hideCountyNumberControls={!canPublishDirectly}
            />
          </Form>
        </Card>
      )}
    </Space>
  );
};

export { AddCavesPage };
