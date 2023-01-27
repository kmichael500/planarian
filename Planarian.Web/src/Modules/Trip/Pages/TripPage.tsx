import {
  Button,
  Card,
  Col,
  Divider,
  message,
  Row,
  Select,
  Space,
  Spin,
  Typography,
} from "antd";
import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import TextArea from "antd/lib/input/TextArea";
import type { ColumnsType } from "antd/es/table";
import {
  MemberGridComponent,
  MemberGridType,
} from "../../../Shared/Components/MemberGridComponent";
import { TripVm } from "../Models/TripVm";
import { TripService } from "../Services/TripService";
import { SettingsService } from "../../Setting/Services/SettingsService";
import LeadTableComponent from "../Components/LeadTableComponent";
import { TripDetailPhotoComponent } from "../Components/TripDetailPhotoComponent";
import { TripTagComponent } from "../Components/TripTagComponent";
import { ApiErrorResponse } from "../../../Shared/Models/ApiErrorResponse";
import { PropertyLength } from "../../../Shared/Constants/PropertyLengthConstant";

const { Title, Paragraph } = Typography;

const TripPage: React.FC = () => {
  let [isLoading, setIsLoading] = useState(false);
  let [isEditing, setIsEditing] = useState(false);
  let [trip, setTrip] = useState<TripVm>();

  const { tripId, projectId } = useParams();

  let [tripReport, setTripReport] = useState<string>("");

  const onEditClick = () => {
    setIsEditing(true);
  };
  const onCancelClick = () => {
    setTripReport(trip?.tripReport ?? "");
    setIsEditing(false);
  };

  const onSaveClick = async () => {
    try {
      await TripService.AddOrUpdateTripReport(tripId as string, tripReport);

      const tripCopy = { ...trip } as TripVm;
      tripCopy.tripReport = tripReport;
      setTrip(tripCopy);
      setIsEditing(false);
    } catch (e) {
      const error = e as ApiErrorResponse;
      message.error(error.message);
    }
  };

  interface UserTableColumn {
    name: string;
  }

  useEffect(() => {
    if (trip === undefined) {
      const getTrip = async () => {
        const tripResponse = await TripService.GetTrip(tripId as string);
        setTrip(tripResponse);
        setTripReport(tripResponse.tripReport ?? "");
        setIsLoading(false);
      };
      getTrip();
    }
  });

  const updateName = async (e: string) => {
    try {
      await TripService.UpdateTripName(e, tripId as string);
      const tripCopy = { ...trip } as TripVm;
      tripCopy.name = e;
      setTrip(tripCopy);
    } catch (e) {
      const error = e as ApiErrorResponse;
      message.error(error.message);
    }
  };

  const updateDescription = async (e: string) => {
    try {
      await TripService.UpdateTripDescription(e, tripId as string);
      const tripCopy = { ...trip } as TripVm;
      tripCopy.description = e;
      setTrip(tripCopy);
    } catch (e) {
      const error = e as ApiErrorResponse;
      message.error(error.message);
    }
  };

  return (
    <>
      <Row align="middle" gutter={10}>
        <Col>
          <Spin spinning={isLoading}>
            <Title
              editable={{
                onChange: updateName,
                maxLength: PropertyLength.NAME,
              }}
              level={2}
            >
              {trip?.name}
            </Title>
            <Paragraph
              type="secondary"
              editable={{
                onChange: updateDescription,
                maxLength: PropertyLength.MEDIUM_TEXT,
              }}
            >
              {trip?.description}
            </Paragraph>
          </Spin>

          <TripTagComponent
            getTags={() => {
              return TripService.GetTags(tripId as string);
            }}
            getTagTypes={SettingsService.GetTripTags}
            tripId={tripId as string}
          />
        </Col>
        {/* take up rest of space to push others to right and left side */}
        <Col flex="auto"></Col>
        <Col>
          <Link to={"./../.."}>
            <Button>Back</Button>
          </Link>
        </Col>
        <Col> </Col>
      </Row>
      <Divider />

      <MemberGridComponent
        type={MemberGridType.Trip}
        tripId={tripId}
        projectId={projectId as string}
      ></MemberGridComponent>
      <Divider></Divider>
      <Card
        loading={isLoading}
        title="Trip Report"
        extra={
          <Row align="middle" gutter={10}>
            {isEditing && (
              <>
                <Col>
                  <Button type="primary" onClick={onSaveClick}>
                    Save
                  </Button>
                </Col>
                <Col>
                  <Button onClick={onCancelClick} type="default">
                    Cancel
                  </Button>
                </Col>
              </>
            )}

            {!isEditing && (
              <Col>
                <Button onClick={onEditClick} type="primary">
                  Edit
                </Button>
              </Col>
            )}
          </Row>
        }
      >
        <Spin spinning={isLoading}>
          {!isEditing && (
            <>
              {tripReport.split(/\r?\n/).map((paragraph) => {
                return <Paragraph>{paragraph}</Paragraph>;
              })}
            </>
          )}
          {isEditing && (
            <TextArea
              onChange={(e) => {
                setTripReport(e.target.value);
              }}
              value={tripReport}
              defaultValue={tripReport}
              disabled={!isEditing}
              rows={20}
            ></TextArea>
          )}
        </Spin>
      </Card>

      <Divider />

      <LeadTableComponent tripId={tripId as string}></LeadTableComponent>

      <Divider />
      <Card
        title="Photos"
        extra={
          <Link to={"uploadPhotos"}>
            <Button>+</Button>
          </Link>
        }
      >
        <TripDetailPhotoComponent
          tripId={tripId as string}
        ></TripDetailPhotoComponent>
      </Card>
    </>
  );
};
export { TripPage };
