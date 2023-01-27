import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { NotFoundException } from "../../../Shared/Exceptions/NotFoundException";
import { ProjectVm } from "../Models/ProjectVm";
import { ProjectService } from "../Services/project.service";
import { Button, Card, Col, Divider, Row, Spin, Typography } from "antd";
import moment from "moment";
import {
  MemberGridComponent,
  MemberGridType,
} from "../../../Shared/Components/MemberGridComponent";
import { TripObjectiveVm } from "../../Objective/Models/TripObjectiveVm";
import { TripObjectiveCreateButton } from "../../Objective/Components/objective.create.button.component";
import { UserAvatarGroupComponent } from "../../User/Componenets/UserAvatarGroupComponent";
import { TripObjectiveTagComponent } from "../../../Shared/Components/TripObjectiveTagComponent";
const { Title, Text } = Typography;

const ProjectDetailComponent: React.FC = () => {
  let [project, setProject] = useState<ProjectVm>();
  let [trips, setTrips] = useState<TripObjectiveVm[]>();
  let [isTripsLoading, setIsTripsLoading] = useState(true);

  const { projectId } = useParams();
  if (projectId === undefined) {
    throw new NotFoundException();
  }

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
      <Row align="middle" gutter={10}>
        <Col>
          <Title level={2}>{project?.name}</Title>
        </Col>
        {/* take up rest of space to push others to right and left side */}
        <Col flex="auto"></Col>
        <Col>
          <Link to={"./.."}>
            <Button>Back</Button>
          </Link>
        </Col>
        <Col>
          {" "}
          <TripObjectiveCreateButton projectId={projectId} />
        </Col>
      </Row>
      <Divider />

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
      </Row>

      <Spin spinning={isTripsLoading}>
        <Row
          gutter={[
            { xs: 8, sm: 8, md: 24, lg: 32 },
            { xs: 8, sm: 8, md: 24, lg: 32 },
          ]}
        >
          {trips?.map((objective, index) => (
            <Col key={index} xs={24} sm={12} md={8} lg={6}>
              <Card
                title={
                  <>
                    {objective.name}{" "}
                    <Row>
                      <UserAvatarGroupComponent
                        size={"small"}
                        maxCount={4}
                        userIds={objective.tripObjectiveMemberIds}
                      />
                    </Row>
                  </>
                }
                loading={isTripsLoading}
                bordered={false}
                actions={[
                  <Link to={`trip/${objective.id}`}>
                    <Button>View</Button>
                  </Link>,
                ]}
              >
                <>
                  <Row>
                    {objective.tripObjectiveTypeIds.map(
                      (objectiveTypeId, index) => (
                        <Col>
                          <TripObjectiveTagComponent
                            key={index}
                            tripObjectiveId={objectiveTypeId}
                          />
                        </Col>
                      )
                    )}
                  </Row>
                  <Divider />

                  <Row>
                    {" "}
                    <Text>Description: {objective.description}</Text>
                  </Row>
                </>
              </Card>
            </Col>
          ))}
        </Row>
      </Spin>
    </>
  );
};

export { ProjectDetailComponent };
