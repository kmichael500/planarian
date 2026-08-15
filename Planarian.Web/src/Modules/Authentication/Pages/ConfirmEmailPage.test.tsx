import { render, waitFor } from "@testing-library/react";
import { message } from "antd";
import React, { StrictMode } from "react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { UserService } from "../../User/UserService";
import { ConfirmEmailPage } from "./ConfirmEmailPage";

const renderPage = () =>
  render(
    <StrictMode>
      <MemoryRouter initialEntries={["/confirm-email?code=ABC123"]}>
        <Routes>
          <Route path="/confirm-email" element={<ConfirmEmailPage />} />
          <Route path="/login" element={<div>Login</div>} />
        </Routes>
      </MemoryRouter>
    </StrictMode>
  );

describe("ConfirmEmailPage", () => {
  afterEach(() => {
    jest.restoreAllMocks();
  });

  it("submits a confirmation code only once under React StrictMode", async () => {
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
  });

  it("shows a confirmation failure only once under React StrictMode", async () => {
    const confirmSpy = jest
      .spyOn(UserService, "ConfirmEmail")
      .mockRejectedValue({ message: "The email confirmation code does not exist" });
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
  });
});
