import { Link, RelativeRoutingType } from "react-router-dom";
import { ArrowLeftOutlined } from "@ant-design/icons";
import {
  PlanarianButton,
  PlanarianButtonTypeWithoutIcon,
} from "./PlanarianButtton";

interface BackButtonComponentProps {
  to: string;
  relative?: RelativeRoutingType;
}

type BackButtonComponentType = BackButtonComponentProps &
  PlanarianButtonTypeWithoutIcon;
const BackButtonComponent: React.FC<BackButtonComponentType> = (props) => {
  const { relative = "path" } = props;
  return (
    <Link to={props.to} relative={relative}>
      <PlanarianButton {...props} icon={<ArrowLeftOutlined />}>
        Back
      </PlanarianButton>
    </Link>
  );
};

export { BackButtonComponent };
