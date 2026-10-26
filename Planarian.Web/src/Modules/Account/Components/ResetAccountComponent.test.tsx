import React from "react";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { message } from "antd";
import { ResetAccountComponent } from "./ResetAccountComponent";
import { AccountService } from "../Services/AccountService";

jest.mock("../Services/AccountService", () => ({
  AccountService: { ResetAccount: jest.fn(), RetryResetCleanup: jest.fn(), HasPendingResetCleanup: jest.fn().mockResolvedValue(false) },
}));

// Keep the test about the Reset Account response/UX, not the reusable
// typed-confirmation modal implementation.
jest.mock("../../../Shared/Components/Validation/ConfirmationModalComponent", () => ({
  ConfirmationModalComponent: ({
    onConfirm,
    children,
    modalMessage,
  }: {
    onConfirm: () => Promise<void>;
    children: React.ReactNode;
    modalMessage: React.ReactNode;
  }) => (
    <div>
      <div>{modalMessage}</div>
      <button onClick={() => void onConfirm()}>{children}</button>
    </div>
  ),
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

const reset = AccountService.ResetAccount as jest.Mock;
let warning: jest.SpyInstance;
let success: jest.SpyInstance;
let error: jest.SpyInstance;

beforeEach(() => {
  jest.clearAllMocks();
  (AccountService.HasPendingResetCleanup as jest.Mock).mockResolvedValue(false);
  warning = jest.spyOn(message, "warning").mockImplementation(jest.fn());
  success = jest.spyOn(message, "success").mockImplementation(jest.fn());
  error = jest.spyOn(message, "error").mockImplementation(jest.fn());
});

afterEach(() => {
  warning.mockRestore();
  success.mockRestore();
  error.mockRestore();
});

describe("ResetAccountComponent", () => {
  it("warns that server-side archives are deleted", () => {
    render(<ResetAccountComponent />);
    expect(screen.getAllByText(/stored account archives/i)[0]).toBeInTheDocument();
    expect(screen.getByText(/download any archive you wish to keep/i)).toBeInTheDocument();
  });

  it("reports committed reset with incomplete object cleanup as a warning", async () => {
    reset.mockResolvedValue({ relationalResetCompleted: true, storageCleanupCompleted: false });
    render(<ResetAccountComponent />);
    fireEvent.click(screen.getByRole("button", { name: "Delete" }));
    await waitFor(() => expect(warning).toHaveBeenCalledWith(expect.stringContaining("Account data was reset")));
    expect(success).not.toHaveBeenCalled();
    expect(error).not.toHaveBeenCalled();
  });

  it("reports full cleanup success separately from a pre-commit failure", async () => {
    reset.mockResolvedValueOnce({ relationalResetCompleted: true, storageCleanupCompleted: true })
      .mockRejectedValueOnce({ message: "Reset rolled back." });
    render(<ResetAccountComponent />);
    fireEvent.click(screen.getByRole("button", { name: "Delete" }));
    await waitFor(() => expect(success).toHaveBeenCalled());
    fireEvent.click(screen.getByRole("button", { name: "Delete" }));
    await waitFor(() => expect(error).toHaveBeenCalledWith("Reset rolled back."));
    expect(warning).not.toHaveBeenCalled();
  });

  it("restores the retry action after page reload and clears it on cleanup success", async () => {
    (AccountService.HasPendingResetCleanup as jest.Mock).mockResolvedValue(true);
    (AccountService.RetryResetCleanup as jest.Mock).mockResolvedValue({
      relationalResetCompleted: true, storageCleanupCompleted: true
    });
    render(<ResetAccountComponent />);
    const retry = await screen.findByRole("button", { name: /retry stored-file cleanup/i });
    fireEvent.click(retry);
    await waitFor(() => expect(AccountService.RetryResetCleanup).toHaveBeenCalledTimes(1));
    await waitFor(() => expect(success).toHaveBeenCalledWith("Remaining storage cleanup requests were accepted."));
    await waitFor(() => expect(screen.queryByRole("button", { name: /retry stored-file cleanup/i })).toBeNull());
  });

});
