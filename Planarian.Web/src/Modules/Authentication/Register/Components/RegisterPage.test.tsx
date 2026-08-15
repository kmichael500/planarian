import {
  act,
  fireEvent,
  render,
  screen,
  waitFor,
} from "@testing-library/react";
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
import { MessageDeliveryStatus } from "../../../../Shared/Models/MessageDeliveryStatus";
import { ClientRoutes } from "../../../../Configuration/Routing/ClientRoutes.generated";
import { AcceptInvitationVm } from "../../../User/Models/AcceptInvitationVm";

let registerSpy: jest.SpyInstance;
let getInvitationSpy: jest.SpyInstance;
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

const PendingProbe = () => {
  const location = useLocation();
  const navigate = useNavigate();
  const state = location.state as {
    emailAddress?: string;
    confirmationEmailJustSent?: boolean;
    confirmationEmailDeliveryStatus?: MessageDeliveryStatus;
  } | null;
  return (
    <>
      <div>Pending email: {state?.emailAddress}</div>
      <div>Pending sent: {String(state?.confirmationEmailJustSent)}</div>
      <div>
        Pending delivery status:{" "}
        {String(state?.confirmationEmailDeliveryStatus)}
      </div>
      <div data-testid="pending-path">{location.pathname}</div>
      <div data-testid="pending-search">{location.search}</div>
      <button onClick={() => navigate(-1)}>Back history</button>
    </>
  );
};

const RegisterRouteControls = () => {
  const navigate = useNavigate();
  return (
    <button
      type="button"
      onClick={() => navigate("/register?invitationCode=SECOND")}
    >
      Load second invitation
    </button>
  );
};

const renderRegister = (initialEntry = "/register") =>
  render(
    <MemoryRouter initialEntries={[initialEntry]}>
      <Routes>
        <Route path="/register" element={<RegisterPage />} />
        <Route
          path={ClientRoutes.emailConfirmationPending.path}
          element={<PendingProbe />}
        />
      </Routes>
    </MemoryRouter>
  );

const renderRegisterWithRouteControls = (initialEntry: string) =>
  render(
    <MemoryRouter initialEntries={[initialEntry]}>
      <Routes>
        <Route
          path="/register"
          element={
            <>
              <RegisterPage />
              <RegisterRouteControls />
            </>
          }
        />
        <Route
          path={ClientRoutes.emailConfirmationPending.path}
          element={<PendingProbe />}
        />
      </Routes>
    </MemoryRouter>
  );

const renderRegisterStrict = (initialEntry = "/register") =>
  render(
    <React.StrictMode>
      <MemoryRouter initialEntries={[initialEntry]}>
        <Routes>
          <Route path="/register" element={<RegisterPage />} />
          <Route
            path={ClientRoutes.emailConfirmationPending.path}
            element={<PendingProbe />}
          />
        </Routes>
      </MemoryRouter>
    </React.StrictMode>
  );

const deferred = <T,>() => {
  let resolve!: (value: T) => void;
  let reject!: (reason?: unknown) => void;
  const promise = new Promise<T>((res, rej) => {
    resolve = res;
    reject = rej;
  });
  return { promise, resolve, reject };
};

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

