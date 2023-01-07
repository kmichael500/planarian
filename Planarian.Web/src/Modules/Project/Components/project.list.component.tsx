import { Row, Col, Card, Spin, Divider, Typography } from "antd";
import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { TripCreateButton } from "../../Trip/Components/trip.create.button.component";
import { ProjectVm } from "../Models/ProjectVm";
import { ProjectService } from "../Services/project.service";
import { ProjectCreateButton } from "./project.create.button.component";

const { Title, Paragraph } = Typography;

const ProjectListComponent: React.FC = () => {
  let [isLoading, setIsLoading] = useState(true);
  let [projects, setProjects] = useState<ProjectVm[]>();

  useEffect(() => {
    if (projects === undefined) {
      const GetProjects = async (): Promise<void> => {
        const response = await ProjectService.GetProjects();
        setProjects(response);
        setIsLoading(false);
      };
      GetProjects();
    }
  });

  return (
    <div className="site-card-wrapper">
      <Row align="middle">
        <Col>
          <Title level={2}>Projects</Title>
        </Col>
        {/* take up rest of space to push others to right and left side */}
        <Col flex="auto"></Col>
        <Col>
          {" "}
          <ProjectCreateButton />
        </Col>
      </Row>
      <Divider />

      <Spin spinning={isLoading} size="large">
        <Row
          gutter={[
            { xs: 8, sm: 8, md: 24, lg: 32 },
            { xs: 8, sm: 8, md: 24, lg: 32 },
          ]}
        >
          {projects?.map((project, index) => (
            <Col key={index} xs={24} sm={12} md={8} lg={6}>
              <Link to={project.id}>
                <Card
                  loading={isLoading}
                  hoverable
                  title={project.name}
                  bordered={false}
                >
                  <Paragraph>
                    Project Members: {project.numberOfProjectMembers}
                  </Paragraph>
                  <Paragraph>Trips: {project.numberOfTrips}</Paragraph>
                </Card>
              </Link>
            </Col>
          ))}
        </Row>
      </Spin>
    </div>
  );
};

export { ProjectListComponent };
