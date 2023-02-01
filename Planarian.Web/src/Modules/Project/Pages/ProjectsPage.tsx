import { Button, Card, Col, Divider, Row, Spin, Typography } from "antd";
import { useContext, useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { AppContext } from "../../../Configuration/Context/AppContext";
import { ProjectCreateButtonComponent } from "../Components/ProjectCreateButtonComponent";
import { ProjectVm } from "../Models/ProjectVm";
import { ProjectService } from "../Services/ProjectService";

const { Title, Paragraph } = Typography;

const ProjectsPage: React.FC = () => {
  let [isLoading, setIsLoading] = useState(true);
  let [projects, setProjects] = useState<ProjectVm[]>();
  const { setHeaderTitle, setHeaderButtons } = useContext(AppContext);

  useEffect(() => {
    setHeaderButtons([<ProjectCreateButtonComponent />]);
    setHeaderTitle(["Projects"]);
  }, []);
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

export { ProjectsPage };
