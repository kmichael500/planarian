import { CheckCircleOutlined } from "@ant-design/icons";
import {
  PlanarianButton,
  PlanarianButtonTypeWithoutIcon,
} from "./PlanarianButtton";

const SubmitButtonComponent: React.FC<PlanarianButtonTypeWithoutIcon> = (
  props
) => {
  return (
    <PlanarianButton
      alwaysShowChildren
      type="primary"
      {...props}
      icon={<CheckCircleOutlined />}
    >
      Submit
    </PlanarianButton>
  );
};

export { SubmitButtonComponent };