describe("RegisterPage", () => {
  beforeEach(() => {
    registerSpy = jest
      .spyOn(RegisterService, "RegisterUser")
      .mockResolvedValue({
        confirmationEmailDeliveryStatus: MessageDeliveryStatus.Submitted,
      });
    getInvitationSpy = jest.spyOn(UserService, "GetInvitation");
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
    expect(
      screen.getByText("Pending delivery status: Submitted")
    ).toBeInTheDocument();
    expect(screen.getByTestId("pending-search")).toBeEmptyDOMElement();
    expect(successSpy).not.toHaveBeenCalled();

    fireEvent.click(screen.getByRole("button", { name: "Back history" }));
    await waitFor(() =>
      expect(screen.getByTestId("pending-path").textContent).toBe(
        ClientRoutes.emailConfirmationPending.path
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
    expect(
      screen.getByText("Pending delivery status: Submitted")
    ).toBeInTheDocument();
    expect(screen.getByTestId("pending-search")).toBeEmptyDOMElement();
  });

  it("routes a created account to pending with SendFailed when confirmation submission fails", async () => {
    registerSpy.mockResolvedValue({
      confirmationEmailDeliveryStatus: MessageDeliveryStatus.SendFailed,
    });
    renderRegister();
    fillRegistration();

    fireEvent.click(screen.getByRole("button", { name: /Submit$/ }));

    expect(
      await screen.findByText("Pending email: user@example.com")
    ).toBeInTheDocument();
    expect(screen.getByText("Pending sent: false")).toBeInTheDocument();
    expect(
      screen.getByText("Pending delivery status: SendFailed")
    ).toBeInTheDocument();
  });

  it("ignores a stale StrictMode invitation failure after the current request succeeds", async () => {
    const staleRequest = deferred<AcceptInvitationVm>();
    const currentRequest = deferred<AcceptInvitationVm>();
    getInvitationSpy
      .mockImplementationOnce(() => staleRequest.promise)
      .mockImplementationOnce(() => currentRequest.promise);

    renderRegisterStrict("/register?invitationCode=ABC123");
    await waitFor(() => expect(getInvitationSpy).toHaveBeenCalledTimes(2));

    await act(async () => {
      currentRequest.resolve({
        invitationCode: "ABC123",
        firstName: "Test",
        lastName: "User",
        email: "user@example.com",
        regions: [],
        accountName: "Test Account",
        accountId: "account123",
      });
    });
    expect(
      await screen.findByText(/You've been invited to access Test Account data/)
    ).toBeInTheDocument();

    await act(async () => {
      staleRequest.reject({ message: "Invitation not found" });
      await Promise.resolve();
    });

    expect(errorSpy).not.toHaveBeenCalled();
  });

  it("clears a loaded invitation when a replacement invitation lookup fails", async () => {
    getInvitationSpy
      .mockResolvedValueOnce({
        invitationCode: "ABC123",
        firstName: "Test",
        lastName: "User",
        email: "user@example.com",
        regions: [],
        accountName: "Test Account",
        accountId: "account123",
      })
      .mockRejectedValueOnce({ message: "Invitation not found" });

    renderRegisterWithRouteControls("/register?invitationCode=ABC123");
    expect(
      await screen.findByText(/You've been invited to access Test Account data/)
    ).toBeInTheDocument();

    fireEvent.click(
      screen.getByRole("button", { name: "Load second invitation" })
    );

    await waitFor(() => expect(getInvitationSpy).toHaveBeenCalledTimes(2));
    await waitFor(() =>
      expect(errorSpy).toHaveBeenCalledWith(
        "Invalid or expired invitation code."
      )
    );
    expect(
      screen.queryByText(/You've been invited to access Test Account data/)
    ).not.toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: /Accept Invitation$/ })
    ).toBeDisabled();
    expect(screen.getByLabelText("First Name")).toHaveValue("");
    expect(screen.getByLabelText("Last Name")).toHaveValue("");
    expect(screen.getByLabelText("Email Address")).toHaveValue("");
  });

  it("submits an invitation registration only once while the request is in flight", async () => {
    const registrationRequest = deferred<{
      confirmationEmailDeliveryStatus: MessageDeliveryStatus;
    }>();
    getInvitationSpy.mockResolvedValue({
      invitationCode: "ABC123",
      firstName: "Test",
      lastName: "User",
      email: "user@example.com",
      regions: [],
      accountName: "Test Account",
      accountId: "account123",
    });
    registerSpy.mockReturnValue(registrationRequest.promise);

    renderRegister("/register?invitationCode=ABC123");
    await screen.findByText(/You've been invited to access Test Account data/);
    fillRegistration();

    const button = screen.getByRole("button", { name: /Accept Invitation$/ });
    act(() => {
      button.dispatchEvent(new MouseEvent("click", { bubbles: true }));
      button.dispatchEvent(new MouseEvent("click", { bubbles: true }));
    });

    await waitFor(() => expect(registerSpy).toHaveBeenCalledTimes(1));

    await act(async () => {
      registrationRequest.resolve({
        confirmationEmailDeliveryStatus: MessageDeliveryStatus.Submitted,
      });
    });
    expect(
      await screen.findByText("Pending email: user@example.com")
    ).toBeInTheDocument();
  });
});
