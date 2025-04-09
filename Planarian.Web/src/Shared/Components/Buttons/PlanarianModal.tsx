import React, { useEffect, useRef, useState } from "react";
import { Grid, Space, Spin } from "antd";
import { CloseOutlined } from "@ant-design/icons";
import { PlanarianButton } from "../../../Shared/Components/Buttons/PlanarianButtton";

const { useBreakpoint } = Grid;

interface DialogModalProps {
  open: boolean;
  onClose?: () => void;
  header?: string | React.ReactNode | (string | React.ReactNode)[];
  footer?: string | React.ReactNode | (string | React.ReactNode)[];
  footerStyle?: React.CSSProperties; // New prop for footer style override
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
  children,
  width,
  height,
  fullScreen = false,
}: DialogModalProps) {
  const dialogRef = useRef<HTMLDialogElement>(null);

  const scrollPositionRef = useRef<number>(0);

  const [shouldRenderChildren, setShouldRenderChildren] = useState(false);

  // For responsive full-screen on small devices
  const screens = useBreakpoint();
  const finalFullScreen = !screens.md || fullScreen;

  const computedWidth = finalFullScreen ? "100vw" : width || "80%";
  const computedHeight = finalFullScreen
    ? "calc(var(--vh, 1vh) * 100)"
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

    onClose?.();
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

      // Wait until next frame so the dialog is definitely "open",
      // then render children
      requestAnimationFrame(() => {
        setShouldRenderChildren(true);
      });
    } else if (!open && dialog.open) {
      // Hide children right away
      setShouldRenderChildren(false);

      // Then close the <dialog>
      dialog.close();
    }
  }, [open]);

  useEffect(() => {
    return () => {
      // this runs on unmount / route change
      // restore scroll when the modal is closed
      handleClose();
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
      // The user clicked outside the content
      onClose?.();
    }
  };

  return (
    <dialog
      ref={dialogRef}
      onClick={handleDialogClick}
      style={{
        position: finalFullScreen ? "static" : "fixed",
        top: finalFullScreen ? "unset" : "50%",
        left: finalFullScreen ? "unset" : "50%",
        transform: finalFullScreen ? "none" : "translate(-50%, -50%)",
        // Use flex layout to pin header/footer and scroll the content area
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
          borderBottom: "1px solid #ddd",
          flexShrink: 0,
        }}
      >
        <div
          style={{
            display: "flex",
            alignItems: "center",
          }}
        >
          {/* Left header item (if any) */}
          <div style={{ marginRight: "auto" }}>{leftHeaderItem}</div>

          {/* Right side items + close button */}
          <Space>
            {rightHeaderItems.map((item, index) => (
              <div key={index}>{item}</div>
            ))}
            <PlanarianButton
              icon={<CloseOutlined />}
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
        }}
      >
        {shouldRenderChildren && children}
      </div>

      {footerContent.length > 0 && (
        <footer
          style={{
            padding: "1rem",
            borderTop: "1px solid #ddd",
            flexShrink: 0,
            width: "100%",
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
