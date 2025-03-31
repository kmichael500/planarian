import React, { useRef, useEffect, useState, ReactNode, FC } from "react";
import styled from "styled-components";
import { Grid } from "antd";

const { useBreakpoint } = Grid;

export interface PlanarianModalProps {
  open: boolean;
  onCancel?: () => void;
  title?: string | ReactNode;
  footer?: string | ReactNode;
  children?: ReactNode;
  width?: string; // Accepts fixed values (e.g., "500px") or percentages (e.g., "50%")
  fullScreen?: boolean;
}

const ANIMATION_DURATION = 300; // Duration in milliseconds

// Styled dialog with animations and backdrop dimming
const StyledDialog = styled.dialog<{
  modalWidth: string;
  fullScreen?: boolean;
}>`
  width: ${({ fullScreen, modalWidth }) => (fullScreen ? "100vw" : modalWidth)};
  max-width: 100%;
  height: ${({ fullScreen }) => (fullScreen ? "100vh" : "auto")};
  border: none;
  border-radius: ${({ fullScreen }) => (fullScreen ? "0" : "8px")};
  padding: 0;
  position: relative;
  opacity: 0;
  transform: scale(0.95);
  transition: opacity ${ANIMATION_DURATION}ms ease-out,
    transform ${ANIMATION_DURATION}ms ease-out;

  &.open {
    opacity: 1;
    transform: scale(1);
  }

  &.closing {
    opacity: 0;
    transform: scale(0.95);
  }

  &::backdrop {
    background: rgba(0, 0, 0, 0.5);
  }
`;

// Container for modal content structured as a flex column
const ModalInnerContainer = styled.div`
  display: flex;
  flex-direction: column;
  height: 100%;
`;

// Header that stays fixed at the top
const Header = styled.header`
  padding: 1rem;
  border-bottom: 1px solid #ccc;
  flex: 0 0 auto;
`;

// Content area that will scroll if needed
const Content = styled.div`
  padding: 1rem;
  flex: 1 1 auto;
  overflow-y: auto;
`;

// Footer that stays fixed at the bottom
const Footer = styled.footer`
  padding: 1rem;
  border-top: 1px solid #ccc;
  flex: 0 0 auto;
`;

// Close button positioned at the top-right corner
const CloseButton = styled.button`
  position: absolute;
  top: 0.5rem;
  right: 0.5rem;
  background: transparent;
  border: none;
  font-size: 1.5rem;
  cursor: pointer;
`;

const PlanarianModal: FC<PlanarianModalProps> = ({
  open: isOpen,
  onCancel: onClose,
  title,
  footer,
  children,
  width = "50%",
  fullScreen = false,
}) => {
  const dialogRef = useRef<HTMLDialogElement>(null);
  const [animationClass, setAnimationClass] = useState("");
  // Save scroll position when modal opens
  const scrollPosition = useRef(0);

  // Get antd breakpoints
  const screens = useBreakpoint();
  // If the screen is smaller than md, force fullScreen
  const computedFullScreen = !screens.md ? true : fullScreen;

  // Effect to manage modal animations and body scroll locking
  useEffect(() => {
    const dialog = dialogRef.current;
    if (!dialog) return;

    if (isOpen) {
      // Save current scroll position
      scrollPosition.current = window.pageYOffset;
      // Lock the body scroll by setting a fixed position
      document.body.style.position = "fixed";
      document.body.style.top = `-${scrollPosition.current}px`;
      document.body.style.width = "100%";

      if (!dialog.open) {
        dialog.showModal();
      }
      // Trigger opening animation
      setTimeout(() => {
        setAnimationClass("open");
      }, 0);
    } else {
      if (dialog.open) {
        // Start closing animation
        setAnimationClass("closing");
        const timer = setTimeout(() => {
          dialog.close();
          setAnimationClass("");
          // Restore body scroll and reset styles
          document.body.style.position = "";
          document.body.style.top = "";
          document.body.style.width = "";
          // Restore the previous scroll position
          window.scrollTo(0, scrollPosition.current);
        }, ANIMATION_DURATION);
        return () => clearTimeout(timer);
      }
    }
  }, [isOpen]);

  // Prevent the default cancel behavior (like ESC key) and trigger onClose
  const handleCancel = (e: React.SyntheticEvent<HTMLDialogElement>) => {
    e.preventDefault();
    onClose && onClose();
  };

  // Close modal if clicking on the backdrop (outside modal content)
  const handleDialogClick = (e: React.MouseEvent<HTMLDialogElement>) => {
    if (e.target === dialogRef.current) {
      onClose && onClose();
    }
  };

  const handleCloseClick = () => {
    onClose && onClose();
  };

  return (
    <StyledDialog
      ref={dialogRef}
      modalWidth={width}
      fullScreen={computedFullScreen}
      className={animationClass}
      onCancel={handleCancel}
      onClick={handleDialogClick}
    >
      <ModalInnerContainer>
        {title && (
          <Header>
            {typeof title === "string" ? <h2>{title}</h2> : title}
          </Header>
        )}
        <Content>{children}</Content>
        {footer && <Footer>{footer}</Footer>}
      </ModalInnerContainer>
      <CloseButton onClick={handleCloseClick} aria-label="Close modal">
        &times;
      </CloseButton>
    </StyledDialog>
  );
};

export { PlanarianModal };
