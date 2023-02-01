import { useContext, useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { NotFoundException } from "../../../Shared/Exceptions/NotFoundException";
import { ProjectVm } from "../Models/ProjectVm";
import { ProjectService } from "../Services/ProjectService";
import { Button, Card, Col, Divider, Row, Spin, Typography } from "antd";
import { CheckCircleOutlined, CloseCircleOutlined } from "@ant-design/icons";
import {
  MemberGridComponent,
  MemberGridType,
} from "../../../Shared/Components/MemberGridComponent";
import { TripVm } from "../../Trip/Models/TripVm";
import { UserAvatarGroupComponent } from "../../User/Componenets/UserAvatarGroupComponent";
import { TagComponent } from "../../Tag/Components/TagComponent";
import { TripCreateButtonComponent } from "../../Trip/Components/TripCreateButtonComponent";
import { AppContext } from "../../../Configuration/Context/AppContext";

const { Title, Text } = Typography;

const ProjectPage: React.FC = () => {
  let [project, setProject] = useState<ProjectVm>();
  const { setHeaderTitle, headerButtons, setHeaderButtons } =
    useContext(AppContext);

  useEffect(() => {
    setHeaderButtons([
      <Link to={"./.."}>
        <Button>Back</Button>
      </Link>,
    ]);
  }, []);
  let [trips, setTrips] = useState<TripVm[]>();
  let [isTripsLoading, setIsTripsLoading] = useState(true);

  const { projectId } = useParams();
  if (projectId === undefined) {
    throw new NotFoundException();
  }

  useEffect(() => {
    setHeaderTitle(["Project: " + project?.name]);
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
          <Title level={3}>Trips</Title>
        </Col>
        {/* take up rest of space to push others to right and left side */}
        <Col flex="auto"></Col>
        <Col>
          <TripCreateButtonComponent projectId={projectId} />
        </Col>
      </Row>

      <Spin spinning={isTripsLoading}>
        <Row
          gutter={[
            { xs: 8, sm: 8, md: 24, lg: 32 },
            { xs: 8, sm: 8, md: 24, lg: 32 },
          ]}
        >
          {trips?.map((trip, index) => (
            <Col key={index} xs={24} sm={12} md={8} lg={6}>
              <Link to={`trip/${trip.id}`}>
                <Card
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
                        <Col>
                          <TagComponent key={index} tagId={tagId} />
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
            </Col>
          ))}
        </Row>
      </Spin>
    </>
  );
};

export { ProjectPage };
