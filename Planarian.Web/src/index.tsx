import React from "react";
import ReactDOM from "react-dom/client";
import { message } from "antd";
import "./index.css";
import App from "./App";
import reportWebVitals from "./reportWebVitals";
import axios, { AxiosHeaders } from "axios";
import { AuthenticationService } from "./Modules/Authentication/Services/AuthenticationService";
import {
  ApiErrorResponse,
  ApiExceptionType,
} from "./Shared/Models/ApiErrorResponse";
import { AppService } from "./Shared/Services/AppService";
import { parseApiOriginMappings, resolveApiBaseUrl } from "./Shared/Helpers/ApiBaseUrl";

import utc from "dayjs/plugin/utc";
import customParseFormat from "dayjs/plugin/customParseFormat";

import { registerLicense } from "@syncfusion/ej2-base";
import dayjs from "dayjs";

dayjs.extend(utc);
dayjs.extend(customParseFormat);

// Set CSS variable for dynamic viewport height
if (typeof window !== "undefined") {
  const setVh = () => {
    const vh = window.innerHeight * 0.01;
    document.documentElement.style.setProperty("--vh", `${vh}px`);
  };
  setVh();
  window.addEventListener("resize", setVh);
}

if (typeof window !== "undefined") {
  const umamiWebsiteId = process.env.REACT_APP_UMAMI_WEBSITE_ID?.trim();
  if (umamiWebsiteId) {
    const script = document.createElement("script");
    script.src = "https://cloud.umami.is/script.js";
    script.defer = true;
    script.setAttribute(
      "data-website-id",
      umamiWebsiteId
    );
    document.body.appendChild(script);
  }
}

registerLicense(
  "Ngo9BigBOggjHTQxAR8/V1NNaF5cXmBCf1FpRmJGdld5fUVHYVZUTXxaS00DNHVRdkdmWXxdcXVWR2VYU01wXkpWYUA="
);

const root = ReactDOM.createRoot(
  document.getElementById("root") as HTMLElement
);
root.render(
  <React.StrictMode>
    <App />
  </React.StrictMode>
);

// If you want to start measuring performance in your app, pass a function
// to log results (for example: reportWebVitals(console.log))
// or send to an analytics endpoint. Learn more: https://bit.ly/CRA-vitals
reportWebVitals();

const baseUrl = resolveApiBaseUrl({
  hostname: window.location.hostname,
  nodeEnv: process.env.NODE_ENV,
  mappings: parseApiOriginMappings(process.env.REACT_APP_API_ORIGIN_MAPPINGS),
});

const HttpClient = axios.create({
  baseURL: baseUrl,
  withCredentials: true,
});

HttpClient.interceptors.request.use((config) => {
  const headers =
    config.headers instanceof AxiosHeaders
      ? config.headers
      : AxiosHeaders.from(config.headers);
  const accountId = AuthenticationService.GetAccountId();

  if (!headers.has("x-account")) {
    if (accountId) {
      headers.set("x-account", accountId);
    } else {
      headers.delete("x-account");
    }
  }

  const method = config.method?.toUpperCase();
  const requiresAntiforgery =
    method != null &&
    !["GET", "HEAD", "OPTIONS", "TRACE"].includes(method);

  if (requiresAntiforgery) {
    const antiforgeryRequestToken = AppService.GetAntiforgeryRequestToken();
    if (antiforgeryRequestToken) {
      headers.set("X-XSRF-TOKEN", antiforgeryRequestToken);
    }
  }

  config.headers = headers;
  return config;
});

// Override default axios error handler to throw custom error data
HttpClient.interceptors.response.use(
  function (response) {
    return response;
  },
  function (error) {
    if (error.response) {
      const statusCode = error.response.status;

      if (error.response.data) {
        const apiError = error.response.data as ApiErrorResponse;
        const retryAfterHeader = error.response.headers?.["retry-after"];
        apiError.statusCode = error.response.status;
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

        if (apiError?.errorCode === ApiExceptionType.TooManyRequests) {
          message.error(apiError.message);
        }

        if (
          statusCode === 401 ||
          apiError?.errorCode === ApiExceptionType.Unauthorized
        ) {
          AuthenticationService.HandleUnauthorized();
        }

        return Promise.reject(error.response.data);
      }

      if (statusCode === 401 || statusCode === 403) {
        const apiError: ApiErrorResponse = {
          message: statusCode === 403 ? "Forbidden" : "Unauthorized",
          errorCode:
            statusCode === 403
              ? ApiExceptionType.Forbidden
              : ApiExceptionType.Unauthorized,
          data: null,
          statusCode: error.response.status,
        };

        if (statusCode === 401) {
          AuthenticationService.HandleUnauthorized();
        }

        return Promise.reject(apiError);
      }
    }
    return Promise.reject(error);
  }
);

export { HttpClient, baseUrl };
