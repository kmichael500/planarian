import {
  Row,
  Col,
  Button,
  Divider,
  Spin,
  Typography,
  Card,
  Select,
  Space,
} from "antd";
import { useState, useEffect, useCallback } from "react";
import { Link, useParams } from "react-router-dom";
import TextArea from "antd/lib/input/TextArea";
import type { ColumnsType } from "antd/es/table";
import {
  MemberGridComponent,
  MemberGridType,
} from "../../../Shared/Components/MemberGridComponent";
import { TripObjectiveVm } from "../Models/TripObjectiveVm";
import { TripObjectiveService } from "../Services/trip.objective.service";
import { TripObjectiveDetailPhotoComponent } from "./objective.detail.photo.component";
import LeadTableComponent from "../../Components/lead.table.component";
import { ObjectiveTypeTagComponent } from "../../ObjectiveTypes/Components/objective.type.tag.component";
import { SettingsService } from "../../Settings/Services/settings.service";
import { LeadVm } from "../../Leads/Models/Lead";

const { Option } = Select;
const { Title, Paragraph } = Typography;

const TripObjectiveDetailComponent: React.FC = () => {
  let [isLoading, setIsLoading] = useState(false);
  let [isEditing, setIsEditing] = useState(false);
  let [objective, setObjective] = useState<TripObjectiveVm>();

  const { tripObjectiveId, projectId } = useParams();

  let [tripReport, setTripReport] = useState<string>("");

  const onEditClick = () => {
    setIsEditing(true);
  };
  const onCancelClick = () => {
    setTripReport(objective?.tripReport ?? "");
    setIsEditing(false);
  };

  const onSaveClick = async () => {
    await TripObjectiveService.AddOrUpdateTripReport(
      tripObjectiveId as string,
      tripReport
    );

    const objectiveCopy = { ...objective } as TripObjectiveVm;
    objectiveCopy.tripReport = tripReport;
    setObjective(objectiveCopy);
    setIsEditing(false);
  };

  interface UserTableColumn {
    name: string;
  }
  const teamMemberColumns: ColumnsType<UserTableColumn> = [
    {
      title: "Name",
      dataIndex: "name",
      key: "name",
      render: (text) => <a>{text}</a>,
    },
    {
      title: "Action",
      key: "action",
      render: (_) => (
        <Space size="middle">
          <a>Delete</a>
        </Space>
      ),
    },
  ];

  useEffect(() => {
    if (objective === undefined) {
      const getObjective = async () => {
        const projectResponse = await TripObjectiveService.GetObjective(
          tripObjectiveId as string
        );
        setObjective(projectResponse);
        setTripReport(projectResponse.tripReport ?? "");
        setIsLoading(false);
      };
      getObjective();
    }
  });

  const updateName = async (e: string) => {
    const objectiveCopy = { ...objective } as TripObjectiveVm;
    objectiveCopy.name = e;
    setObjective(objectiveCopy);

    await TripObjectiveService.UpdateObjectiveName(
      e,
      tripObjectiveId as string
    );
  };

  const updateDescription = async (e: string) => {
    const objectiveCopy = { ...objective } as TripObjectiveVm;
    objectiveCopy.description = e;
    setObjective(objectiveCopy);

    await TripObjectiveService.UpdateObjectiveDescription(
      e,
      tripObjectiveId as string
    );
  };

  const tripReportWithParagraphs = () => {};

  return (
    <>
      <Row align="middle" gutter={10}>
        <Col>
          <Spin spinning={isLoading}>
            <Title editable={{ onChange: updateName }} level={2}>
              {objective?.name}
            </Title>
            <Paragraph
              type="secondary"
              editable={{ onChange: updateDescription }}
            >
              {objective?.description}
            </Paragraph>
          </Spin>

          <ObjectiveTypeTagComponent
            getTags={() => {
              return TripObjectiveService.GetTags(tripObjectiveId as string);
            }}
            getTagTypes={SettingsService.GetTripObjectiveTypes}
            tripObjectiveId={tripObjectiveId as string}
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
        type={MemberGridType.TripObjective}
        tripObjectiveId={tripObjectiveId}
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

      <LeadTableComponent
        tripObjectiveId={tripObjectiveId as string}
      ></LeadTableComponent>

      <Divider />
      <Card
        title="Photos"
        extra={
          <Link to={"uploadPhotos"}>
            <Button>+</Button>
          </Link>
        }
      >
        <TripObjectiveDetailPhotoComponent
          tripObjectiveId={tripObjectiveId as string}
        ></TripObjectiveDetailPhotoComponent>
      </Card>
    </>
  );
};
export { TripObjectiveDetailComponent };
