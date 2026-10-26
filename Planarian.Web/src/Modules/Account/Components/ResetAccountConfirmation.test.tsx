import React from "react";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { message } from "antd";
import { ResetAccountComponent } from "./ResetAccountComponent";
import { AccountService } from "../Services/AccountService";

jest.mock("../Services/AccountService", () => ({
  AccountService: {
    ResetAccount: jest.fn(),
    HasPendingResetCleanup: jest.fn().mockResolvedValue(false),
    RetryResetCleanup: jest.fn(),
  },
}));

jest.mock("../../../Shared/Components/SignalRProgress/SignalRProgressComponent", () => ({
  SignalRProgressComponent: () => <span>Reset progress</span>,
}));

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

beforeEach(() => {
  (AccountService.HasPendingResetCleanup as jest.Mock).mockResolvedValue(false);
});

it("requires the real typed confirmation before a destructive request", async () => {
  const reset = AccountService.ResetAccount as jest.Mock;
  reset.mockResolvedValue({ relationalResetCompleted: true, storageCleanupCompleted: true });
  const success = jest.spyOn(message, "success").mockImplementation(jest.fn());

  try {
    render(<ResetAccountComponent />);
    fireEvent.click(screen.getByRole("button", { name: /delete/i }));
    // PlanarianButton collapses labels at the jsdom breakpoint; the icon remains accessible.
    const confirm = screen.getByRole("button", { name: /^(yes|check-circle)$/i });
    expect(confirm).toBeDisabled();
    expect(reset).not.toHaveBeenCalled();

    const input = screen.getByRole("textbox");
    fireEvent.change(input, { target: { value: "DELETE ALMOST EVERYTHING" } });
    expect(confirm).toBeDisabled();
    fireEvent.change(input, { target: { value: "DELETE EVERYTHING" } });
    expect(confirm).toBeEnabled();
    fireEvent.click(confirm);
    await waitFor(() => expect(reset).toHaveBeenCalledTimes(1));
    await waitFor(() => expect(success).toHaveBeenCalled());
    await waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());
  } finally {
    success.mockRestore();
  }
});
