import { fireEvent, render, screen } from "@testing-library/react";
import React from "react";
import { ConfirmationModalComponent } from "./ConfirmationModalComponent";

beforeAll(() => {
  Object.defineProperty(window, "matchMedia", {
    writable: true,
    value: () => ({
      matches: false,
      addListener: () => undefined,
      removeListener: () => undefined,
      addEventListener: () => undefined,
      removeEventListener: () => undefined,
      dispatchEvent: () => false,
    }),
  });
});

describe("ConfirmationModalComponent", () => {
  it("requires the full phrase but ignores capitalization and surrounding whitespace", () => {
    const onConfirm = jest.fn();

    render(
      <ConfirmationModalComponent
        confirmationWord="DELETE ALL SINK OR CAVITY TAGS"
        onConfirm={onConfirm}
        okText="Yes"
        autoClose={false}
      >
        Delete
      </ConfirmationModalComponent>
    );

    fireEvent.click(screen.getByRole("button", { name: /delete/i }));

    expect(
      screen.getByText("Type the full phrase shown below to confirm.")
    ).toBeInTheDocument();

    const input = screen.getByRole("textbox");
    const confirmButton = screen.getByRole("button", { name: "check-circle" });

    fireEvent.change(input, {
      target: { value: "  delete all sink or cavity tags  " },
    });

    expect(confirmButton).toBeEnabled();
    fireEvent.click(confirmButton);
    expect(onConfirm).toHaveBeenCalledTimes(1);
  });

  it("does not accept omitted or changed words", () => {
    render(
      <ConfirmationModalComponent
        confirmationWord="DELETE ALL SINK OR CAVITY TAGS"
        okText="Yes"
      >
        Delete
      </ConfirmationModalComponent>
    );

    fireEvent.click(screen.getByRole("button", { name: /delete/i }));

    const input = screen.getByRole("textbox");
    const confirmButton = screen.getByRole("button", { name: "check-circle" });

    fireEvent.change(input, {
      target: { value: "delete all sink cavity tags" },
    });

    expect(confirmButton).toBeDisabled();

    fireEvent.change(input, {
      target: { value: "delete all sink of cavity tags" },
    });

    expect(confirmButton).toBeDisabled();
  });
});
