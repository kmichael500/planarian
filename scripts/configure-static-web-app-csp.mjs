import { readFile, writeFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

const CONNECT_SOURCES = [
  "https://cloud.umami.is",
  "https://api.mapbox.com",
  "https://api.maptiler.com",
  "https://elevation.nationalmap.gov",
  "https://epqs.nationalmap.gov",
  "https://hydro.nationalmap.gov",
  "https://macrostrat.org",
  "https://ngmdb-tiles.usgs.gov",
  "https://nominatim.openstreetmap.org",
  "https://services.arcgis.com",
  "https://services.arcgisonline.com",
  "https://tile.openstreetmap.org",
  "https://tile.opentopomap.org",
  "https://tiles.arcgis.com",
  "https://tiles.macrostrat.org",
  "https://waterservices.usgs.gov",
  "https://xdd.wisc.edu",
];

const IMAGE_SOURCES = [
  "https://api.maptiler.com",
  "https://elevation.nationalmap.gov",
  "https://hydro.nationalmap.gov",
  "https://ngmdb-tiles.usgs.gov",
  "https://ngmdb.usgs.gov",
  "https://saplanarian.blob.core.windows.net",
  "https://services.arcgisonline.com",
  "https://tile.openstreetmap.org",
  "https://tile.opentopomap.org",
  "https://tiles.arcgis.com",
  "https://tiles.macrostrat.org",
];

const unique = (values) => [...new Set(values)];

export function parseApiOrigins(value) {
  if (!value?.trim()) return [];

  let parsed;
  try {
    parsed = JSON.parse(value);
  } catch {
    throw new Error("REACT_APP_API_ORIGIN_MAPPINGS must be a valid JSON object.");
  }

  if (parsed === null || Array.isArray(parsed) || typeof parsed !== "object") {
    throw new Error("REACT_APP_API_ORIGIN_MAPPINGS must be a valid JSON object.");
  }

  const origins = [];
  for (const rawOrigin of Object.values(parsed)) {
    if (typeof rawOrigin !== "string") {
      throw new Error("Each API mapping value must be an HTTPS origin.");
    }
    let url;
    try {
      url = new URL(rawOrigin);
    } catch {
      throw new Error("Each API mapping value must be an HTTPS origin.");
    }

    if (url.protocol !== "https:" || url.username || url.password) {
      throw new Error("Each API mapping value must be an HTTPS origin.");
    }
    if (url.pathname !== "/" || url.search || url.hash) {
      throw new Error("Each API mapping value must be an origin without a path, query, or fragment.");
    }

    origins.push(url.origin);
  }

  return unique(origins);
}

function websocketSources(apiOrigins) {
  return apiOrigins.map((origin) => {
    const url = new URL(origin);
    return `wss://${url.host}`;
  });
}

export function buildContentSecurityPolicy(apiOrigins) {
  const connectSources = unique([
    "'self'",
    ...apiOrigins,
    ...websocketSources(apiOrigins),
    ...CONNECT_SOURCES,
  ]);
  const imageSources = unique([
    "'self'",
    "data:",
    "blob:",
    ...apiOrigins,
    ...IMAGE_SOURCES,
  ]);

  const directives = [
    ["default-src", ["'none'"]],
    ["script-src", ["'self'", "'wasm-unsafe-eval'", "https://cloud.umami.is"]],
    ["style-src", ["'self'", "'unsafe-inline'", "https://fonts.googleapis.com"]],
    ["img-src", imageSources],
    ["font-src", ["'self'", "data:", "https://fonts.gstatic.com"]],
    ["connect-src", connectSources],
    ["worker-src", ["'self'", "blob:"]],
    ["child-src", ["blob:"]],
    ["frame-src", ["'none'"]],
    ["media-src", ["'self'", "blob:"]],
    ["manifest-src", ["'self'"]],
    ["object-src", ["'none'"]],
    ["base-uri", ["'none'"]],
    ["frame-ancestors", ["'none'"]],
    ["form-action", ["'self'"]],
  ];

  return directives.map(([name, sources]) => `${name} ${sources.join(" ")}`).join("; ");
}

export function configureStaticWebAppConfig(config, apiOrigins) {
  return {
    ...config,
    globalHeaders: {
      ...(config.globalHeaders ?? {}),
      "Content-Security-Policy": buildContentSecurityPolicy(apiOrigins),
    },
  };
}

async function main() {
  const repositoryRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
  const configPath = process.env.PLANARIAN_STATIC_WEB_APP_CONFIG_PATH?.trim()
    ? path.resolve(process.env.PLANARIAN_STATIC_WEB_APP_CONFIG_PATH)
    : path.join(repositoryRoot, "Planarian.Web", "build", "staticwebapp.config.json");

  const config = JSON.parse(await readFile(configPath, "utf8"));
  const apiOrigins = parseApiOrigins(process.env.REACT_APP_API_ORIGIN_MAPPINGS);
  const configured = configureStaticWebAppConfig(config, apiOrigins);
  await writeFile(configPath, `${JSON.stringify(configured, null, 2)}\n`, "utf8");
}

const invokedPath = process.argv[1] ? pathToFileURL(path.resolve(process.argv[1])).href : null;
if (invokedPath === import.meta.url) {
  main().catch((error) => {
    console.error(error instanceof Error ? error.message : error);
    process.exitCode = 1;
  });
}
