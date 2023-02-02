import { EditOutlined } from "@ant-design/icons";
import {
  PlanarianButton,
  PlanarianButtonTypeWithoutIcon,
} from "./PlanarianButtton";

const EditButtonComponentt: React.FC<PlanarianButtonTypeWithoutIcon> = (
  props
) => {
  return (
    <PlanarianButton type="primary" {...props} icon={<EditOutlined />}>
      Edit
    </PlanarianButton>
  );
};

export { EditButtonComponentt };
