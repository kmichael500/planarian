import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import React, { useContext } from "react";
import { MemoryRouter } from "react-router-dom";
import { AppContext } from "../../../Configuration/Context/AppContext";
import { HttpHelpers } from "../../../Shared/Helpers/HttpHelpers";
import { LoginPage } from "./LoginPage";

const mockNavigate = jest.fn();
let mockLocationSearch = "";
const mockLogin = jest.fn();

jest.mock("react-router-dom", () => ({
  ...jest.requireActual("react-router-dom"),
  useLocation: () => ({
    pathname: "/login",
    search: mockLocationSearch,
    hash: "",
    state: null,
    key: "test",
  }),
  useNavigate: () => mockNavigate,
}));

const AppContextOverride: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const defaultValue = useContext(AppContext);
  return (
    <AppContext.Provider value={{ ...defaultValue, login: mockLogin }}>
      {children}
    </AppContext.Provider>
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
  jest.restoreAllMocks();
  mockNavigate.mockReset();
  mockLogin.mockReset();
  mockLogin.mockResolvedValue(undefined);
  mockLocationSearch = "";
});

const renderLogin = () =>
  render(
    <MemoryRouter>
      <AppContextOverride>
        <LoginPage />
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
  it("navigates to the redirect sanitized by HttpHelpers", async () => {
    mockLocationSearch = "?redirectUrl=%2F%2Fevil.example%2Fphish";
    const redirectSpy = jest
      .spyOn(HttpHelpers, "GetLocalRedirectUrl")
      .mockReturnValue("/safe-destination");
    renderLogin();

    submitCredentials();

    await waitFor(() => {
      expect(redirectSpy).toHaveBeenCalledWith("//evil.example/phish");
      expect(mockNavigate).toHaveBeenCalledWith("/safe-destination");
    });
  });
});
