import { LoginOutlined } from "@ant-design/icons";
import { Link } from "react-router-dom";
import {
  PlanarianButton,
  PlanarianButtonTypeWithoutIcon,
} from "./PlanarianButtton";

interface LoginButtonComponentProps extends PlanarianButtonTypeWithoutIcon {
  invitationCode?: string;
}

const LoginButtonComponent: React.FC<LoginButtonComponentProps> = (props) => {
  const { invitationCode, ...restProps } = props;
  const linkTo = invitationCode
    ? `/login?invitationCode=${invitationCode}`
    : "/login";

  return (
    <Link to={linkTo}>
      <PlanarianButton {...restProps} icon={<LoginOutlined />}>
        Login
      </PlanarianButton>
    </Link>
  );
};

export { LoginButtonComponent };
