import React, { StrictMode } from "react";
import { fireEvent, render, screen } from "@testing-library/react";
import { PlanarianModal } from "./PlanarianModal";

beforeAll(() => {
  Object.defineProperty(window, "matchMedia", {
    writable: true,
    value: (query: string) => ({
      matches: false,
      media: query,
      onchange: null,
      addListener: () => {},
      removeListener: () => {},
      addEventListener: () => {},
      removeEventListener: () => {},
      dispatchEvent: () => false,
    }),
  });
  Object.defineProperty(HTMLDialogElement.prototype, "showModal", {
    configurable: true,
    value: function showModal(this: HTMLDialogElement) {
      this.setAttribute("open", "");
    },
  });
  Object.defineProperty(HTMLDialogElement.prototype, "close", {
    configurable: true,
    value: function close(this: HTMLDialogElement) {
      this.removeAttribute("open");
      this.dispatchEvent(new Event("close"));
    },
  });
  Object.defineProperty(window, "scrollTo", { configurable: true, value: jest.fn() });
});

it("does not report a close when StrictMode runs mount cleanup", () => {
  const onClose = jest.fn();

  render(
    <StrictMode>
      <PlanarianModal open onClose={onClose}>Content</PlanarianModal>
    </StrictMode>
  );

  expect(onClose).not.toHaveBeenCalled();
});

it("blocks dismissal while a destructive operation is running", () => {
  const onClose = jest.fn();
  render(
    <PlanarianModal
      open
      header="Processing"
      onClose={onClose}
      dismissible={false}
      closeOnBackdrop={false}
    >
      Working
    </PlanarianModal>
  );
  const dialog = screen.getByRole("dialog", { name: "Processing" });
  expect(screen.queryByRole("button", { name: "Close dialog" })).toBeNull();

  const cancelEvent = new Event("cancel", { cancelable: true });
  fireEvent(dialog, cancelEvent);
  expect(cancelEvent.defaultPrevented).toBe(true);
  fireEvent.click(dialog);
  expect(onClose).not.toHaveBeenCalled();
  expect(dialog).toHaveAttribute("open");
});

it("supports disabling backdrop dismissal without disabling the close control", () => {
  const onClose = jest.fn();
  render(
    <PlanarianModal open header="Confirm" onClose={onClose} closeOnBackdrop={false}>
      Confirm action
    </PlanarianModal>
  );
  fireEvent.click(screen.getByRole("dialog", { name: "Confirm" }));
  expect(onClose).not.toHaveBeenCalled();
  fireEvent.click(screen.getByRole("button", { name: "Close dialog" }));
  expect(onClose).toHaveBeenCalledTimes(1);
});

it("does not call onClose again when the parent closes the dialog", () => {
  const onClose = jest.fn();
  const { rerender } = render(
    <PlanarianModal open header="Edit" onClose={onClose}>Content</PlanarianModal>
  );
  rerender(
    <PlanarianModal open={false} header="Edit" onClose={onClose}>Content</PlanarianModal>
  );
  expect(onClose).not.toHaveBeenCalled();
});
