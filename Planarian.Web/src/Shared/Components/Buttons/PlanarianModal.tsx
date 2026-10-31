import React, { useEffect, useRef, useState } from "react";
import { Grid, Space } from "antd";
import { CloseOutlined } from "@ant-design/icons";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";

const { useBreakpoint } = Grid;

interface DialogModalProps {
  open: boolean;
  onClose?: () => void;
  header?: string | React.ReactNode | (string | React.ReactNode)[];
  footer?: string | React.ReactNode | (string | React.ReactNode)[];
  footerStyle?: React.CSSProperties;
  headerStyle?: React.CSSProperties;
  contentStyle?: React.CSSProperties;
  children?: React.ReactNode;

  width?: string | number;

  height?: string | number;

  fullScreen?: boolean;
}

export function PlanarianModal({
  open,
  onClose,
  header: headerItems,
  footer: footerItems,
  footerStyle,
  headerStyle,
  contentStyle,
  children,
  width,
  height,
  fullScreen = false,
}: DialogModalProps) {
  const dialogRef = useRef<HTMLDialogElement>(null);

  const scrollPositionRef = useRef<number>(0);

  const [shouldRenderChildren, setShouldRenderChildren] = useState(false);

  const screens = useBreakpoint();
  const finalFullScreen = !screens.md || fullScreen;

  const computedWidth = finalFullScreen ? "100vw" : width || "80%";
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

  const leftHeaderItem = headerContent[0];
  const rightHeaderItems = headerContent.slice(1);

  const handleClose = () => {
    document.body.style.position = "";
    document.body.style.top = "";
    document.body.style.width = "";
    window.scrollTo(0, scrollPositionRef.current);

    setShouldRenderChildren(false);

    onClose?.();
  };

  useEffect(() => {
    const dialog = dialogRef.current;
    if (!dialog) return;

    if (open && !dialog.open) {
      scrollPositionRef.current = window.scrollY;

      document.body.style.position = "fixed";
      document.body.style.top = `-${scrollPositionRef.current}px`;
      document.body.style.width = "100%";

      dialog.showModal();

      // Delay child mounting until the native dialog is open.
      requestAnimationFrame(() => {
        setShouldRenderChildren(true);
      });
    } else if (!open && dialog.open) {
      setShouldRenderChildren(false);

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

  const handleDialogClick: React.MouseEventHandler<HTMLDialogElement> = (e) => {
    if (dialogRef.current && e.target === dialogRef.current) {
      onClose?.();
    }
  };

  return (
    <dialog
      className="planarian-modal"
      ref={dialogRef}
      onClick={handleDialogClick}
      style={{
        position: finalFullScreen ? "static" : "fixed",
        top: finalFullScreen ? "unset" : "50%",
        left: finalFullScreen ? "unset" : "50%",
        transform: finalFullScreen ? "none" : "translate(-50%, -50%)",
        display: "flex",
        flexDirection: "column",
        width: computedWidth,
        height: computedHeight,
        maxWidth: finalFullScreen ? "100%" : computedWidth,
        maxHeight: finalFullScreen ? "100%" : computedHeight,
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
          <div style={{ marginRight: "auto", minWidth: 0 }}>{leftHeaderItem}</div>

          <Space>
            {rightHeaderItems.map((item, index) => (
              <div key={index}>{item}</div>
            ))}
            <PlanarianButton
              icon={<CloseOutlined />}
              aria-label="Close"
              onClick={(e) => {
                e.stopPropagation();
                setShouldRenderChildren(false);
                dialogRef.current?.close();
              }}
            />
          </Space>
        </div>
      </header>

      <div
        style={{
          flex: 1,
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
