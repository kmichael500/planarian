import axios, { AxiosHeaders } from "axios";
import { message } from "antd";
import { ApiErrorResponse, ApiExceptionType } from "../Models/ApiErrorResponse";
import { RequestRuntimeState } from "./RequestRuntimeState";

let baseUrl: string | undefined;
export type AuthenticationFailureReason = "unauthorized" | "invalid-account";
let unauthorizedHandler: ((reason: AuthenticationFailureReason) => void) | null = null;

export const HttpClient = axios.create({ withCredentials: true });

export function getApiBaseUrl(): string | undefined {
  return baseUrl;
}

export function configureHttpClient(apiBaseUrl: string): void {
  baseUrl = apiBaseUrl;
  HttpClient.defaults.baseURL = apiBaseUrl;
}

export function registerUnauthorizedHandler(
  handler: (reason: AuthenticationFailureReason) => void
): () => void {
  unauthorizedHandler = handler;
  return () => {
    if (unauthorizedHandler === handler) {
      unauthorizedHandler = null;
    }
  };
}

HttpClient.interceptors.request.use((config) => {
  const headers =
    config.headers instanceof AxiosHeaders
      ? config.headers
      : AxiosHeaders.from(config.headers);
  const accountId = RequestRuntimeState.getCurrentAccountId();

  if (!headers.has("x-account")) {
    if (accountId) {
      headers.set("x-account", accountId);
    } else {
      headers.delete("x-account");
    }
  }

  const method = config.method?.toUpperCase();
  if (method && !["GET", "HEAD", "OPTIONS", "TRACE"].includes(method)) {
    const antiforgeryRequestToken = RequestRuntimeState.getAntiforgeryRequestToken();
    if (antiforgeryRequestToken) {
      headers.set("X-XSRF-TOKEN", antiforgeryRequestToken);
    }
  }

  config.headers = headers;
  return config;
});

HttpClient.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response) {
      const statusCode = error.response.status;
      if (error.response.data) {
        const apiError = error.response.data as ApiErrorResponse;
        const retryAfterHeader = error.response.headers?.["retry-after"];
        apiError.statusCode = statusCode;
        apiError.requestId =
          apiError.requestId ??
          error.response.headers?.["x-request-id"] ??
          error.response.headers?.["request-id"];
        if (retryAfterHeader) {
          const retryAfterSeconds = parseInt(retryAfterHeader, 10);
          if (!Number.isNaN(retryAfterSeconds)) {
            apiError.retryAfterSeconds = retryAfterSeconds;
          }
        }
        if (apiError.errorCode === ApiExceptionType.TooManyRequests) {
          message.error(apiError.message);
        }
        if (statusCode === 401 || apiError.errorCode === ApiExceptionType.Unauthorized) {
          unauthorizedHandler?.("unauthorized");
        } else if (apiError.errorCode === ApiExceptionType.InvalidAccountSelection) {
          // Ignore stale errors from a previous account selection.
          const requestAccountId = error.config?.headers
            ? AxiosHeaders.from(error.config.headers).get("x-account")
            : null;
          if (!requestAccountId ||
              requestAccountId === RequestRuntimeState.getCurrentAccountId()) {
            unauthorizedHandler?.("invalid-account");
          }
        }
        return Promise.reject(apiError);
      }
      if (statusCode === 401 || statusCode === 403) {
        const apiError: ApiErrorResponse = {
          message: statusCode === 403 ? "Forbidden" : "Unauthorized",
          errorCode: statusCode === 403 ? ApiExceptionType.Forbidden : ApiExceptionType.Unauthorized,
          data: null,
          statusCode,
        };
        if (statusCode === 401) {
          unauthorizedHandler?.("unauthorized");
        }
        return Promise.reject(apiError);
      }
    }
    return Promise.reject(error);
  }
);
