import { useContext, useEffect, useState } from "react";
import { AppContext } from "../../../Configuration/Context/AppContext";
import { AddCaveComponent } from "../Components/AddCaveComponent";
import { BackButtonComponent } from "../../../Shared/Components/Buttons/BackButtonComponent";
import { Card, Form, message } from "antd";
import { AddCaveVm } from "../Models/AddCaveVm";
import { CaveService } from "../Service/CaveService";
import { useNavigate, useParams } from "react-router-dom";
import { ApiErrorResponse } from "../../../Shared/Models/ApiErrorResponse";
import { ReviewChangeRequest } from "../Models/ProposeChangeRequestVm";
import { ProposedChangeRequestVm } from "../Models/ProposedChangeRequestVm";

const CaveEditReviewPage: React.FC = () => {
  const { caveChangeRequestId } = useParams();
  const [proposedChange, setProposedChange] =
    useState<ProposedChangeRequestVm>();

  if (caveChangeRequestId === undefined) {
    throw new Error("caveChangeRequestId is undefined");
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
      const caveResponse = await CaveService.GetProposedChange(
        caveChangeRequestId
      );

      setProposedChange(caveResponse);

      setHeaderButtons([<BackButtonComponent to={"./.."} />]);
      setHeaderTitle([`Review`]);

      setIsLoading(false);
    };
    getCave();
  }, []);

  const handleFormSubmit = async (values: AddCaveVm) => {
    try {
      setIsLoading(true);

      const changeRequest: ReviewChangeRequest = {
        cave: values,
        id: caveChangeRequestId,
        approve: true,
        notes: null,
      };

      await CaveService.ReviewChange(changeRequest);
      message.success(`'${values?.name}' has been submitted successfully`);
      navigate(`/caves/review`);
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
          initialValues={proposedChange?.cave}
          onFinish={handleFormSubmit}
          onChange={handleFormChange}
          form={form}
        >
          <AddCaveComponent
            isEditing={true}
            form={form}
            cave={proposedChange?.cave}
          />
        </Form>
      </Card>
    </>
  );
};

export { CaveEditReviewPage };
