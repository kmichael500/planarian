import { Spin } from "antd";
import { PresetColorType, PresetStatusColorType } from "antd/es/_util/colors";
import { LiteralUnion } from "antd/es/_util/type";
import { useEffect, useState } from "react";
import { SettingsService } from "../../Setting/Services/SettingsService";
import { defaultIfEmpty } from "../../../Shared/Helpers/StringHelpers";
import { PlanarianTag } from "../../../Shared/Components/Display/PlanarianTag";
import { SelectListItem } from "../../../Shared/Models/SelectListItem";

export interface TripTagComponentProps {
  tagId?: string;
  item?: SelectListItem<string>;
  color?: LiteralUnion<PresetColorType | PresetStatusColorType>;
}

const TagComponent: React.FC<TripTagComponentProps> = (props) => {
  const [tagName, setTagName] = useState<string>();
  const [isTagNameLoading, setIsTagNameLoading] = useState(true);

  useEffect(() => {
    if (props.item) {
      setIsTagNameLoading(false);
      return;
    }

    if (!props.tagId) {
      setTagName(undefined);
      setIsTagNameLoading(false);
      return;
    }

    const getTagName = async () => {
      try {
        const tripNameResponse = await SettingsService.GetTagName(props.tagId!);
        setTagName(tripNameResponse);
      } catch (error) {
        setTagName("Error");
      }
      setIsTagNameLoading(false);
    };

    getTagName();
  }, [props.item, props.tagId]);

  const displayValue = props.item?.display ?? tagName;
  const colorKey = props.item?.value ?? props.tagId;

  return (
    <Spin spinning={!props.item && isTagNameLoading}>
      <PlanarianTag
        key={colorKey}
        color={props.color}
        colorKey={colorKey}
        style={{
          whiteSpace: "pre-wrap",
          wordBreak: "break-word",
        }}
      >
        {defaultIfEmpty(displayValue)}
      </PlanarianTag>
    </Spin>
  );
};

export { TagComponent };
