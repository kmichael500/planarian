import { useContext, useEffect, useState } from "react";
import { useParams } from "react-router-dom";
import { ProjectVm } from "../Models/ProjectVm";
import { ProjectService } from "../Services/ProjectService";
import { Divider } from "antd";
import {
  MemberGridComponent,
  MemberGridType,
} from "../../../Shared/Components/MemberGrid/MemberGridComponent";
import { AppContext } from "../../../Configuration/Context/AppContext";
import { BackButtonComponent } from "../../../Shared/Components/Buttons/BackButtonComponent";
import { TripsComponent } from "../../Trip/Components/TripsComponent";
import { NotFoundError } from "../../../Shared/Exceptions/PlanarianErrors";

const ProjectPage: React.FC = () => {
  let [project, setProject] = useState<ProjectVm>();
  const { setHeaderTitle, setHeaderButtons } = useContext(AppContext);

  useEffect(() => {
    setHeaderButtons([<BackButtonComponent to={"./.."} />]);
  }, []);

  const { projectId } = useParams();
  if (projectId === undefined) {
    throw new NotFoundError("project");
  }

  useEffect(() => {
    setHeaderTitle([`Project: ${project?.name ?? ""}`]);
  }, [project]);
  useEffect(() => {
    if (project === undefined) {
      const getProject = async () => {
        const projectResponse = await ProjectService.GetProject(projectId);
        setProject(projectResponse);
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
      <TripsComponent projectId={projectId} />
    </>
  );
};

export { ProjectPage };
