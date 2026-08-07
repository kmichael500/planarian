export type ApiOriginMappings = ReadonlyMap<string, string>;

export interface ApiBaseUrlOptions {
  hostname: string;
  nodeEnv?: string;
  mappings: ApiOriginMappings;
}

export function parseApiOriginMappings(
  value: string | undefined,
  nodeEnv?: string
): ApiOriginMappings {
  if (!value?.trim()) {
    return new Map();
  }

  let parsed: unknown;
  try {
    parsed = JSON.parse(value);
  } catch {
    throw new Error("REACT_APP_API_ORIGIN_MAPPINGS must be valid JSON.");
  }

  if (
    parsed === null ||
    Array.isArray(parsed) ||
    typeof parsed !== "object"
  ) {
    throw new Error("REACT_APP_API_ORIGIN_MAPPINGS must be a JSON object mapping frontend hostnames to API origins.");
  }

  const mappings = new Map<string, string>();
  for (const [rawHostname, rawOrigin] of Object.entries(parsed)) {
    const hostname = normalizeHostname(rawHostname);
    if (mappings.has(hostname)) {
      throw new Error(
        `REACT_APP_API_ORIGIN_MAPPINGS contains duplicate hostname "${hostname}".`
      );
    }

    mappings.set(hostname, normalizeApiOrigin(rawOrigin, hostname, nodeEnv));
  }

  return mappings;
}

export function resolveApiBaseUrl({
  hostname,
  nodeEnv,
  mappings,
}: ApiBaseUrlOptions): string {
  const normalizedHostname = normalizeHostname(hostname);
  const configuredOrigin = mappings.get(normalizedHostname);
  if (configuredOrigin) {
    return configuredOrigin;
  }

  if (nodeEnv === "development" || nodeEnv === "test") {
    return "https://localhost:7111";
  }

  if (mappings.size === 0) {
    throw new Error(
      "REACT_APP_API_ORIGIN_MAPPINGS is required for a production build."
    );
  }

  throw new Error(
    `No API origin is configured for frontend hostname "${hostname}" in REACT_APP_API_ORIGIN_MAPPINGS.`
  );
}

function normalizeHostname(value: string): string {
  const hostname = value.trim().toLowerCase();
  if (
    !hostname ||
    hostname.includes("://") ||
    /[/?#]/.test(hostname)
  ) {
    throw new Error(`Invalid frontend hostname mapping key "${value}".`);
  }

  try {
    const url = new URL(`http://${hostname}`);
    if (url.hostname !== hostname || url.port) {
      throw new Error();
    }
  } catch {
    throw new Error(`Invalid frontend hostname mapping key "${value}".`);
  }

  return hostname;
}

function normalizeApiOrigin(value: unknown, hostname: string, nodeEnv?: string): string {
  if (typeof value !== "string") {
    throw new Error(`API origin for frontend hostname "${hostname}" must be a string.`);
  }

  let url: URL;
  try {
    url = new URL(value.trim());
  } catch {
    throw new Error(`API origin for frontend hostname "${hostname}" must be an absolute HTTP/HTTPS origin.`);
  }

  const permitsLocalHttp =
    nodeEnv === "development" && hostname === "localhost" && url.protocol === "http:";
  if (
    (url.protocol !== "https:" && !permitsLocalHttp) ||
    url.username ||
    url.password ||
    url.pathname !== "/" ||
    url.search ||
    url.hash
  ) {
    throw new Error(`API origin for frontend hostname "${hostname}" must be an absolute HTTPS origin without a path, query, or fragment.`);
  }

  return url.origin;
}
