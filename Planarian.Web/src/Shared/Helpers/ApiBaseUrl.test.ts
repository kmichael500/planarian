import { parseApiOriginMappings, resolveApiBaseUrl } from "./ApiBaseUrl";

describe("API origin mappings", () => {
  const mappings = (value: string, nodeEnv?: string) => parseApiOriginMappings(value, nodeEnv);

  it("resolves a configured frontend hostname without inferring an API name", () => {
    expect(
      resolveApiBaseUrl({
        hostname: "portal.example.com",
        nodeEnv: "production",
        mappings: mappings('{"portal.example.com":"https://services.example.com"}'),
      })
    ).toBe("https://services.example.com");
  });

  it("supports multiple independent mappings", () => {
    const configuredMappings = mappings(
      '{"frontend-a.example.com":"https://services.example.com","other.example.org":"https://backend.example.org"}'
    );

    expect(resolveApiBaseUrl({ hostname: "frontend-a.example.com", nodeEnv: "production", mappings: configuredMappings })).toBe("https://services.example.com");
    expect(resolveApiBaseUrl({ hostname: "other.example.org", nodeEnv: "production", mappings: configuredMappings })).toBe("https://backend.example.org");
  });

  it("compares hostnames case-insensitively and normalizes API origins", () => {
    const configuredMappings = mappings(
      '{" Portal.Example.Com ":" HTTPS://SERVICES.EXAMPLE.COM:443/ "}'
    );

    expect(resolveApiBaseUrl({ hostname: "PORTAL.example.COM", nodeEnv: "production", mappings: configuredMappings })).toBe("https://services.example.com");
  });

  it.each([
    ["{not-json"],
    ["[\"https://services.example.com\"]"],
  ])("rejects malformed mapping input %s", (value) => {
    expect(() => mappings(value)).toThrow("REACT_APP_API_ORIGIN_MAPPINGS");
  });

  it.each([
    ['{"portal.example.com":"not a url"}'],
    ['{"portal.example.com":"ftp://services.example.com"}'],
  ])("rejects invalid API origins", (value) => {
    expect(() => mappings(value)).toThrow("API origin");
  });

  it("rejects insecure hosted API origins", () => {
    expect(() => mappings('{"portal.example.com":"http://services.example.com"}', "production"))
      .toThrow("absolute HTTPS origin");
  });

  it("rejects mapping keys with ports because window.location.hostname excludes them", () => {
    expect(() => mappings('{"portal.example.com:3000":"https://services.example.com"}')).toThrow(
      "Invalid frontend hostname mapping key"
    );
  });

  it("fails for an unmapped production hostname", () => {
    expect(() =>
      resolveApiBaseUrl({
        hostname: "missing.example.com",
        nodeEnv: "production",
        mappings: mappings('{"portal.example.com":"https://services.example.com"}'),
      })
    ).toThrow("No API origin is configured");
  });

  it("fails for missing production configuration", () => {
    expect(() =>
      resolveApiBaseUrl({ hostname: "portal.example.com", nodeEnv: "production", mappings: mappings("") })
    ).toThrow("REACT_APP_API_ORIGIN_MAPPINGS is required");
  });

  it("uses the local API default during development", () => {
    expect(
      resolveApiBaseUrl({ hostname: "localhost", nodeEnv: "development", mappings: mappings("") })
    ).toBe("https://localhost:7111");
  });

  it("uses a configured local development mapping when supplied", () => {
    expect(
      resolveApiBaseUrl({
        hostname: "localhost",
        nodeEnv: "development",
        mappings: mappings('{"localhost":"http://localhost:7123"}', "development"),
      })
    ).toBe("http://localhost:7123");
  });
});
