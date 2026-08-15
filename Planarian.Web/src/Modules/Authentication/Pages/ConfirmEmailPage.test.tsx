import { render, screen, waitFor } from "@testing-library/react";
import { message } from "antd";
import React, { StrictMode, useContext } from "react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { AppContext } from "../../../Configuration/Context/AppContext";
import { UserService } from "../../User/UserService";
import { ConfirmEmailPage } from "./ConfirmEmailPage";

const refreshSessionMock = jest.fn();

const AppContextOverride: React.FC<{ children: React.ReactNode }> = ({
  children,
}) => {
  const defaultValue = useContext(AppContext);
  return (
    <AppContext.Provider
      value={{ ...defaultValue, refreshSession: refreshSessionMock }}
    >
      {children}
    </AppContext.Provider>
  );
};

const renderPage = () =>
  render(
    <StrictMode>
      <MemoryRouter initialEntries={["/confirm-email?code=ABC123"]}>
        <AppContextOverride>
          <Routes>
            <Route path="/confirm-email" element={<ConfirmEmailPage />} />
            <Route path="/" element={<div>App Home</div>} />
            <Route path="/login" element={<div>Login</div>} />
          </Routes>
        </AppContextOverride>
      </MemoryRouter>
    </StrictMode>
  );

describe("ConfirmEmailPage", () => {
  beforeEach(() => {
    refreshSessionMock.mockReset();
    refreshSessionMock.mockResolvedValue(undefined);
  });

  afterEach(() => {
    jest.restoreAllMocks();
  });

  it("confirms once, refreshes the session, and returns to the app under React StrictMode", async () => {
    const confirmSpy = jest
      .spyOn(UserService, "ConfirmEmail")
      .mockResolvedValue(undefined);
    const successSpy = jest
      .spyOn(message, "success")
      .mockImplementation(() => undefined as any);
    const errorSpy = jest
      .spyOn(message, "error")
      .mockImplementation(() => undefined as any);

    renderPage();

    await waitFor(() => expect(confirmSpy).toHaveBeenCalledTimes(1));
    expect(confirmSpy).toHaveBeenCalledWith("ABC123");
    await waitFor(() =>
      expect(successSpy).toHaveBeenCalledWith("Your email has been verified!")
    );
    expect(successSpy).toHaveBeenCalledTimes(1);
    expect(errorSpy).not.toHaveBeenCalled();
    expect(refreshSessionMock).toHaveBeenCalledTimes(1);
    expect(await screen.findByText("App Home")).toBeInTheDocument();
  });

  it("shows a confirmation failure only once under React StrictMode", async () => {
    const confirmSpy = jest
      .spyOn(UserService, "ConfirmEmail")
      .mockRejectedValue({
        message: "The email confirmation code does not exist",
      });
    const successSpy = jest
      .spyOn(message, "success")
      .mockImplementation(() => undefined as any);
    const errorSpy = jest
      .spyOn(message, "error")
      .mockImplementation(() => undefined as any);

    renderPage();

    await waitFor(() => expect(confirmSpy).toHaveBeenCalledTimes(1));
    await waitFor(() =>
      expect(errorSpy).toHaveBeenCalledWith(
        "The email confirmation code does not exist"
      )
    );
    expect(errorSpy).toHaveBeenCalledTimes(1);
    expect(successSpy).not.toHaveBeenCalled();
    expect(refreshSessionMock).not.toHaveBeenCalled();
    expect(await screen.findByText("Login")).toBeInTheDocument();
  });

  it("keeps a successful confirmation successful when session refresh fails", async () => {
    const confirmSpy = jest
      .spyOn(UserService, "ConfirmEmail")
      .mockResolvedValue(undefined);
    const successSpy = jest
      .spyOn(message, "success")
      .mockImplementation(() => undefined as any);
    const errorSpy = jest
      .spyOn(message, "error")
      .mockImplementation(() => undefined as any);
    refreshSessionMock.mockRejectedValue({ message: "Session refresh failed" });

    renderPage();

    await waitFor(() => expect(confirmSpy).toHaveBeenCalledTimes(1));
    await waitFor(() => expect(refreshSessionMock).toHaveBeenCalledTimes(1));
    expect(successSpy).toHaveBeenCalledWith("Your email has been verified!");
    expect(errorSpy).not.toHaveBeenCalled();
    expect(await screen.findByText("Login")).toBeInTheDocument();
  });
});
