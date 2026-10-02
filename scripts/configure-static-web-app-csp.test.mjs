import assert from "node:assert/strict";
import test from "node:test";
import {
  buildContentSecurityPolicy,
  configureStaticWebAppConfig,
  parseApiOrigins,
} from "./configure-static-web-app-csp.mjs";

function directives(policy) {
  return new Map(
    policy
      .split(";")
      .map((part) => part.trim())
      .filter(Boolean)
      .map((part) => {
        const [name, ...sources] = part.split(/\s+/);
        return [name, sources];
      })
  );
}

test("restricts script execution to the app, PDF WebAssembly, and Umami", () => {
  const policy = buildContentSecurityPolicy(["https://api.example.test"]);
  const parsed = directives(policy);

  assert.deepEqual(parsed.get("default-src"), ["'none'"]);
  assert.deepEqual(parsed.get("script-src"), ["'self'", "'wasm-unsafe-eval'", "https://cloud.umami.is"]);
  const scripts = parsed.get("script-src");
  assert.ok(!scripts.includes("'unsafe-eval'"));
  assert.ok(!scripts.includes("'unsafe-inline'"));
  assert.ok(!scripts.includes("https:"));
  assert.ok(!scripts.includes("*"));
  assert.deepEqual(parsed.get("object-src"), ["'none'"]);
  assert.deepEqual(parsed.get("base-uri"), ["'none'"]);
  assert.deepEqual(parsed.get("frame-ancestors"), ["'none'"]);
  assert.deepEqual(parsed.get("frame-src"), ["'self'"]);
});

test("includes required API and map origins without blanket network allowances", () => {
  const parsed = directives(buildContentSecurityPolicy(["https://api.example.test"]));
  const connect = parsed.get("connect-src");
  const images = parsed.get("img-src");

  for (const source of [
    "https://api.example.test",
    "wss://api.example.test",
    "https://cloud.umami.is",
    "https://api.mapbox.com",
    "https://tiles.arcgis.com",
    "https://macrostrat.org",
    "https://tiles.mapterhorn.com",
    "https://nominatim.openstreetmap.org",
  ]) {
    assert.ok(connect.includes(source), `connect-src should allow ${source}`);
  }

  for (const source of [
    "https://api.example.test",
    "https://saplanarian.blob.core.windows.net",
    "https://ngmdb.usgs.gov",
    "https://tiles.mapterhorn.com",
    "https://tile.openstreetmap.org",
  ]) {
    assert.ok(images.includes(source), `img-src should allow ${source}`);
  }

  assert.ok(!connect.includes("https:"));
  assert.ok(!images.includes("https:"));
  assert.ok(!connect.includes("*"));
  assert.ok(!images.includes("*"));
});

test("derives HTTPS and WSS API sources from hosted origin mappings", () => {
  assert.deepEqual(
    parseApiOrigins(
      '{"portal-a.example.com":"https://api-a.example.com","portal-b.example.com":"https://api-b.example.com"}'
    ),
    ["https://api-a.example.com", "https://api-b.example.com"]
  );

  assert.deepEqual(parseApiOrigins(undefined), []);
  assert.deepEqual(parseApiOrigins(""), []);
  assert.throws(() => parseApiOrigins("{not-json"), /valid JSON object/);
  assert.throws(
    () => parseApiOrigins('{"portal.example.com":"http://api.example.com"}'),
    /HTTPS origin/
  );
  assert.throws(
    () => parseApiOrigins('{"portal.example.com":"https://api.example.com/path"}'),
    /origin without a path/
  );
});

test("adds CSP as a global header without disturbing Static Web Apps routing", () => {
  const input = {
    navigationFallback: { rewrite: "index.html", exclude: ["*.js"] },
    globalHeaders: { "X-Existing-Header": "kept" },
  };

  const output = configureStaticWebAppConfig(input, ["https://api.example.test"]);
  assert.deepEqual(output.navigationFallback, input.navigationFallback);
  assert.equal(output.globalHeaders["X-Existing-Header"], "kept");
  assert.equal(
    output.globalHeaders["Content-Security-Policy"],
    buildContentSecurityPolicy(["https://api.example.test"])
  );
  assert.equal(output.globalHeaders["Content-Security-Policy-Report-Only"], undefined);
});

test("keeps the minimum compatibility exceptions explicit", () => {
  const parsed = directives(buildContentSecurityPolicy([]));

  assert.deepEqual(parsed.get("style-src"), [
    "'self'",
    "'unsafe-inline'",
    "https://fonts.googleapis.com",
  ]);
  assert.deepEqual(parsed.get("font-src"), [
    "'self'",
    "data:",
    "https://fonts.gstatic.com",
  ]);
  assert.deepEqual(parsed.get("worker-src"), ["'self'", "blob:"]);
  assert.deepEqual(parsed.get("child-src"), ["blob:"]);
  assert.deepEqual(parsed.get("manifest-src"), ["'self'"]);
  assert.deepEqual(parsed.get("media-src"), ["'self'", "blob:"]);
  assert.deepEqual(parsed.get("form-action"), ["'self'"]);
});
