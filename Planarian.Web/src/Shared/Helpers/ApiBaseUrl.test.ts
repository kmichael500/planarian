import { resolveApiBaseUrl } from "./ApiBaseUrl";

describe("resolveApiBaseUrl", () => {
  test.each([
    ["app.planarian.xyz", "https://api.planarian.xyz"],
    ["app.planarian.org", "https://api.planarian.org"],
    ["app-dev.planarian.xyz", "https://api-dev.planarian.xyz"],
    ["app-dev.planarian.org", "https://api-dev.planarian.org"],
  ])("maps %s to %s", (hostname, expected) => {
    expect(resolveApiBaseUrl({ hostname, nodeEnv: "production" })).toBe(expected);
  });

  it("uses the local API for development without an override", () => {
    expect(
      resolveApiBaseUrl({ hostname: "localhost", nodeEnv: "development" })
    ).toBe("https://localhost:7111");
  });

  it("uses an explicit override for a custom developer host", () => {
    expect(
      resolveApiBaseUrl({
        hostname: "preview.example.test",
        nodeEnv: "production",
        explicitOverride: " https://api.preview.example.test ",
      })
    ).toBe("https://api.preview.example.test");
  });

  it("does not let an override replace a known Planarian domain mapping", () => {
    expect(
      resolveApiBaseUrl({
        hostname: "app.planarian.org",
        nodeEnv: "production",
        explicitOverride: "https://api.preview.example.test",
      })
    ).toBe("https://api.planarian.org");
  });

  it("fails clearly for an unknown hosted hostname", () => {
    expect(() =>
      resolveApiBaseUrl({ hostname: "app.example.test", nodeEnv: "production" })
    ).toThrow('No API origin is configured for hosted frontend hostname "app.example.test"');
  });
});
