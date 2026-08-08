import { configureHttpClient, getApiBaseUrl, HttpClient } from "./HttpClient";
import { RequestRuntimeState } from "./RequestRuntimeState";

describe("HTTP infrastructure", () => {
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
});
