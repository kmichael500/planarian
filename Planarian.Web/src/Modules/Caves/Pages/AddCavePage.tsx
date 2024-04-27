import { useContext, useEffect } from "react";
import { AppContext } from "../../../Configuration/Context/AppContext";
import { AddCaveComponent } from "../Components/AddCaveComponent";
import { BackButtonComponent } from "../../../Shared/Components/Buttons/BackButtonComponent";
import { Card, Form, message } from "antd";
import { AddCaveVm } from "../Models/AddCaveVm";
import { CaveService } from "../Service/CaveService";
import { AuthenticationService } from "../../Authentication/Services/AuthenticationService";
import { useNavigate } from "react-router-dom";
import { ApiErrorResponse } from "../../../Shared/Models/ApiErrorResponse";

const AddCavesPage: React.FC = () => {
  const { setHeaderTitle, setHeaderButtons } = useContext(AppContext);
  const navigate = useNavigate();
  useEffect(() => {
    setHeaderButtons([<BackButtonComponent to={"./.."} />]);
    setHeaderTitle(["Add a Cave"]);
  }, []);

  const initialValues: AddCaveVm = {
    name: "",
    alternateNames: [],
    countyId: null,
    stateId: "",
    lengthFeet: 0,
    depthFeet: 0,
    maxPitDepthFeet: 0,
    numberOfPits: 0,
    narrative: null,
    reportedOn: null,
    reportedByNameTagIds: [],
    entrances: [
      {
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
      },
    ],
    geologyTagIds: [],
    biologyTagIds: [],
    archeologyTagIds: [],
    cartographerNameTagIds: [],
    mapStatusTagIds: [],
    geologicAgeTagIds: [],
    physiographicProvinceTagIds: [],
    otherTagIds: [],
  };

  const handleFormSubmit = async (values: AddCaveVm) => {
    try {
      await CaveService.AddCave(values);
      message.success(`'${values.name}' added successfully`);
      navigate("/caves");
    } catch (e: any) {
      const error = e as ApiErrorResponse;
      message.error(error.message);
    }
  };

  const [form] = Form.useForm<AddCaveVm>();

  return (
    <>
      <Card>
        <Form<AddCaveVm>
          layout="vertical"
          initialValues={initialValues}
          onFinish={handleFormSubmit}
          form={form}
        >
          <AddCaveComponent form={form} />
        </Form>
      </Card>
    </>
  );
};

export { AddCavesPage };
