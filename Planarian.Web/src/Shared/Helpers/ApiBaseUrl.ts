const hostedApiOrigins: Record<string, string> = {
  "app.planarian.xyz": "https://api.planarian.xyz",
  "app.planarian.org": "https://api.planarian.org",
  "app-dev.planarian.xyz": "https://api-dev.planarian.xyz",
  "app-dev.planarian.org": "https://api-dev.planarian.org",
};

export interface ApiBaseUrlOptions {
  hostname: string;
  nodeEnv?: string;
  explicitOverride?: string;
}

export function resolveApiBaseUrl({
  hostname,
  nodeEnv,
  explicitOverride,
}: ApiBaseUrlOptions): string {
  const hostedApiOrigin = hostedApiOrigins[hostname.toLowerCase()];
  if (hostedApiOrigin) {
    return hostedApiOrigin;
  }

  const trimmedOverride = explicitOverride?.trim();
  if (trimmedOverride) {
    return trimmedOverride;
  }

  if (nodeEnv === "development" || nodeEnv === "test") {
    return "https://localhost:7111";
  }

  throw new Error(
    `No API origin is configured for hosted frontend hostname "${hostname}". ` +
      "Use a Planarian hosted domain or configure REACT_APP_SERVER_URL for a custom deployment."
  );
}
