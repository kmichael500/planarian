import { Card } from "antd";
import { ProjectCreateButton } from "./project.create.button.component";

const ProjectCardComponent: React.FC = () => {
  return (
    <Card title="Card title" bordered={false}>
      Card content
    </Card>
  );
};

export { ProjectCardComponent };
