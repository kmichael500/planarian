import { MailOutlined } from "@ant-design/icons";
import {
  PlanarianButton,
  PlanarianButtonTypeWithoutIcon,
} from "./PlanarianButtton";

const InviteButtonComponent: React.FC<PlanarianButtonTypeWithoutIcon> = (
  props
) => {
  return (
    <PlanarianButton type="primary" {...props} icon={<MailOutlined />}>
      Invite
    </PlanarianButton>
  );
};

export { InviteButtonComponent };
