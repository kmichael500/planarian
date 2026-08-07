import React from "react";
import ReactDOM from "react-dom/client";
import utc from "dayjs/plugin/utc";
import customParseFormat from "dayjs/plugin/customParseFormat";
import dayjs from "dayjs";
import { registerLicense } from "@syncfusion/ej2-base";
import "./index.css";
import App from "./App";
import reportWebVitals from "./reportWebVitals";
import { BootstrapErrorBoundary } from "./Shared/Components/BootstrapErrorBoundary";
import { configureHttpClient } from "./Shared/Http/HttpClient";
import { parseApiOriginMappings, resolveApiBaseUrl } from "./Shared/Helpers/ApiBaseUrl";

dayjs.extend(utc);
dayjs.extend(customParseFormat);

function showBootstrapError(): void {
  const rootElement = document.getElementById("root");
  if (rootElement) {
    rootElement.replaceChildren();
    const message = document.createElement("main");
    message.setAttribute("role", "alert");
    message.style.padding = "2rem";
    message.style.fontFamily = "sans-serif";
    message.innerHTML = "<h1>Planarian failed to start</h1><p>Please reload the page and try again.</p>";
    rootElement.appendChild(message);
  }
}

function configureBootstrap(): void {
  const baseUrl = resolveApiBaseUrl({
    hostname: window.location.hostname,
    nodeEnv: process.env.NODE_ENV,
    mappings: parseApiOriginMappings(process.env.REACT_APP_API_ORIGIN_MAPPINGS, process.env.NODE_ENV),
  });
  configureHttpClient(baseUrl);

  const setVh = () => {
    document.documentElement.style.setProperty("--vh", `${window.innerHeight * 0.01}px`);
  };
  setVh();
  window.addEventListener("resize", setVh);

  const umamiWebsiteId = process.env.REACT_APP_UMAMI_WEBSITE_ID?.trim();
  if (umamiWebsiteId) {
    const script = document.createElement("script");
    script.src = "https://cloud.umami.is/script.js";
    script.defer = true;
    script.setAttribute("data-website-id", umamiWebsiteId);
    document.body.appendChild(script);
  }

  registerLicense("Ngo9BigBOggjHTQxAR8/V1NNaF5cXmBCf1FpRmJGdld5fUVHYVZUTXxaS00DNHVRdkdmWXxdcXVWR2VYU01wXkpWYUA=");
}

try {
  configureBootstrap();
  const rootElement = document.getElementById("root");
  if (!rootElement) {
    throw new Error("The application root element is missing.");
  }
  ReactDOM.createRoot(rootElement).render(
    <React.StrictMode>
      <BootstrapErrorBoundary>
        <App />
      </BootstrapErrorBoundary>
    </React.StrictMode>
  );
  reportWebVitals();
} catch (error) {
  console.error("Planarian failed to start.", error);
  showBootstrapError();
}
