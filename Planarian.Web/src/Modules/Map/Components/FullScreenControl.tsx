import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";
import { FullscreenOutlined } from "@ant-design/icons";

interface FullScreenControlProps {
  handleClick: () => any;
}

const FullScreenControl = ({ handleClick }: FullScreenControlProps) => {
  return (
    <div
      style={{
        position: "absolute",
        top: "150px",
        left: "10px",
        zIndex: 1,
      }}
    >
      <PlanarianButton icon={<FullscreenOutlined />} onClick={handleClick} />
    </div>
  );
};

export { FullScreenControl };
