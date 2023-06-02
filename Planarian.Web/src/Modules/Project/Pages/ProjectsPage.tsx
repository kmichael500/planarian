import { Card, Col, Row, Spin, Typography } from "antd";
import { useContext, useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { AppContext } from "../../../Configuration/Context/AppContext";
import { CardGridComponent } from "../../../Shared/Components/CardGrid/CardGridComponent";
import { SpinnerCardComponent } from "../../../Shared/Components/SpinnerCard/SpinnerCard";
import { ProjectCreateButtonComponent } from "../Components/ProjectCreateButtonComponent";
import { ProjectVm } from "../Models/ProjectVm";
import { ProjectService } from "../Services/ProjectService";
import { PagedResult } from "../../Search/Models/PagedResult";

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
      <SpinnerCardComponent numberOfCards={16} spinning={isLoading}>
        <CardGridComponent
          noDataDescription="No projects found"
          noDataCreateButton={<ProjectCreateButtonComponent />}
          renderItem={(project) => (
            <Link to={project.id}>
              <Card
                style={{ height: "100%" }}
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
          )}
          items={projects}
        />
      </SpinnerCardComponent>
    </div>
  );
};

export { ProjectsPage };
