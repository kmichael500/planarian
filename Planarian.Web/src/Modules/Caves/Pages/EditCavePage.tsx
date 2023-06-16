import { useContext, useEffect, useState } from "react";
import { AppContext } from "../../../Configuration/Context/AppContext";
import { AddCaveComponent } from "../Components/AddCaveComponent";
import { BackButtonComponent } from "../../../Shared/Components/Buttons/BackButtonComponent";
import { Card, Form, message } from "antd";
import { AddCaveVm } from "../Models/AddCaveVm";
import { CaveService } from "../Service/CaveService";
import { AuthenticationService } from "../../Authentication/Services/AuthenticationService";
import { useNavigate, useParams } from "react-router-dom";
import { ApiErrorResponse } from "../../../Shared/Models/ApiErrorResponse";
import { CaveVm } from "../Models/CaveVm";
import { formatDateTime } from "../../../Shared/Helpers/StringHelpers";
import { DeleteButtonComponent } from "../../../Shared/Components/Buttons/DeleteButtonComponent";

const EditCavePage: React.FC = () => {
  const { caveId } = useParams();
  const [cave, setCave] = useState<CaveVm>();

  if (caveId === undefined) {
    throw new Error("caveId is undefined");
  }

  const [isLoading, setIsLoading] = useState<boolean>(true);
  const { setHeaderTitle, setHeaderButtons } = useContext(AppContext);

  const navigate = useNavigate();

  useEffect(() => {
    const getCave = async () => {
      const caveResponse = await CaveService.GetCave(caveId);

      // antd input requires date to be in format yyyy-MM-DD otherwise it will not be displayed
      caveResponse.reportedOn = formatDateTime(
        caveResponse.reportedOn,
        "yyyy-MM-DD"
      );
      caveResponse.entrances.forEach((entrance) => {
        entrance.reportedOn = formatDateTime(entrance.reportedOn, "yyyy-MM-DD");
      });
      setCave(caveResponse);

      setHeaderButtons([
        <DeleteButtonComponent
          title={`Are you sure you want to delete '${caveResponse?.name}'? This action is not reversable!'`}
          onConfirm={async () => {
            await CaveService.DeleteCave(caveResponse?.id as string);
          }}
        />,
        <BackButtonComponent to={"./.."} />,
      ]);
      setHeaderTitle([`Edit ${caveResponse?.displayId} ${caveResponse?.name}`]);

      setIsLoading(false);
    };
    getCave();
  }, [cave]);

  const handleFormSubmit = async (values: AddCaveVm) => {
    try {
      console.log(values);
      await CaveService.UpdateCave(values);
      message.success(`'${values?.name}' has been updated successfully`);
      navigate(`/caves/${cave?.id}`);
    } catch (e: any) {
      const error = e as ApiErrorResponse;
      message.error(error.message);
    }
  };

  const [form] = Form.useForm<AddCaveVm>();

  return (
    <>
      <Card loading={isLoading}>
        <Form<AddCaveVm>
          layout="vertical"
          initialValues={cave}
          onFinish={handleFormSubmit}
          form={form}
        >
          <AddCaveComponent isEditing={true} form={form} />
        </Form>
      </Card>
    </>
  );
};

export { EditCavePage };
