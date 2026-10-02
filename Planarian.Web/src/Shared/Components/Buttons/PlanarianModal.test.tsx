import React, { StrictMode } from "react";
import { render } from "@testing-library/react";
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