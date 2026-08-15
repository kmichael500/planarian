import { fireEvent, render, screen } from "@testing-library/react";
import React, { useContext } from "react";
import { MemoryRouter, Route, Routes, useLocation } from "react-router-dom";
import { AppContext } from "../../../Configuration/Context/AppContext";
import { LoginPage } from "./LoginPage";

const mockLogin = jest.fn();

const AppContextOverride: React.FC<{ children: React.ReactNode }> = ({
  children,
}) => {
  const defaultValue = useContext(AppContext);
  return (
    <AppContext.Provider value={{ ...defaultValue, login: mockLogin }}>
      {children}
    </AppContext.Provider>
  );
};

const DestinationProbe = () => {
  const location = useLocation();
  return (
    <div data-testid="destination">
      {`${location.pathname}${location.search}${location.hash}`}
    </div>
  );
};

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
  mockLogin.mockReset();
  mockLogin.mockResolvedValue(undefined);
});

const renderLogin = (redirectUrl: string) =>
  render(
    <MemoryRouter
      initialEntries={[`/login?redirectUrl=${encodeURIComponent(redirectUrl)}`]}
    >
      <AppContextOverride>
        <Routes>
          <Route path="/login" element={<LoginPage />} />
          <Route path="*" element={<DestinationProbe />} />
        </Routes>
      </AppContextOverride>
    </MemoryRouter>
  );

const submitCredentials = () => {
  fireEvent.change(screen.getByLabelText("Email Address"), {
    target: { value: "user@example.com" },
  });
  fireEvent.change(screen.getByLabelText("Password"), {
    target: { value: "correct-password" },
  });
  fireEvent.click(screen.getByRole("button", { name: /Login$/ }));
};

describe("LoginPage post-login redirect safety", () => {
  it.each([
    ["//evil.example/phish", "/"],
    ["https://evil.example/phish", "/"],
    ["javascript:alert(1)", "/"],
    ["/caves/ABC?tab=files#history", "/caves/ABC?tab=files#history"],
  ])(
    "navigates redirect %p only to the sanitized local destination",
    async (redirectUrl, expected) => {
      renderLogin(redirectUrl);

      submitCredentials();

      expect(await screen.findByTestId("destination")).toHaveTextContent(
        expected
      );
      expect(mockLogin).toHaveBeenCalledTimes(1);
    }
  );
});
