import React from "react";
import ReactDOM from "react-dom/client";
import "./index.css";
import App from "./App";
import reportWebVitals from "./reportWebVitals";
import axios, { AxiosRequestTransformer } from "axios";
import { AuthenticationService } from "./Modules/Authentication/Services/AuthenticationService";
import { isNullOrWhiteSpace } from "./Shared/Helpers/StringHelpers";
import {
  ApiErrorResponse,
  ApiExceptionType,
} from "./Shared/Models/ApiErrorResponse";

import { registerLicense } from "@syncfusion/ej2-base";

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

let baseUrl: string | undefined;
if (!isNullOrWhiteSpace(process.env.REACT_APP_SERVER_URL)) {
  baseUrl = process.env.REACT_APP_SERVER_URL;
} else if (process.env.NODE_ENV === "development") {
  baseUrl = "https://localhost:7111";
} else {
  baseUrl = "https://wa-planarian.azurewebsites.net";
}

// using tranform request instead of interceptors because
// it's not clearing  the headers when the user logs out and we call delete on the token
const setAuthHeaders: AxiosRequestTransformer = (data: any, headers) => {
  const token = localStorage.getItem("token");
  if (token) {
    headers["Authorization"] = `Bearer ${token}`;
    const accountId = AuthenticationService.GetAccountId();
    if (accountId) {
      headers["x-account"] = accountId;
    }
  } else {
    delete headers["Authorization"];
    delete headers["x-account"];
  }

  return data;
};

if (!Array.isArray(axios.defaults.transformRequest)) {
  axios.defaults.transformRequest = [];
}

axios.defaults.transformRequest = [setAuthHeaders].concat(
  axios.defaults.transformRequest
);

const HttpClient = axios.create({
  baseURL: baseUrl,
});

// Override default axios error handler to throw custom error data
HttpClient.interceptors.response.use(
  function (response) {
    return response;
  },
  function (error) {
    if (error.response) {
      if (error.response.data) {
        return Promise.reject(error.response.data);
      }

      if (error.response.status === 401 || error.response.status === 403) {
        // Create a custom unauthorized error object
        const unauthorizedError: ApiErrorResponse = {
          message: "Unauthorized",
          errorCode: ApiExceptionType.Unauthorized,
          data: null,
        };
        return Promise.reject(unauthorizedError);
      }
    }
    return Promise.reject(error);
  }
);

export { HttpClient, baseUrl };
