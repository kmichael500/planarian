import { useContext, useEffect, useState } from "react";
import { AppContext } from "../../../Configuration/Context/AppContext";
import { AddCaveComponent } from "../Components/AddCaveComponent";
import { BackButtonComponent } from "../../../Shared/Components/Buttons/BackButtonComponent";
import { Card, Form, message } from "antd";
import { AddCaveVm } from "../Models/AddCaveVm";
import { CaveService } from "../Service/CaveService";
import { useNavigate, useParams } from "react-router-dom";
import { ApiErrorResponse } from "../../../Shared/Models/ApiErrorResponse";
import { CaveVm } from "../Models/CaveVm";
import { DeleteButtonComponent } from "../../../Shared/Components/Buttons/DeleteButtonComponent";
import { caveToForm } from "../Helpers/CaveFormMapper";
import { UnsavedChangesModal } from "../../../Shared/Components/UnsavedChangesModal";
import { useUnsavedChangesGuard } from "../../../Shared/Hooks/useUnsavedChangesGuard";
import { getCaveAuthoringSubmissionValues } from "../Helpers/CaveAuthoringFormValues";

const EditCavePage: React.FC = () => {
  const { caveId } = useParams();
  const [cave, setCave] = useState<CaveVm>();

  if (caveId === undefined) {
    throw new Error("caveId is undefined");
  }

  const [isLoading, setIsLoading] = useState<boolean>(true);
  const { setHeaderTitle, setHeaderButtons } = useContext(AppContext);

  const navigate = useNavigate();
  const unsaved = useUnsavedChangesGuard();

  useEffect(() => {
    const getCave = async () => {
      const context = await CaveService.GetEditAuthoringContext(caveId);
      const caveResponse = context.cave;

      setCave(caveResponse);
      const formValues = caveToForm(caveResponse, context.linePlots);

      form.setFieldsValue(formValues);

      setHeaderButtons([
        <DeleteButtonComponent
          title={`Are you sure you want to delete '${caveResponse?.name}'? This action is not reversable!'`}
          onConfirm={async () => {
            try {
              await CaveService.DeleteCave(caveResponse?.id as string);
              message.success(
                `'${caveResponse?.name}' has been deleted successfully`
              );
              unsaved.markClean();
              navigate(`/caves`);
            } catch (e: any) {
              const error = e as ApiErrorResponse;
              message.error(error.message);
            }
          }}
        />,
        <BackButtonComponent to={"./.."} />,
      ]);
      setHeaderTitle([`Edit ${caveResponse?.displayId} ${caveResponse?.name}`]);

      setIsLoading(false);
    };
    getCave();
  }, []);

  const handleFormSubmit = async (_validatedValues: AddCaveVm) => {
    const values = getCaveAuthoringSubmissionValues(form);
    try {
      setIsLoading(true);
      await CaveService.UpdateCave(values);
      message.success(`'${values?.name}' has been updated successfully`);
      unsaved.markClean();
      navigate(`/caves/${cave?.id}`);
    } catch (e: any) {
      const error = e as ApiErrorResponse & { conflictKind?: string };
      if (error.statusCode === 409 && error.conflictKind === "PublishedCaveChanged") {
        message.warning("This Cave changed after you opened the editor. Reload before saving so you do not overwrite newer changes.");
      } else {
        message.error(error.message);
      }
    } finally {
      setIsLoading(false);
    }
  };

  const [form] = Form.useForm<AddCaveVm>();
  return (
    <>
      <UnsavedChangesModal
        open={unsaved.isBlocked}
        onKeepEditing={unsaved.keepEditing}
        onDiscardChanges={unsaved.discardChanges}
      />
      <Card loading={isLoading}>
        {cave && (
          <Form<AddCaveVm>
            layout="vertical"
            initialValues={cave}
            onFinish={handleFormSubmit}
            onValuesChange={unsaved.markDirty}
            form={form}
          >
            <AddCaveComponent isEditing={true} form={form} cave={cave}
              onAuthoringChange={unsaved.markDirty} />
          </Form>
        )}
      </Card>
    </>
  );
};

export { EditCavePage };
