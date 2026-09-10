import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
import { FullscreenOutlined } from "@ant-design/icons";

interface FullScreenControlProps {
  handleClick: () => any;
  position?: {
    top?: string;
    left?: string;
    right?: string;
    bottom?: string;
  };
}

const FullScreenControl = ({
  handleClick,
  position = { top: "150px", left: "10px" },
}: FullScreenControlProps) => {
  return (
    <div
      style={{
        position: "absolute",
        ...position,
      }}
    >
      <PlanarianButton icon={<FullscreenOutlined />} onClick={handleClick} />
    </div>
  );
};

export { FullScreenControl };
