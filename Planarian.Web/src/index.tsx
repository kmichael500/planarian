import React from "react";
import ReactDOM from "react-dom/client";
import "./index.css";
import App from "./App";
import reportWebVitals from "./reportWebVitals";
import axios, { AxiosRequestTransformer } from "axios";
import { AuthenticationService } from "./Modules/Authentication/Services/AuthenticationService";
import { isNullOrWhiteSpace } from "./Shared/Helpers/StringHelpers";
import { AppService } from "./Shared/Services/AppService";

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

export { HttpClient, baseUrl };
