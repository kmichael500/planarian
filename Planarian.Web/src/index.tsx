import React from "react";
import ReactDOM from "react-dom/client";
import "./index.css";
import App from "./App";
import reportWebVitals from "./reportWebVitals";
import axios from "axios";
import { message } from "antd";
import { AuthenticationService } from "./Modules/Authentication/Services/authentication.service";
import { isNullOrWhiteSpace } from "./Shared/Helpers/StringHelpers";

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
} else {
  baseUrl = "https://localhost:7111";
}

let headers = {};
if (AuthenticationService.IsAuthenticated()) {
  headers = {
    Authorization: `Bearer ${AuthenticationService.GetToken()}`,
  };
}

const HttpClient = axios.create({
  // .. where we make our configurations
  baseURL: baseUrl,
  headers: headers,
});

// Override default axios error handler to throw custom error data
HttpClient.interceptors.response.use(
  function (response) {
    // Do something with response data
    return response;
  },
  function (error) {
    if (error.response) {
      return Promise.reject(error.response.data);
    }
    // Do something with response error
    return Promise.reject(error);
  }
);

export { HttpClient, baseUrl };
