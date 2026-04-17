import { Col, Spin } from "antd";
import { PresetColorType, PresetStatusColorType } from "antd/es/_util/colors";
import { LiteralUnion } from "antd/es/_util/type";
import { useEffect, useState } from "react";
import { SettingsService } from "../../Setting/Services/SettingsService";
import { defaultIfEmpty } from "../../../Shared/Helpers/StringHelpers";
import { PlanarianTag } from "../../../Shared/Components/Display/PlanarianTag";

export interface TripTagComponentProps {
  tagId: string;
  color?: LiteralUnion<PresetColorType | PresetStatusColorType>;
}

const TagComponent: React.FC<TripTagComponentProps> = (props) => {
  const [tagName, setTagName] = useState<string>();
  const [isTagNameLoading, setIsTagNameLoading] = useState(true);

  useEffect(() => {
    const getTagName = async () => {
      try {
        const tripNameResponse = await SettingsService.GetTagName(props.tagId);
        setTagName(tripNameResponse);
      } catch (error) {
        setTagName("Error");
      }
      setIsTagNameLoading(false);
    };

    getTagName();
  }, [props.tagId]);

  return (
    <Spin spinning={isTagNameLoading}>
      <PlanarianTag
        key={props.tagId}
        color={props.color}
        colorKey={props.tagId}
        style={{
          whiteSpace: "pre-wrap",
          wordBreak: "break-word",
        }}
      >
        {defaultIfEmpty(tagName)}
      </PlanarianTag>
    </Spin>
  );
};

export { TagComponent };
