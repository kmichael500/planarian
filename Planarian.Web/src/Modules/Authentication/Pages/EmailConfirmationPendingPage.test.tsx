import { act, fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { message } from "antd";
import React from "react";
import {
  MemoryRouter,
  Route,
  Routes,
  useLocation,
} from "react-router-dom";
import { ApiExceptionType } from "../../../Shared/Models/ApiErrorResponse";
import { UserService } from "../../User/UserService";
import { EmailConfirmationPendingPage } from "./EmailConfirmationPendingPage";

let resendSpy: jest.SpyInstance;
let successSpy: jest.SpyInstance;
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

const LocationStateProbe = () => {
  const location = useLocation();
  return (
    <>
      <div data-testid="location-state">
        {location.state == null ? "null" : JSON.stringify(location.state)}
      </div>
      <div data-testid="location-search">{location.search}</div>
    </>
  );
};

const LoginDestinationProbe = () => {
  const location = useLocation();
  return <div>Login destination: {`${location.pathname}${location.search}`}</div>;
};

const renderPage = (
  state?: {
    emailAddress?: string;
    confirmationEmailJustSent?: boolean;
    confirmationEmailDeliveryFailed?: boolean;
  },
  search = ""
) =>
  render(
    <MemoryRouter
      initialEntries={[
        {
          pathname: "/confirm-email/pending",
          search,
          state,
        },
      ]}
    >
      <Routes>
        <Route
          path="/confirm-email/pending"
          element={
            <>
              <EmailConfirmationPendingPage />
              <LocationStateProbe />
            </>
          }
        />
        <Route path="/login" element={<LoginDestinationProbe />} />
      </Routes>
    </MemoryRouter>
  );

describe("EmailConfirmationPendingPage", () => {
  beforeEach(() => {
    resendSpy = jest
      .spyOn(UserService, "ResendEmailConfirmation")
      .mockResolvedValue();
    successSpy = jest
      .spyOn(message, "success")
      .mockImplementation(() => undefined as any);
    errorSpy = jest
      .spyOn(message, "error")
      .mockImplementation(() => undefined as any);
  });

  afterEach(() => {
    jest.restoreAllMocks();
  });

  it("shows the transition email while consuming it from router history state", async () => {
    renderPage({
      emailAddress: "  user@example.com  ",
      confirmationEmailJustSent: true,
    });

    expect(screen.getByText("user@example.com")).toBeInTheDocument();
    expect(screen.getByText(/We sent a confirmation link to/)).toBeInTheDocument();
    expect(screen.queryByLabelText("Email Address")).not.toBeInTheDocument();
    await waitFor(() =>
      expect(screen.getByTestId("location-state").textContent).toBe("null")
    );
  });

  it("shows generic copy and an email input for direct navigation without router state", () => {
    renderPage();

    expect(
      screen.getByText(/Confirm your email address before signing in/)
    ).toBeInTheDocument();
    expect(screen.getByLabelText("Email Address")).toBeInTheDocument();
  });

  it("does not claim a new email was sent when reached from an unconfirmed login", () => {
    renderPage({
      emailAddress: "user@example.com",
      confirmationEmailJustSent: false,
    });

    expect(
      screen.getByText(/still needs to be confirmed before you can sign in/)
    ).toBeInTheDocument();
    expect(screen.queryByText(/We sent a confirmation link to/)).not.toBeInTheDocument();
  });

  it("shows a delivery warning when a correct-password login reports a permanent failure", () => {
    renderPage({
      emailAddress: "user@example.com",
      confirmationEmailJustSent: false,
      confirmationEmailDeliveryFailed: true,
    });

    expect(
      screen.getByText("We couldn't deliver your confirmation email.")
    ).toBeInTheDocument();
    const alert = screen.getByRole("alert");
    expect(
      within(alert).getByText(/email provider reported that a confirmation message to/)
    ).toBeInTheDocument();
    expect(within(alert).getByText("user@example.com")).toBeInTheDocument();
  });

  it("clears an old delivery warning after a resend request succeeds", async () => {
    renderPage({
      emailAddress: "user@example.com",
      confirmationEmailJustSent: false,
      confirmationEmailDeliveryFailed: true,
    });

    expect(screen.getByRole("alert")).toBeInTheDocument();
    fireEvent.click(
      screen.getByRole("button", { name: /Resend confirmation email$/ })
    );

    await waitFor(() => expect(resendSpy).toHaveBeenCalledTimes(1));
    await waitFor(() => expect(screen.queryByRole("alert")).not.toBeInTheDocument());
  });

  it("keeps the login continuation query while consuming transient router state", async () => {
    renderPage(
      {
        emailAddress: "user@example.com",
        confirmationEmailJustSent: false,
      },
      "?redirectUrl=%2Fcaves%2FABC"
    );

    await waitFor(() =>
      expect(screen.getByTestId("location-state").textContent).toBe("null")
    );
    expect(screen.getByTestId("location-search").textContent).toBe(
      "?redirectUrl=%2Fcaves%2FABC"
    );
  });

  it("preserves redirectUrl when returning to Login", async () => {
    renderPage(undefined, "?redirectUrl=%2Fcaves%2FABC");

    fireEvent.click(screen.getByRole("button", { name: /Back to Login$/ }));

    expect(
      await screen.findByText("Login destination: /login?redirectUrl=%2Fcaves%2FABC")
    ).toBeInTheDocument();
  });

  it("preserves invitationCode when returning to Login", async () => {
    renderPage(undefined, "?invitationCode=ABC123");

    fireEvent.click(screen.getByRole("button", { name: /Back to Login$/ }));

    expect(
      await screen.findByText("Login destination: /login?invitationCode=ABC123")
    ).toBeInTheDocument();
  });

  it("does not resend when fallback form validation fails", async () => {
    renderPage();

    fireEvent.change(screen.getByLabelText("Email Address"), {
      target: { value: "not-an-email" },
    });
    fireEvent.click(
      screen.getByRole("button", { name: /Resend confirmation email$/ })
    );

    await waitFor(() =>
      expect(
        screen.getByText("Please enter a valid email address.")
      ).toBeInTheDocument()
    );
    expect(resendSpy).not.toHaveBeenCalled();
  });

  it("resends a valid fallback email and shows generic success feedback", async () => {
    renderPage();

    fireEvent.change(screen.getByLabelText("Email Address"), {
      target: { value: "user@example.com" },
    });
    fireEvent.click(
      screen.getByRole("button", { name: /Resend confirmation email$/ })
    );

    await waitFor(() =>
      expect(resendSpy).toHaveBeenCalledWith("user@example.com")
    );
    expect(successSpy).toHaveBeenCalledWith(
      "If an unconfirmed account exists for that email address, a confirmation email has been sent."
    );
  });

  it("resends the trimmed transition email captured from router state", async () => {
    renderPage({
      emailAddress: "  user@example.com  ",
      confirmationEmailJustSent: false,
    });

    const button = screen.getByRole("button", {
      name: /Resend confirmation email$/,
    });
    fireEvent.click(button);

    await waitFor(() =>
      expect(resendSpy).toHaveBeenCalledWith("user@example.com")
    );
    await waitFor(() => expect(button).not.toBeDisabled());
  });

  it("does not show a page-level error for TooManyRequests", async () => {
    resendSpy.mockRejectedValue({
      message: "Too many attempts.",
      errorCode: ApiExceptionType.TooManyRequests,
    });
    renderPage({ emailAddress: "user@example.com" });

    fireEvent.click(
      screen.getByRole("button", { name: /Resend confirmation email$/ })
    );

    await waitFor(() => expect(resendSpy).toHaveBeenCalledTimes(1));
    await waitFor(() =>
      expect(
        screen.getByRole("button", { name: /Resend confirmation email$/ })
      ).not.toBeDisabled()
    );
    expect(errorSpy).not.toHaveBeenCalled();
  });

  it("shows a single page-level error for non-rate-limit failures", async () => {
    resendSpy.mockRejectedValue({
      message: "The email failed to send",
      errorCode: ApiExceptionType.EmailFailedToSend,
    });
    renderPage({ emailAddress: "user@example.com" });

    fireEvent.click(
      screen.getByRole("button", { name: /Resend confirmation email$/ })
    );

    await waitFor(() =>
      expect(errorSpy).toHaveBeenCalledWith("The email failed to send")
    );
    expect(errorSpy).toHaveBeenCalledTimes(1);
  });

  it("prevents a second resend while the first request is in flight", async () => {
    let resolveRequest!: () => void;
    resendSpy.mockReturnValue(
      new Promise<void>((resolve) => {
        resolveRequest = resolve;
      })
    );
    renderPage({ emailAddress: "user@example.com" });

    const button = screen.getByRole("button", {
      name: /Resend confirmation email$/,
    });

    act(() => {
      button.dispatchEvent(new MouseEvent("click", { bubbles: true }));
      button.dispatchEvent(new MouseEvent("click", { bubbles: true }));
    });

    expect(resendSpy).toHaveBeenCalledTimes(1);
    expect(button).toBeDisabled();

    await act(async () => {
      resolveRequest();
    });
    expect(button).not.toBeDisabled();
  });
});
