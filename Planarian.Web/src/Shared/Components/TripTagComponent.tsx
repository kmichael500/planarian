import { Spin, Tag } from "antd";
import { PresetColorType, PresetStatusColorType } from "antd/es/_util/colors";
import { LiteralUnion } from "antd/es/_util/type";
import { useState, useEffect } from "react";
import { SettingsService } from "../../Modules/Settings/Services/SettingsService";

export interface TripTagComponentProps {
  tagId: string;
  color?: LiteralUnion<PresetColorType | PresetStatusColorType, string>;
}
const TripTagComponent: React.FC<TripTagComponentProps> = (props) => {
  let [tagName, setTagName] = useState<string>();
  let [isTagNameLoading, setIsTagNameLoading] = useState(true);

  useEffect(() => {
    if (tagName === undefined) {
      const getTagName = async () => {
        const tripNameResponse = await SettingsService.GetTagName(props.tagId);
        setTagName(tripNameResponse);
        setIsTagNameLoading(false);
      };
      getTagName();
    }
  });

  return (
    <Spin spinning={isTagNameLoading}>
      <Tag key={props.tagId} color={props.color}>
        {tagName}
      </Tag>
    </Spin>
  );
};

export { TripTagComponent };
