import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { message } from "antd";
import React from "react";
import {
  MemoryRouter,
  Route,
  Routes,
  useLocation,
  useNavigate,
} from "react-router-dom";
import { RegisterPage } from "./RegisterPage";
import { RegisterService } from "../Services/RegisterService";
import { UserService } from "../../../User/UserService";

let registerSpy: jest.SpyInstance;
let getInvitationSpy: jest.SpyInstance;
let successSpy: jest.SpyInstance;

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

const PendingProbe = () => {
  const location = useLocation();
  const navigate = useNavigate();
  const state = location.state as {
    emailAddress?: string;
    confirmationEmailJustSent?: boolean;
  } | null;
  return (
    <>
      <div>Pending email: {state?.emailAddress}</div>
      <div>Pending sent: {String(state?.confirmationEmailJustSent)}</div>
      <div data-testid="pending-path">{location.pathname}</div>
      <div data-testid="pending-search">{location.search}</div>
      <button onClick={() => navigate(-1)}>Back history</button>
    </>
  );
};

const renderRegister = (initialEntry = "/register") =>
  render(
    <MemoryRouter initialEntries={[initialEntry]}>
      <Routes>
        <Route path="/register" element={<RegisterPage />} />
        <Route path="/confirm-email/pending" element={<PendingProbe />} />
      </Routes>
    </MemoryRouter>
  );

const fillRegistration = () => {
  fireEvent.change(screen.getByLabelText("First Name"), {
    target: { value: "Test" },
  });
  fireEvent.change(screen.getByLabelText("Last Name"), {
    target: { value: "User" },
  });
  fireEvent.change(screen.getByLabelText("Email Address"), {
    target: { value: "user@example.com" },
  });
  fireEvent.change(screen.getByLabelText("Phone Number"), {
    target: { value: "+1 (615) 555-1234" },
  });
  fireEvent.change(screen.getByLabelText("Password"), {
    target: { value: "a-valid-long-password" },
  });
  fireEvent.change(screen.getByLabelText("Confirm Password"), {
    target: { value: "a-valid-long-password" },
  });
};

describe("RegisterPage confirmation-pending navigation", () => {
  beforeEach(() => {
    registerSpy = jest
      .spyOn(RegisterService, "RegisterUser")
      .mockResolvedValue();
    getInvitationSpy = jest.spyOn(UserService, "GetInvitation");
    successSpy = jest
      .spyOn(message, "success")
      .mockImplementation(() => undefined as any);
    jest
      .spyOn(message, "error")
      .mockImplementation(() => undefined as any);
  });

  afterEach(() => {
    jest.restoreAllMocks();
  });

  it("replace-navigates successful registration with the submitted email in route state", async () => {
    renderRegister();
    fillRegistration();

    fireEvent.click(screen.getByRole("button", { name: /Submit$/ }));

    expect(
      await screen.findByText("Pending email: user@example.com")
    ).toBeInTheDocument();
    expect(registerSpy).toHaveBeenCalledTimes(1);
    expect(registerSpy).toHaveBeenCalledWith(
      expect.objectContaining({ emailAddress: "user@example.com" })
    );
    expect(screen.getByText("Pending sent: true")).toBeInTheDocument();
    expect(screen.getByTestId("pending-search")).toBeEmptyDOMElement();
    expect(successSpy).not.toHaveBeenCalled();

    fireEvent.click(screen.getByRole("button", { name: "Back history" }));
    await waitFor(() =>
      expect(screen.getByTestId("pending-path").textContent).toBe(
        "/confirm-email/pending"
      )
    );
  });

  it("passes the registration invitationCode without carrying it onto the pending URL", async () => {
    getInvitationSpy.mockResolvedValue({
      invitationCode: "ABC123",
      firstName: "Test",
      lastName: "User",
      email: "user@example.com",
      regions: [],
      accountName: "Test Account",
      accountId: "account123",
    });
    renderRegister("/register?invitationCode=ABC123");

    expect(
      await screen.findByText(/You've been invited to access Test Account data/)
    ).toBeInTheDocument();
    expect(getInvitationSpy).toHaveBeenCalledTimes(1);
    expect(getInvitationSpy).toHaveBeenCalledWith("ABC123");

    fillRegistration();
    fireEvent.click(screen.getByRole("button", { name: /Accept Invitation$/ }));

    expect(
      await screen.findByText("Pending email: user@example.com")
    ).toBeInTheDocument();
    expect(registerSpy).toHaveBeenCalledTimes(1);
    expect(registerSpy).toHaveBeenCalledWith(
      expect.objectContaining({
        emailAddress: "user@example.com",
        invitationCode: "ABC123",
      })
    );
    expect(screen.getByText("Pending sent: true")).toBeInTheDocument();
    expect(screen.getByTestId("pending-search")).toBeEmptyDOMElement();
  });
});
