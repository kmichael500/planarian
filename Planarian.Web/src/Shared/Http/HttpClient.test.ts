import { message } from "antd";
import { AxiosHeaders } from "axios";
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

  it("configures the shared base URL and credential mode", () => {
    configureHttpClient("https://api.example.test");

    expect(getApiBaseUrl()).toBe("https://api.example.test");
    expect(HttpClient.defaults.baseURL).toBe("https://api.example.test");
    expect(HttpClient.defaults.withCredentials).toBe(true);
  });

  it("adds account and antiforgery state to unsafe requests", async () => {
    RequestRuntimeState.setCurrentAccountId("account-1");
    RequestRuntimeState.setAntiforgeryRequestToken("token-1");
    let requestHeaders: AxiosHeaders | undefined;

    await HttpClient.post(
      "/mutation",
      {},
      {
        adapter: async (config) => {
          requestHeaders = AxiosHeaders.from(config.headers);
          return {
            data: {},
            status: 200,
            statusText: "OK",
            headers: {},
            config,
          };
        },
      }
    );

    expect(requestHeaders?.get("x-account")).toBe("account-1");
    expect(requestHeaders?.get("X-XSRF-TOKEN")).toBe("token-1");
  });

  it("preserves an explicit account header instead of replacing it with runtime state", async () => {
    RequestRuntimeState.setCurrentAccountId("runtime-account");
    let requestHeaders: AxiosHeaders | undefined;

    await HttpClient.get("/account-specific", {
      headers: { "x-account": "requested-account" },
      adapter: async (config) => {
        requestHeaders = AxiosHeaders.from(config.headers);
        return { data: {}, status: 200, statusText: "OK", headers: {}, config };
      },
    });

    expect(requestHeaders?.get("x-account")).toBe("requested-account");
  });

  it("does not attach an antiforgery token to safe requests", async () => {
    RequestRuntimeState.setCurrentAccountId("account-1");
    RequestRuntimeState.setAntiforgeryRequestToken("token-1");
    let requestHeaders: AxiosHeaders | undefined;

    await HttpClient.get("/read", {
      adapter: async (config) => {
        requestHeaders = AxiosHeaders.from(config.headers);
        return { data: {}, status: 200, statusText: "OK", headers: {}, config };
      },
    });

    expect(requestHeaders?.get("x-account")).toBe("account-1");
    expect(requestHeaders?.has("X-XSRF-TOKEN")).toBe(false);
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
