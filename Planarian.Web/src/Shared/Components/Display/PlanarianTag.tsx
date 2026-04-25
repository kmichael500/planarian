import { Tag, TagProps } from "antd";
import { CSSProperties } from "react";
import { useTheme } from "../../../ThemeProvider";
import {
  getPlanarianNeutralTagColor,
  getPlanarianTagPaletteColor,
} from "./planarianTagPalette";

export type PlanarianTagProps = TagProps & {
  colorKey?: string;
};

const PlanarianTag: React.FC<PlanarianTagProps> = (props) => {
  const { effectiveMode } = useTheme();
  const { className, color, colorKey, style, ...rest } = props;
  const resolvedClassName = className
    ? `planarian-tag ${className}`
    : "planarian-tag";

  if (color) {
    return (
      <Tag
        {...rest}
        className={resolvedClassName}
        color={color}
        style={style}
      />
    );
  }

  const paletteColor = colorKey
    ? getPlanarianTagPaletteColor(colorKey, effectiveMode)
    : getPlanarianNeutralTagColor(effectiveMode);

  const resolvedStyle: CSSProperties = {
    background: paletteColor.background,
    color: paletteColor.text,
    borderColor: paletteColor.border,
    ...style,
  };

  return <Tag {...rest} className={resolvedClassName} style={resolvedStyle} />;
};

export { PlanarianTag };
