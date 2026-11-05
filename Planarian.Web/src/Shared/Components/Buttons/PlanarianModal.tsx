import React, { useEffect, useRef, useState } from "react";
import { Grid, Space } from "antd";
import { CloseOutlined } from "@ant-design/icons";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";

const { useBreakpoint } = Grid;

interface DialogModalProps {
  open: boolean;
  onClose?: () => void;
  dismissible?: boolean;
  closeOnBackdrop?: boolean;
  className?: string;
  header?: string | React.ReactNode | (string | React.ReactNode)[];
  footer?: string | React.ReactNode | (string | React.ReactNode)[];
  footerStyle?: React.CSSProperties;
  headerStyle?: React.CSSProperties;
  contentStyle?: React.CSSProperties;
  children?: React.ReactNode;

  width?: string | number;

  height?: string | number;
  maxHeight?: string | number;

  fullScreen?: boolean;
}

export function PlanarianModal({
  open,
  onClose,
  dismissible = true,
  closeOnBackdrop = true,
  className,
  header: headerItems,
  footer: footerItems,
  footerStyle,
  headerStyle,
  contentStyle,
  children,
  width,
  height,
  maxHeight,
  fullScreen = false,
}: DialogModalProps) {
  const dialogRef = useRef<HTMLDialogElement>(null);
  const openRef = useRef(open);
  openRef.current = open;

  const scrollPositionRef = useRef<number>(0);

  const [shouldRenderChildren, setShouldRenderChildren] = useState(false);

  // For responsive full-screen on small devices
  const screens = useBreakpoint();
  const finalFullScreen = !screens.md || fullScreen;

  const computedWidth = finalFullScreen ? "100vw" : width || "80%";
  const contentSized = !finalFullScreen && height === "auto";
  const computedHeight = finalFullScreen
    ? "var(--planarian-viewport-height)"
    : height || "80%";

  const headerContent = Array.isArray(headerItems)
    ? headerItems
    : headerItems
    ? [headerItems]
    : [];
  const footerContent = Array.isArray(footerItems)
    ? footerItems
    : footerItems
    ? [footerItems]
    : [];

  // The first header item goes on the left, the rest on the right
  const leftHeaderItem = headerContent[0];
  const rightHeaderItems = headerContent.slice(1);

  const handleClose = () => {
    // Restore scroll
    document.body.style.position = "";
    document.body.style.top = "";
    document.body.style.width = "";
    window.scrollTo(0, scrollPositionRef.current);

    setShouldRenderChildren(false);

    if (openRef.current) onClose?.();
  };

  useEffect(() => {
    const dialog = dialogRef.current;
    if (!dialog) return;

    if (open && !dialog.open) {
      // Preserve current scroll
      scrollPositionRef.current = window.scrollY;

      // Lock body scroll
      document.body.style.position = "fixed";
      document.body.style.top = `-${scrollPositionRef.current}px`;
      document.body.style.width = "100%";

      // Show the <dialog> now
      dialog.showModal();

      setShouldRenderChildren(true);
    } else if (!open && dialog.open) {
      // Hide children right away
      setShouldRenderChildren(false);

      // Then close the <dialog>
      dialog.close();
    }
  }, [open]);

  useEffect(() => {
    return () => {
      document.body.style.position = "";
      document.body.style.top = "";
      document.body.style.width = "";
      window.scrollTo(0, scrollPositionRef.current);
    };
  }, []);

  useEffect(() => {
    const dialog = dialogRef.current;
    if (!dialog) return;

    dialog.addEventListener("close", handleClose);
    return () => {
      dialog.removeEventListener("close", handleClose);
    };
  }, [onClose]);

  useEffect(() => {
    const dialog = dialogRef.current;
    if (!dialog) return;
    const handleCancel = (event: Event) => {
      if (!dismissible) event.preventDefault();
    };
    dialog.addEventListener("cancel", handleCancel);
    return () => dialog.removeEventListener("cancel", handleCancel);
  }, [dismissible]);

  const handleDialogClick: React.MouseEventHandler<HTMLDialogElement> = (e) => {
    if (dismissible && closeOnBackdrop && e.target === e.currentTarget) {
      onClose?.();
    }
  };

  return (
    <dialog
      className={["planarian-modal", className].filter(Boolean).join(" ")}
      aria-label={typeof leftHeaderItem === "string" ? leftHeaderItem : undefined}
      ref={dialogRef}
      onClick={handleDialogClick}
      style={{
        position: finalFullScreen ? "static" : "fixed",
        top: finalFullScreen ? "unset" : "50%",
        left: finalFullScreen ? "unset" : "50%",
        transform: finalFullScreen ? "none" : "translate(-50%, -50%)",
        display: open ? (contentSized ? "block" : "flex") : "none",
        flexDirection: contentSized ? undefined : "column",
        width: computedWidth,
        height: contentSized ? "fit-content" : computedHeight,
        maxWidth: finalFullScreen ? "100%" : computedWidth,
        maxHeight: finalFullScreen ? "100%" : maxHeight ?? computedHeight,
        margin: 0,
        border: "none",
        borderRadius: finalFullScreen ? 0 : "8px",
        padding: 0,
        overflow: "hidden",
        zIndex: 9999,
        background: "var(--modal-background)",
        color: "var(--text-color)",
        boxShadow: "0 20px 60px rgba(0, 0, 0, 0.35)",
      }}
    >
      <style>
        {`
          dialog:not([open]) {
            display: none !important;
          }
          dialog::backdrop {
            background: rgba(0, 0, 0, 0.4);
            z-index: 9998;
          }
        `}
      </style>

      <header
        style={{
          padding: "1rem",
          borderBottom: "1px solid var(--border-color)",
          flexShrink: 0,
          background: "var(--surface-color)",
          color: "var(--text-color)",
          ...headerStyle,
        }}
      >
        <div
          style={{
            display: "flex",
            alignItems: "center",
          }}
        >
          {/* Left header item (if any) */}
          <div style={{ marginRight: "auto", minWidth: 0 }}>{leftHeaderItem}</div>

          {/* Right side items + close button */}
          <Space>
            {rightHeaderItems.map((item, index) => (
              <div key={index}>{item}</div>
            ))}
            {dismissible && (
              <PlanarianButton
                icon={<CloseOutlined />}
                aria-label="Close dialog"
                onClick={(e) => {
                  e.stopPropagation();
                  dialogRef.current?.close();
                }}
              />
            )}
          </Space>
        </div>
      </header>

      <div
        style={{
          flex: contentSized ? undefined : "1 1 auto",
          minHeight: 0,
          maxHeight: contentSized
            ? "calc(" + (maxHeight || "90dvh") + " - 140px)"
            : undefined,
          overflowY: "auto",
          padding: "1rem",
          background: "var(--modal-background)",
          color: "var(--text-color)",
          ...contentStyle,
        }}
      >
        {shouldRenderChildren && children}
      </div>

      {footerContent.length > 0 && (
        <footer
          style={{
            padding: "1rem",
            borderTop: "1px solid var(--border-color)",
            flexShrink: 0,
            width: "100%",
            background: "var(--surface-color)",
            color: "var(--text-color)",
          }}
        >
          {footerStyle ? (
            <div style={footerStyle}>
              {footerContent.map((item, index) => (
                <div key={index}>{item}</div>
              ))}
            </div>
          ) : (
            <Space
              style={{
                display: "flex",
                width: "100%",
                justifyContent: "flex-end",
                alignItems: "center",
              }}
            >
              {footerContent.map((item, index) => (
                <div key={index}>{item}</div>
              ))}
            </Space>
          )}
        </footer>
      )}
    </dialog>
  );
}
