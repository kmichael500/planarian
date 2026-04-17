import { CSSProperties, HTMLAttributes } from "react";
import { ReactComponent as LogoLightSvg } from "./logo-light.svg";

type LogoIconProps = HTMLAttributes<HTMLSpanElement>;

const scaleSize = (value: CSSProperties["fontSize"], multiplier: number) => {
  if (typeof value === "number") {
    return value * multiplier;
  }

  if (typeof value === "string") {
    const match = value.trim().match(/^(-?\d*\.?\d+)(px)?$/i);

    if (match) {
      const numericValue = Number.parseFloat(match[1]);
      return `${numericValue * multiplier}px`;
    }
  }

  return value;
};

export const LogoIcon = ({ style, ...props }: LogoIconProps) => {
  const fallbackSize = style?.fontSize ? scaleSize(style.fontSize, 2) : undefined;

  const resolvedStyle: CSSProperties = {
    display: "inline-flex",
    alignItems: "center",
    justifyContent: "center",
    lineHeight: 0,
    width: style?.width ?? fallbackSize,
    height: style?.height ?? fallbackSize,
    ...style,
  };

  return (
    <span {...props} style={resolvedStyle}>
      <LogoLightSvg
        style={{
          display: "block",
          width: "100%",
          height: "100%",
          background: "transparent",
        }}
      />
    </span>
  );
};
