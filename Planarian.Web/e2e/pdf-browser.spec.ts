import { expect, test } from "@playwright/test";
import {
  API_ORIGIN,
  PRIMARY_ACCOUNT_NAME,
  PRIMARY_COUNTY_ID,
  accountIdByName,
  createCave,
  login,
} from "./support";

test("PDF.js renders an authenticated PDF through real CORS/CSP and the file endpoint supports ranges", async ({ browser, page }) => {
  const requests: Array<{ method: string; url: string; headers: Record<string, string> }> = [];
  const statuses: Array<{ url: string; status: number; headers: Record<string, string> }> = [];
  const cspErrors: string[] = [];

  page.on("request", (request) => {
    if (request.url().startsWith(API_ORIGIN) && request.url().includes("/api/files/")) {
      requests.push({
        method: request.method(),
        url: request.url(),
        headers: request.headers(),
      });
    }
  });
  page.on("response", (response) => {
    if (response.url().startsWith(API_ORIGIN) && response.url().includes("/api/files/")) {
      statuses.push({
        url: response.url(),
        status: response.status(),
        headers: response.headers(),
      });
    }
  });
  page.on("console", (message) => {
    if (/content security policy|refused to (load|connect|create)/i.test(message.text())) {
      cspErrors.push(message.text());
    }
  });

  await login(page);
  const accountId = await accountIdByName(page.context().request, PRIMARY_ACCOUNT_NAME);
  const { caveId, fileId } = await createCave(
    page.context().request,
    accountId,
    PRIMARY_COUNTY_ID,
    "PDF Browser Cave",
    { pdf: true }
  );
  expect(fileId).toBeTruthy();

  await page.goto("/caves/" + caveId);
  await page.getByRole("button", { name: "View browser-e2e" }).click();
  await expect(page.getByLabel("PDF viewer")).toBeVisible();
  await expect(page.locator("pdfjs-viewer-element")).toHaveCount(1);

  await expect.poll(async () =>
    page.locator("pdfjs-viewer-element").evaluate((element: any) => {
      const frame = element.iframe as HTMLIFrameElement | undefined;
      return Boolean(
        frame?.contentDocument?.querySelector(
          ".page canvas, .page .textLayer, #viewer .page"
        )
      );
    })
  ).toBeTruthy();

  const fileRequests = requests.filter((request) => request.url.includes("/api/files/" + fileId + "/view"));
  expect(fileRequests.some((request) =>
    Boolean(request.headers["x-planarian-file-stream-session"])
  )).toBeTruthy();
  // Playwright does not surface browser-managed CORS preflights as page
  // requests, and request.headers() omits some browser-owned headers such as
  // Origin. The PDF request is demonstrably cross-origin and carries a
  // non-safelisted custom header; rendering plus the CORS response assertions
  // below therefore prove the browser completed the required preflight.
  expect(new URL(fileRequests[0].url).origin).toBe(API_ORIGIN);
  expect(new URL(page.url()).origin).not.toBe(API_ORIGIN);

  // PDF.js is free to fetch a small document in one response or use byte
  // ranges depending on the browser. Prove browser CORS from the response
  // that completed, then probe the same authenticated endpoint
  // deterministically for byte-range semantics.
  const browserFileResponse = statuses.find((response) =>
    response.url.includes("/api/files/" + fileId + "/view")
  );
  expect(browserFileResponse).toBeTruthy();
  expect(browserFileResponse?.headers["access-control-allow-origin"]).toBe(
    "https://planarian.local"
  );
  expect(browserFileResponse?.headers["access-control-allow-credentials"]).toBe("true");
  expect(browserFileResponse?.headers["accept-ranges"]).toBe("bytes");

  const rangedResponse = await page.context().request.get(
    API_ORIGIN + "/api/files/" + fileId + "/view?account_id=" + accountId,
    {
      headers: {
        Origin: "https://planarian.local",
        Range: "bytes=0-1023",
        "x-planarian-file-stream-session": "playwright-range-probe",
      },
    }
  );
  expect(rangedResponse.status()).toBe(206);
  const rangedHeaders = rangedResponse.headers();
  expect(rangedHeaders["access-control-allow-origin"]).toBe("https://planarian.local");
  expect(rangedHeaders["access-control-allow-credentials"]).toBe("true");
  expect(rangedHeaders["accept-ranges"]).toBe("bytes");
  expect(rangedHeaders["content-range"]).toMatch(/^bytes 0-1023\/\d+$/);
  expect(cspErrors).toEqual([]);

  const downloadPromise = page.waitForEvent("download");
  await page.getByRole("button", { name: /Download$/ }).click();
  const download = await downloadPromise;
  expect(download.suggestedFilename()).toBe("browser-e2e.pdf");

  const unauthenticated = await browser.newContext({ ignoreHTTPSErrors: true });
  try {
    const denied = await unauthenticated.request.get(
      API_ORIGIN + "/api/files/" + fileId + "/view?account_id=" + accountId
    );
    expect(denied.status()).toBe(401);
  } finally {
    await unauthenticated.close();
  }

  const missing = await page.context().request.get(
    API_ORIGIN + "/api/files/aaaaaaaaaa/view?account_id=" + accountId
  );
  expect(missing.status()).toBe(404);
});
