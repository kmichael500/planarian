import { useContext, useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { NotFoundException } from "../../../Shared/Exceptions/NotFoundException";
import { ProjectVm } from "../Models/ProjectVm";
import { ProjectService } from "../Services/ProjectService";
import { Card, Col, Divider, Row, Spin, Typography } from "antd";
import { CheckCircleOutlined, CloseCircleOutlined } from "@ant-design/icons";
import {
  MemberGridComponent,
  MemberGridType,
} from "../../../Shared/Components/MemberGrid/MemberGridComponent";
import { TripVm } from "../../Trip/Models/TripVm";
import { UserAvatarGroupComponent } from "../../User/Componenets/UserAvatarGroupComponent";
import { TagComponent } from "../../Tag/Components/TagComponent";
import { TripCreateButtonComponent } from "../../Trip/Components/TripCreateButtonComponent";
import { AppContext } from "../../../Configuration/Context/AppContext";
import { BackButtonComponent } from "../../../Shared/Components/Buttons/BackButtonComponent";
import { CardGridComponent } from "../../../Shared/Components/CardGrid/CardGridComponent";
import { SpinnerCardComponent } from "../../../Shared/Components/SpinnerCard/SpinnerCard";

const { Title, Text } = Typography;

const ProjectPage: React.FC = () => {
  let [project, setProject] = useState<ProjectVm>();
  const { setHeaderTitle, headerButtons, setHeaderButtons } =
    useContext(AppContext);

  useEffect(() => {
    setHeaderButtons([<BackButtonComponent to={"./.."} />]);
  }, []);
  let [trips, setTrips] = useState<TripVm[]>();
  let [isTripsLoading, setIsTripsLoading] = useState(true);

  const { projectId } = useParams();
  if (projectId === undefined) {
    throw new NotFoundException();
  }

  useEffect(() => {
    setHeaderTitle([`Project: ${project?.name ?? ""}`]);
  }, [project]);
  useEffect(() => {
    if (project === undefined) {
      const getProject = async () => {
        const projectResponse = await ProjectService.GetProject(projectId);
        setProject(projectResponse);
        const tripsResponse = await ProjectService.GetTrips(projectId);

        setTrips(tripsResponse);
        setIsTripsLoading(false);
      };
      getProject();
    }
  });

  return (
    <>
      <MemberGridComponent
        type={MemberGridType.Project}
        projectId={projectId}
      ></MemberGridComponent>
      <Divider></Divider>
      <Row align="middle">
        <Col>
          <Typography.Title level={3}>Trips</Typography.Title>
        </Col>
        {/* take up rest of space to push others to right and left side */}
        <Col flex="auto"></Col>
        <Col>
          <TripCreateButtonComponent projectId={projectId} />
        </Col>
      </Row>

      <SpinnerCardComponent spinning={isTripsLoading}>
        <CardGridComponent
          items={trips?.map((trip) => ({
            item: (
              <Link to={`trip/${trip.id}`}>
                <Card
                  style={{ height: "100%" }}
                  title={
                    <>
                      {trip.name}{" "}
                      <Row>
                        <UserAvatarGroupComponent
                          size={"small"}
                          maxCount={4}
                          userIds={trip.tripMemberIds}
                        />
                      </Row>
                    </>
                  }
                  loading={isTripsLoading}
                  bordered={false}
                  hoverable
                >
                  <>
                    <Row>
                      {trip.tripTagTypeIds.map((tagId, index) => (
                        <Col key={tagId}>
                          <TagComponent tagId={tagId} />
                        </Col>
                      ))}
                    </Row>
                    <Divider />

                    <Row>
                      <Col>
                        <Text>Description: {trip.description}</Text>
                      </Col>
                    </Row>
                    <Row>
                      <Col>
                        <Text>
                          Trip Report:{" "}
                          {trip.isTripReportCompleted ? (
                            <CheckCircleOutlined />
                          ) : (
                            <CloseCircleOutlined />
                          )}
                        </Text>
                      </Col>
                    </Row>
                    <Row>
                      <Col>
                        <Text>Photos: {trip.numberOfPhotos}</Text>
                      </Col>
                    </Row>
                  </>
                </Card>
              </Link>
            ),
            key: trip.id,
          }))}
        />
      </SpinnerCardComponent>
    </>
  );
};

export { ProjectPage };
