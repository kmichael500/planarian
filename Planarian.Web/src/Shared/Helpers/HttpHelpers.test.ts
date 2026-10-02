import { HttpHelpers } from "./HttpHelpers";

describe("HttpHelpers.GetLocalRedirectUrl", () => {
  it.each([
    [null, "/"],
    ["", "/"],
    ["   ", "/"],
    ["/", "/"],
    ["/caves/ABC?tab=files#history", "/caves/ABC?tab=files#history"],
  ])("normalizes local redirect %p to %p", (redirectUrl, expected) => {
    expect(HttpHelpers.GetLocalRedirectUrl(redirectUrl)).toBe(expected);
  });

  it.each([
    "//evil.example/phish",
    "https://evil.example/phish",
    String.raw`\\evil.example\phish`,
    "javascript:alert(1)",
  ])("rejects non-local redirect %p", (redirectUrl) => {
    expect(HttpHelpers.GetLocalRedirectUrl(redirectUrl)).toBe("/");
  });
});

describe("HttpHelpers.GetSafeExternalHttpUrl", () => {
  it.each([
    [
      "https://example.com/path?x=1#section",
      "https://example.com/path?x=1#section",
    ],
    ["http://example.com/", "http://example.com/"],
  ])("allows web URL %p", (url, expected) => {
    expect(HttpHelpers.GetSafeExternalHttpUrl(url)).toBe(expected);
  });

  it.each([
    null,
    undefined,
    "",
    "/relative/path",
    "javascript:alert(1)",
    "data:text/html,<script>alert(1)</script>",
    "vbscript:msgbox(1)",
  ])("rejects non-web external URL %p", (url) => {
    expect(HttpHelpers.GetSafeExternalHttpUrl(url)).toBeNull();
  });
});
