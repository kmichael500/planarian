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
import { isNullOrWhiteSpace } from "../../../Shared/Helpers/StringHelpers";
import { DeleteButtonComponent } from "../../../Shared/Components/Buttons/DeleteButtonComponent";
import dayjs from "dayjs";
import { ProposeChangeRequestVm } from "../Models/ProposeChangeRequestVm";

const EditCavePage: React.FC = () => {
  const { caveId } = useParams();
  const [cave, setCave] = useState<CaveVm>();

  if (caveId === undefined) {
    throw new Error("caveId is undefined");
  }

  const [isLoading, setIsLoading] = useState<boolean>(true);
  const { setHeaderTitle, setHeaderButtons } = useContext(AppContext);

  const navigate = useNavigate();

  const [isFormDirty, setIsFormDirty] = useState<boolean>(false);
  useEffect(() => {
    const handleBeforeUnload = (event: BeforeUnloadEvent) => {
      if (isFormDirty) {
        event.preventDefault();
        event.returnValue =
          "You have unsaved changes! Are you sure you want to leave?";
      }
    };

    // Attach the event listener
    window.addEventListener("beforeunload", handleBeforeUnload);

    // Cleanup the event listener on component unmount
    return () => {
      window.removeEventListener("beforeunload", handleBeforeUnload);
    };
  }, [isFormDirty]);

  useEffect(() => {
    const getCave = async () => {
      const caveResponse = await CaveService.GetCave(caveId);

      // date picker requires it to be a dayjs object
      if (!isNullOrWhiteSpace(caveResponse.reportedOn)) {
        caveResponse.reportedOn = dayjs.utc(caveResponse.reportedOn) as any;
      }
      caveResponse.entrances.forEach((entrance) => {
        if (!isNullOrWhiteSpace(entrance.reportedOn)) {
          entrance.reportedOn = dayjs.utc(entrance.reportedOn) as any;
        }
      });
      setCave(caveResponse);

      setHeaderButtons([
        <DeleteButtonComponent
          title={`Are you sure you want to delete '${caveResponse?.name}'? This action is not reversable!'`}
          onConfirm={async () => {
            try {
              await CaveService.DeleteCave(caveResponse?.id as string);
              message.success(
                `'${caveResponse?.name}' has been deleted successfully`
              );
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

  const handleFormSubmit = async (values: AddCaveVm) => {
    try {
      setIsLoading(true);

      const changeRequest: ProposeChangeRequestVm = {
        cave: values,
      };

      await CaveService.ProposeChange(changeRequest);
      message.success(`'${values?.name}' has been submitted successfully`);
      navigate(`/caves/${cave?.id}`);
    } catch (e: any) {
      const error = e as ApiErrorResponse;
      message.error(error.message);
    } finally {
      setIsLoading(false);
    }
  };

  const [form] = Form.useForm<AddCaveVm>();
  const handleFormChange = () => {
    setIsFormDirty(true);
  };
  return (
    <>
      <Card loading={isLoading}>
        <Form<AddCaveVm>
          layout="vertical"
          initialValues={cave}
          onFinish={handleFormSubmit}
          onChange={handleFormChange} // Attach change handler
          form={form}
        >
          <AddCaveComponent isEditing={true} form={form} cave={cave} />
        </Form>
      </Card>
    </>
  );
};

export { EditCavePage };
