import { parseApiOriginMappings, resolveApiBaseUrl } from "./ApiBaseUrl";

describe("API origin mappings", () => {
  const mappings = (value: string) => parseApiOriginMappings(value);

  it("resolves a configured frontend hostname without inferring an API name", () => {
    expect(
      resolveApiBaseUrl({
        hostname: "portal.example.com",
        nodeEnv: "production",
        mappings: mappings('{"portal.example.com":"https://services.unrelated-domain.net"}'),
      })
    ).toBe("https://services.unrelated-domain.net");
  });

  it("supports multiple independent mappings", () => {
    const configuredMappings = mappings(
      '{"frontend-a.example.com":"https://totally-different-api.example.net","other.example.net":"https://services.example.org"}'
    );

    expect(resolveApiBaseUrl({ hostname: "frontend-a.example.com", nodeEnv: "production", mappings: configuredMappings })).toBe("https://totally-different-api.example.net");
    expect(resolveApiBaseUrl({ hostname: "other.example.net", nodeEnv: "production", mappings: configuredMappings })).toBe("https://services.example.org");
  });

  it("compares hostnames case-insensitively and normalizes API origins", () => {
    const configuredMappings = mappings(
      '{" Portal.Example.Com ":" HTTPS://SERVICES.EXAMPLE.NET:443/ "}'
    );

    expect(resolveApiBaseUrl({ hostname: "PORTAL.example.COM", nodeEnv: "production", mappings: configuredMappings })).toBe("https://services.example.net");
  });

  it.each([
    ["{not-json"],
    ["[\"https://services.example.net\"]"],
  ])("rejects malformed mapping input %s", (value) => {
    expect(() => mappings(value)).toThrow("REACT_APP_API_ORIGIN_MAPPINGS");
  });

  it.each([
    ['{"portal.example.com":"not a url"}'],
    ['{"portal.example.com":"ftp://services.example.net"}'],
  ])("rejects invalid API origins", (value) => {
    expect(() => mappings(value)).toThrow("API origin");
  });

  it("fails for an unmapped production hostname", () => {
    expect(() =>
      resolveApiBaseUrl({
        hostname: "missing.example.com",
        nodeEnv: "production",
        mappings: mappings('{"portal.example.com":"https://services.example.net"}'),
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
        mappings: mappings('{"localhost":"http://localhost:7123"}'),
      })
    ).toBe("http://localhost:7123");
  });
});
