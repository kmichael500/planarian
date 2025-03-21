import { Col, Spin, Tag } from "antd";
import { PresetColorType, PresetStatusColorType } from "antd/es/_util/colors";
import { LiteralUnion } from "antd/es/_util/type";
import { useEffect, useState } from "react";
import { SettingsService } from "../../Setting/Services/SettingsService";
import { defaultIfEmpty } from "../../../Shared/Helpers/StringHelpers";

export interface TripTagComponentProps {
  tagId: string;
  color?: LiteralUnion<PresetColorType | PresetStatusColorType, string>;
}

export interface TripTagComponentProps {
  tagId: string;
  color?: LiteralUnion<PresetColorType | PresetStatusColorType, string>;
}
function stringToColor(str: string): string {
  const hash = stringToHash(str);
  const hue = hash % 360;
  return hslToHex(hue, 70, 80); // Adjusted for more distinct pastel colors
}

function stringToHash(str: string): number {
  let hash = 0;
  for (let i = 0; i < str.length; i++) {
    hash = str.charCodeAt(i) + ((hash << 5) - hash);
  }
  return hash;
}

function hslToHex(h: number, s: number, l: number): string {
  const hNormalized = h / 360;
  const sNormalized = s / 100;
  const lNormalized = l / 100;

  const a = sNormalized * Math.min(lNormalized, 1 - lNormalized);
  const f = (n: number) => {
    const k = (n + hNormalized * 12) % 12;
    const color = lNormalized - a * Math.max(Math.min(k - 3, 9 - k, 1), -1);
    return Math.round(255 * color)
      .toString(16)
      .padStart(2, "0");
  };

  return `#${f(0)}${f(8)}${f(4)}`;
}

function getTextColor(bgColor: string): string {
  const r = parseInt(bgColor.substring(1, 3), 16);
  const g = parseInt(bgColor.substring(3, 5), 16);
  const b = parseInt(bgColor.substring(5, 7), 16);
  const brightness = (r * 299 + g * 587 + b * 114) / 1000;
  return brightness > 128 ? "#000000" : "#FFFFFF";
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

  const backgroundColor = stringToColor(tagName || props.tagId);
  const textColor = getTextColor(backgroundColor);

  return (
    <Spin spinning={isTagNameLoading}>
      <Tag
        key={props.tagId}
        // color={backgroundColor}
        style={{
          color: textColor,
          whiteSpace: "pre-wrap",
          wordBreak: "break-word",
        }}
      >
        {defaultIfEmpty(tagName)}
      </Tag>
    </Spin>
  );
};

export { TagComponent };
