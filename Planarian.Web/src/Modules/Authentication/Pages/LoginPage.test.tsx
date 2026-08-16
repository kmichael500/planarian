import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { message } from "antd";
import React, { useContext } from "react";
import {
  MemoryRouter,
  Route,
  Routes,
  useLocation,
} from "react-router-dom";
import { AppContext } from "../../../Configuration/Context/AppContext";
import { ClientRoutes } from "../../../Configuration/Routing/ClientRoutes.generated";
import { ApiExceptionType } from "../../../Shared/Models/ApiErrorResponse";
import { MessageDeliveryStatus } from "../../../Shared/Models/MessageDeliveryStatus";
import { LoginPage } from "./LoginPage";

const loginMock = jest.fn();
let errorSpy: jest.SpyInstance;

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
});

const AppContextOverride: React.FC<{ children: React.ReactNode }> = ({
  children,
}) => {
  const defaultValue = useContext(AppContext);
  return (
    <AppContext.Provider value={{ ...defaultValue, login: loginMock }}>
      {children}
    </AppContext.Provider>
  );
};

const PendingProbe = () => {
  const location = useLocation();
  const state = location.state as {
    emailAddress?: string;
    confirmationEmailJustSent?: boolean;
    confirmationEmailDeliveryStatus?: MessageDeliveryStatus;
  } | null;
  return (
    <>
      <div>Pending email: {state?.emailAddress}</div>
      <div>Pending sent: {String(state?.confirmationEmailJustSent)}</div>
      <div>Pending delivery status: {String(state?.confirmationEmailDeliveryStatus)}</div>
      <div data-testid="pending-search">{location.search}</div>
    </>
  );
};

const LoginProbe = () => {
  const location = useLocation();
  return (
    <>
      <LoginPage />
      <div data-testid="login-path">{location.pathname}</div>
    </>
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

const renderLogin = (initialEntry = "/login") =>
  render(
    <MemoryRouter initialEntries={[initialEntry]}>
      <AppContextOverride>
        <Routes>
          <Route path="/login" element={<LoginProbe />} />
          <Route path={ClientRoutes.emailConfirmationPending.path} element={<PendingProbe />} />
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
    target: { value: "wrong-or-right-password" },
  });
  fireEvent.click(screen.getByRole("button", { name: /Login$/ }));
};

describe("LoginPage email confirmation routing", () => {
  beforeEach(() => {
    loginMock.mockReset();
    errorSpy = jest
      .spyOn(message, "error")
      .mockImplementation(() => undefined as any);
  });

  afterEach(() => {
    jest.restoreAllMocks();
  });

  it("routes EmailNotConfirmed login failures to the pending page with the email in state", async () => {
    loginMock.mockRejectedValue({
      message: "Please confirm your email address before logging in.",
      errorCode: ApiExceptionType.EmailNotConfirmed,
    });
    renderLogin();

    submitCredentials();

    expect(
      await screen.findByText("Pending email: user@example.com")
    ).toBeInTheDocument();
    expect(screen.getByText("Pending sent: false")).toBeInTheDocument();
    expect(screen.getByText("Pending delivery status: undefined")).toBeInTheDocument();
    expect(errorSpy).not.toHaveBeenCalled();
  });

  it("carries the latest confirmation delivery status to the pending page", async () => {
    loginMock.mockRejectedValue({
      message: "Please confirm your email address before logging in.",
      errorCode: ApiExceptionType.EmailNotConfirmed,
      data: {
        confirmationEmailDeliveryStatus: MessageDeliveryStatus.PermanentFailed,
      },
    });
    renderLogin();

    submitCredentials();

    expect(
      await screen.findByText("Pending delivery status: PermanentFailed")
    ).toBeInTheDocument();
    expect(errorSpy).not.toHaveBeenCalled();
  });

  it("preserves redirectUrl when an unconfirmed login moves to the pending page", async () => {
    loginMock.mockRejectedValue({
      message: "Please confirm your email address before logging in.",
      errorCode: ApiExceptionType.EmailNotConfirmed,
    });
    renderLogin("/login?redirectUrl=%2Fcaves%2FABC");

    submitCredentials();

    await screen.findByText("Pending email: user@example.com");
    expect(screen.getByTestId("pending-search").textContent).toBe(
      "?redirectUrl=%2Fcaves%2FABC"
    );
  });

  it("passes and preserves invitationCode when an unconfirmed invitation login moves to pending", async () => {
    loginMock.mockRejectedValue({
      message: "Please confirm your email address before logging in.",
      errorCode: ApiExceptionType.EmailNotConfirmed,
    });
    renderLogin("/login?invitationCode=ABC123");

    submitCredentials();

    await screen.findByText("Pending email: user@example.com");
    expect(loginMock).toHaveBeenCalledWith(
      expect.objectContaining({
        emailAddress: "user@example.com",
        password: "wrong-or-right-password",
      }),
      "ABC123"
    );
    expect(screen.getByTestId("pending-search").textContent).toBe(
      "?invitationCode=ABC123"
    );
  });

  it("preserves the existing login query unchanged when both continuation parameters are present", async () => {
    loginMock.mockRejectedValue({
      message: "Please confirm your email address before logging in.",
      errorCode: ApiExceptionType.EmailNotConfirmed,
    });
    renderLogin(
      "/login?redirectUrl=%2Fcaves%2FABC&invitationCode=ABC123"
    );

    submitCredentials();

    await screen.findByText("Pending email: user@example.com");
    expect(screen.getByTestId("pending-search").textContent).toBe(
      "?redirectUrl=%2Fcaves%2FABC&invitationCode=ABC123"
    );
  });

  it("keeps invalid-password failures on login and shows the normal error", async () => {
    loginMock.mockRejectedValue({
      message: "Password is invalid",
      errorCode: ApiExceptionType.InvalidPassword,
    });
    renderLogin();

    submitCredentials();

    await waitFor(() =>
      expect(errorSpy).toHaveBeenCalledWith("Password is invalid")
    );
    expect(screen.getByTestId("login-path").textContent).toBe("/login");
  });
});

describe("LoginPage post-login redirect safety", () => {
  beforeEach(() => {
    loginMock.mockReset();
    loginMock.mockResolvedValue(undefined);
  });

  it.each([
    ["//evil.example/phish", "/"],
    ["https://evil.example/phish", "/"],
    ["javascript:alert(1)", "/"],
    ["/caves/ABC?tab=files#history", "/caves/ABC?tab=files#history"],
  ])(
    "navigates redirect %p only to the sanitized local destination",
    async (redirectUrl, expected) => {
      renderLogin(`/login?redirectUrl=${encodeURIComponent(redirectUrl)}`);

      submitCredentials();

      expect(await screen.findByTestId("destination")).toHaveTextContent(expected);
      expect(loginMock).toHaveBeenCalledTimes(1);
    }
  );
});
