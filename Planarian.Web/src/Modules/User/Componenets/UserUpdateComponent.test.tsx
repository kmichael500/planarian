import { render, screen, waitFor } from "@testing-library/react";
import React from "react";
import userEvent from "@testing-library/user-event";
import { UserService } from "../UserService";
import { UserUpdateComponent } from "./UserUpdateComponent";

jest.mock("../UserService", () => ({
  UserService: {
    GetCurrentUser: jest.fn(),
    UpdateCurrentUser: jest.fn(),
    UpdateCurrentUserPassword: jest.fn(),
  },
}));

const mockedUserService = UserService as jest.Mocked<typeof UserService>;

beforeAll(() => {
  Object.defineProperty(window, "matchMedia", {
    writable: true,
    value: () => ({
      matches: false,
      addListener: () => {},
      removeListener: () => {},
      addEventListener: () => {},
      removeEventListener: () => {},
      dispatchEvent: () => false,
    }),
  });
});

beforeEach(() => {
  jest.clearAllMocks();
  mockedUserService.GetCurrentUser.mockResolvedValue({
    firstName: "Test",
    lastName: "User",
    emailAddress: "old@example.com",
    phoneNumber: "+16155551212",
  });
  mockedUserService.UpdateCurrentUser.mockResolvedValue(undefined);
  mockedUserService.UpdateCurrentUserPassword.mockResolvedValue(undefined);
});

describe("UserUpdateComponent sensitive changes", () => {
  it("submits the current password with an email change", async () => {
    render(<UserUpdateComponent />);
    const email = await screen.findByLabelText("Email Address");

    userEvent.clear(email);
    userEvent.type(email, "new@example.com");
    userEvent.type(
      await screen.findByLabelText("Current Password"),
      "CurrentPassword!123"
    );
    userEvent.click(screen.getByRole("button", { name: /Save/i }));

    await waitFor(() =>
      expect(mockedUserService.UpdateCurrentUser).toHaveBeenCalledTimes(1)
    );
    expect(mockedUserService.UpdateCurrentUser).toHaveBeenCalledWith(
      expect.objectContaining({
        emailAddress: "new@example.com",
        currentPassword: "CurrentPassword!123",
      })
    );
  });

  it("submits the current and new passwords without the confirmation field", async () => {
    render(<UserUpdateComponent />);
    await screen.findByLabelText("Email Address");
    userEvent.click(screen.getByRole("button", { name: /Change Password/i }));

    userEvent.type(
      await screen.findByLabelText("Current Password"),
      "CurrentPassword!123"
    );
    userEvent.type(screen.getByLabelText("New Password"), "NewPassword!123");
    userEvent.type(
      screen.getByLabelText("Confirm Password"),
      "NewPassword!123"
    );
    userEvent.click(screen.getByRole("button", { name: /Save/i }));

    await waitFor(() =>
      expect(mockedUserService.UpdateCurrentUserPassword).toHaveBeenCalledTimes(
        1
      )
    );
    expect(mockedUserService.UpdateCurrentUserPassword).toHaveBeenCalledWith({
      currentPassword: "CurrentPassword!123",
      password: "NewPassword!123",
    });
  });

  it("does not retain a current password after an email change is abandoned", async () => {
    render(<UserUpdateComponent />);
    const email = await screen.findByLabelText("Email Address");

    userEvent.clear(email);
    userEvent.type(email, "new@example.com");
    const currentPassword = await screen.findByLabelText("Current Password");
    userEvent.type(currentPassword, "secret-current-password");
    userEvent.clear(email);
    userEvent.type(email, "old@example.com");

    await waitFor(() =>
      expect(
        screen.queryByLabelText("Current Password")
      ).not.toBeInTheDocument()
    );
    userEvent.click(screen.getByRole("button", { name: /Save/i }));

    await waitFor(() =>
      expect(mockedUserService.UpdateCurrentUser).toHaveBeenCalled()
    );
    expect(
      mockedUserService.UpdateCurrentUser.mock.calls[0][0]
    ).not.toHaveProperty("currentPassword");
  });
});
