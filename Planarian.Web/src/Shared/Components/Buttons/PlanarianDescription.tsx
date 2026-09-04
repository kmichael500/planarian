import { CheckOutlined, CopyOutlined } from "@ant-design/icons";
import React, { useEffect, useRef, useState } from "react";
import { Descriptions, DescriptionsProps, Grid } from "antd";
import {
  getCopyTokenElements,
  isWithinCopyToken,
} from "../../Helpers/CopyTokenHelpers";
import { EMPTY_DISPLAY_VALUE } from "../../Helpers/StringHelpers";
import { PlanarianButton } from "./PlanarianButtton";
import "./PlanarianDescription.scss";

const defaultColumns: DescriptionsProps["column"] = {
  xs: 1,
  sm: 1,
  md: 2,
  lg: 3,
  xl: 3,
  xxl: 3,
};

const interactiveSelector =
  'a, button, input, select, textarea, summary, [role="button"], [role="link"], [contenteditable="true"]';
const normalizeCopyText = (value: string | null | undefined) =>
  value?.replace(/\s+/g, " ").trim() ?? "";

const isEmptyCopyText = (value: string | null | undefined) => {
  const normalizedValue = normalizeCopyText(value);
  return !normalizedValue || normalizedValue === EMPTY_DISPLAY_VALUE;
};

const getRenderedCopyText = (element: HTMLElement) => {
  const copyTokens = getCopyTokenElements(element)
    .map((token) => normalizeCopyText(token.textContent))
    .filter(Boolean);
  const chunks: string[] = [];
  let hasTextOutsideTokens = false;
  const walker = document.createTreeWalker(element, NodeFilter.SHOW_TEXT);

  for (let node = walker.nextNode(); node; node = walker.nextNode()) {
    const chunk = normalizeCopyText(node.textContent);
    if (!chunk) continue;

    chunks.push(chunk);
    if (!isWithinCopyToken(node.parentElement)) {
      hasTextOutsideTokens = true;
    }
  }

  return copyTokens.length > 0 && !hasTextOutsideTokens
    ? copyTokens.join(", ")
    : chunks.join(" ");
};

interface CopyableDescriptionValueProps {
  children: React.ReactNode;
  copyLabel: string;
  copyText?: string;
}

const CopyableDescriptionValue = ({
  children,
  copyLabel,
  copyText,
}: CopyableDescriptionValueProps) => {
  const contentRef = useRef<HTMLDivElement>(null);
  const [copied, setCopied] = useState(false);

  useEffect(() => {
    if (!copied) return;
    const resetTimer = window.setTimeout(() => setCopied(false), 1500);
    return () => window.clearTimeout(resetTimer);
  }, [copied]);

  const copyValue = async () => {
    const text = normalizeCopyText(
      copyText ??
        (contentRef.current ? getRenderedCopyText(contentRef.current) : "")
    );

    if (isEmptyCopyText(text)) return;

    try {
      await navigator.clipboard.writeText(text);
      setCopied(true);
    } catch {
      setCopied(false);
    }
  };

  return (
    <div className="planarian-description-copyable-value">
      <div
        ref={contentRef}
        className="planarian-description-copyable-value__content"
      >
        {children}
      </div>
      <PlanarianButton
        aria-label={`Copy ${copyLabel}`}
        className="planarian-description-copyable-value__button"
        icon={copied ? <CheckOutlined /> : <CopyOutlined />}
        neverShowChildren
        onClick={(event) => {
          event.stopPropagation();
          void copyValue();
        }}
        size="small"
        type="text"
      />
      <span
        aria-live="polite"
        className="planarian-description-copyable-value__status"
      >
        {copied ? `${copyLabel} copied` : ""}
      </span>
    </div>
  );
};

type AntDescriptionItem = NonNullable<DescriptionsProps["items"]>[number];

export type PlanarianDescriptionItem = AntDescriptionItem & {
  copyLabel?: string;
  copyText?: string;
};

interface PlanarianDescriptionProps
  extends Omit<DescriptionsProps, "children" | "items"> {
  items?: PlanarianDescriptionItem[];
  copyable?: boolean;
}

const hasCopyableValue = (
  children: React.ReactNode,
  copyText: string | undefined
) => {
  if (copyText !== undefined) return !isEmptyCopyText(copyText);
  if (children === null || children === undefined) return false;
  return typeof children !== "string" || !isEmptyCopyText(children);
};

const PlanarianDescription: React.FC<PlanarianDescriptionProps> = ({
  bordered = true,
  column = defaultColumns,
  copyable = true,
  items = [],
  layout = "horizontal",
  styles,
  ...props
}) => {
  const screens = Grid.useBreakpoint();
  const useCompactLabelWidth = layout === "horizontal" && screens.md === false;

  const mergedStyles: DescriptionsProps["styles"] = {
    ...styles,
    content: {
      wordBreak: "normal",
      overflowWrap: "break-word",
      ...styles?.content,
    },
    ...(useCompactLabelWidth
      ? { label: { width: "40%", ...styles?.label } }
      : {}),
  };

  const renderedItems: NonNullable<DescriptionsProps["items"]> = items.map(
    ({ children, className, copyLabel, copyText, label, ...item }) => {
      const canCopy = copyable && hasCopyableValue(children, copyText);
      const resolvedCopyLabel =
        copyLabel ?? (typeof label === "string" ? label : "value");

      return {
        ...item,
        label,
        className: canCopy
          ? [className, "planarian-description-copyable-item"]
              .filter(Boolean)
              .join(" ")
          : className,
        children: canCopy ? (
          <CopyableDescriptionValue
            copyLabel={resolvedCopyLabel}
            copyText={copyText}
          >
            {children}
          </CopyableDescriptionValue>
        ) : (
          children
        ),
      };
    }
  );

  const handleDescriptionClick = (event: React.MouseEvent<HTMLDivElement>) => {
    const target = event.target;
    if (!(target instanceof Element) || target.closest(interactiveSelector))
      return;

    const copyableCell = target.closest(
      "td.planarian-description-copyable-item"
    );
    if (!copyableCell) return;

    const selection = window.getSelection();
    const hasSelectionInCell =
      !!selection?.toString().trim() &&
      ((selection.anchorNode && copyableCell.contains(selection.anchorNode)) ||
        (selection.focusNode && copyableCell.contains(selection.focusNode)));
    if (hasSelectionInCell) return;

    copyableCell
      .querySelector<HTMLButtonElement>(
        ".planarian-description-copyable-value__button"
      )
      ?.click();
  };

  return (
    <div className="planarian-description" onClick={handleDescriptionClick}>
      <Descriptions
        {...props}
        bordered={bordered}
        column={column}
        items={renderedItems}
        layout={layout}
        styles={mergedStyles}
      />
    </div>
  );
};

export { PlanarianDescription };
