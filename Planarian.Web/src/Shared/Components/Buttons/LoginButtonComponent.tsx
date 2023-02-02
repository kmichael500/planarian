import { LoginOutlined } from "@ant-design/icons";
import { Link } from "react-router-dom";
import {
  PlanarianButton,
  PlanarianButtonTypeWithoutIcon,
} from "./PlanarianButtton";

const LoginButtonComponent: React.FC<PlanarianButtonTypeWithoutIcon> = (
  props
) => {
  return (
    <Link to={"./login"}>
      <PlanarianButton {...props} icon={<LoginOutlined />}>
        Login
      </PlanarianButton>
    </Link>
  );
};

export { LoginButtonComponent };
