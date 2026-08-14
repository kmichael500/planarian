import { message } from "antd";
import { ApiExceptionType } from "../Models/ApiErrorResponse";
import { configureHttpClient, getApiBaseUrl, HttpClient } from "./HttpClient";
import { RequestRuntimeState } from "./RequestRuntimeState";

describe("HTTP infrastructure", () => {
  const originalBaseUrl = getApiBaseUrl();
  let errorSpy: jest.SpyInstance;

  beforeEach(() => {
    RequestRuntimeState.setCurrentAccountId(null);
    RequestRuntimeState.setAntiforgeryRequestToken(null);
    errorSpy = jest
      .spyOn(message, "error")
      .mockImplementation(() => undefined as any);
  });

  afterEach(() => {
    RequestRuntimeState.setCurrentAccountId(null);
    RequestRuntimeState.setAntiforgeryRequestToken(null);
    configureHttpClient(originalBaseUrl as string);
    jest.restoreAllMocks();
  });

  it("can be configured without importing the application entrypoint", () => {
    configureHttpClient("https://api.example.test");

    expect(getApiBaseUrl()).toBe("https://api.example.test");
    expect(HttpClient.defaults.baseURL).toBe("https://api.example.test");
    expect(HttpClient.defaults.withCredentials).toBe(true);
  });

  it("keeps request state in the low-level runtime module", () => {
    RequestRuntimeState.setCurrentAccountId("account-1");
    RequestRuntimeState.setAntiforgeryRequestToken("token-1");

    expect(RequestRuntimeState.getCurrentAccountId()).toBe("account-1");
    expect(RequestRuntimeState.getAntiforgeryRequestToken()).toBe("token-1");
  });

  it("shows TooManyRequests errors through the global response interceptor", async () => {
    const apiError = {
      message: "Too many attempts.",
      errorCode: ApiExceptionType.TooManyRequests,
      data: null,
    };

    await expect(
      HttpClient.get("/rate-limited", {
        adapter: async () =>
          Promise.reject({
            response: {
              status: 429,
              data: apiError,
              headers: {},
            },
          }),
      })
    ).rejects.toMatchObject({
      message: "Too many attempts.",
      errorCode: ApiExceptionType.TooManyRequests,
      statusCode: 429,
    });

    expect(errorSpy).toHaveBeenCalledTimes(1);
    expect(errorSpy).toHaveBeenCalledWith("Too many attempts.");
  });
});
