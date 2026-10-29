import { expect, test } from "@playwright/test";
import {
  API_ORIGIN,
  PRIMARY_ACCOUNT_NAME,
  PRIMARY_COUNTY_ID,
  accountIdByName,
  createCave,
  login,
} from "./support";

test.describe("authenticated browser runtime", () => {
  test("bootstraps with cookies, CORS, CSP, reload persistence, and nested navigation", async ({ page }) => {
    const apiRequests: string[] = [];
    const loginResponses: Array<Record<string, string>> = [];
    page.on("request", (request) => {
      if (request.url().startsWith(API_ORIGIN)) {
        apiRequests.push(request.url());
      }
    });
    page.on("response", async (response) => {
      if (response.url().includes("/api/authentication/login")) {
        loginResponses.push(response.headers());
      }
    });

    const documentResponse = await page.goto("/login");
    expect(documentResponse?.headers()["content-security-policy"]).toContain("worker-src 'self' blob:");
    expect(documentResponse?.headers()["content-security-policy"]).toContain(API_ORIGIN);

    await login(page);
    expect(apiRequests.some((url) => url.includes("/api/authentication/login"))).toBeTruthy();
    expect(loginResponses[0]?.["access-control-allow-origin"]).toBe("https://planarian.local");
    expect(loginResponses[0]?.["access-control-allow-credentials"]).toBe("true");

    const accountId = await accountIdByName(page.context().request, PRIMARY_ACCOUNT_NAME);
    const { caveId } = await createCave(
      page.context().request,
      accountId,
      PRIMARY_COUNTY_ID,
      "Browser Navigation Cave"
    );

    await page.goto("/caves/" + caveId);
    await expect(
      page.getByRole("heading", { name: /Browser Navigation Cave$/ })
    ).toBeVisible();
    await expect(page.getByRole("link", { name: /Edit$/ })).toBeVisible();

    await page.reload();
    await expect(
      page.getByRole("heading", { name: /Browser Navigation Cave$/ })
    ).toBeVisible();

    await page.getByRole("link", { name: /Edit$/ }).click();
    await expect(page).toHaveURL(new RegExp("/caves/" + caveId + "/edit$"));
    await page.goBack();
    await expect(page).toHaveURL(new RegExp("/caves/" + caveId + "$"));
    await page.goForward();
    await expect(page).toHaveURL(new RegExp("/caves/" + caveId + "/edit$"));
    await page.reload();
    await expect(page.getByLabel("Name", { exact: true }).first()).toHaveValue(
      "Browser Navigation Cave"
    );

    await page.goto("/caves/" + caveId);
    await page.getByRole("button", { name: /History$/ }).click();
    const historyDialog = page.getByRole("dialog").filter({ hasText: "Cave History" });
    await expect(historyDialog).toBeVisible();
    await expect(historyDialog.getByText(/Revision 1/)).toBeVisible();
    await historyDialog.getByRole("button", { name: /Created Current Revision 1/ }).click();
    await expect(historyDialog.locator(".ant-spin-spinning")).toHaveCount(0);
  });

  test("invalid or expired browser session returns to login instead of retaining authenticated UI", async ({ page }) => {
    await login(page);
    await page.context().clearCookies();
    await page.goto("/caves");
    await expect(page).toHaveURL(/\/login/);
    await expect(page.getByRole("button", { name: /Login$/ })).toBeVisible();
  });

  test("unauthenticated direct navigation to a nested protected route is redirected safely", async ({ page }) => {
    await page.goto("/caves/aaaaaaaaaa/edit");
    await expect(page).toHaveURL(/\/login/);
  });
});
