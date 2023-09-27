import { Col, Spin, Tag } from "antd";
import { PresetColorType, PresetStatusColorType } from "antd/es/_util/colors";
import { LiteralUnion } from "antd/es/_util/type";
import { useEffect, useState } from "react";
import { SettingsService } from "../../Setting/Services/SettingsService";
import { splitCamelCase } from "../../../Shared/Helpers/StringHelpers";

export interface TripTagComponentProps {
  tagId: string;
  color?: LiteralUnion<PresetColorType | PresetStatusColorType, string>;
}

const TagComponent: React.FC<TripTagComponentProps> = (props) => {
  let [tagName, setTagName] = useState<string>();
  let [isTagNameLoading, setIsTagNameLoading] = useState(true);

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
      <Tag key={props.tagId} color={props.color}>
        {tagName}
      </Tag>
    </Spin>
  );
};

export { TagComponent };
