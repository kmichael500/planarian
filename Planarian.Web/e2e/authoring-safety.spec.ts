import { expect, test } from "@playwright/test";
import path from "node:path";
import {
  PRIMARY_ACCOUNT_NAME,
  PRIMARY_COUNTY_ID,
  accountIdByName,
  createCave,
  getCave,
  login,
} from "./support";

test.describe("Cave authoring browser safety", () => {
  test("protects dirty edits for SPA navigation and preserves keep/discard choices", async ({ page }) => {
    await login(page);
    const accountId = await accountIdByName(page.context().request, PRIMARY_ACCOUNT_NAME);
    const { caveId } = await createCave(
      page.context().request,
      accountId,
      PRIMARY_COUNTY_ID,
      "Unsaved Navigation Cave"
    );

    await page.goto("/caves/" + caveId + "/edit");
    const name = page.getByLabel("Name", { exact: true }).first();
    await expect(name).toHaveValue("Unsaved Navigation Cave");
    await name.fill("Unsaved Navigation Cave changed");

    await page.getByRole("button", { name: /Back$/ }).click();
    const modal = page.getByRole("dialog").filter({ hasText: "Discard unsaved changes?" });
    await expect(modal).toBeVisible();
    await modal.getByRole("button", { name: "Keep editing" }).click();
    await expect(page).toHaveURL(new RegExp("/caves/" + caveId + "/edit$"));
    await expect(name).toHaveValue("Unsaved Navigation Cave changed");

    await page.getByRole("button", { name: /Back$/ }).click();
    await modal.getByRole("button", { name: "Discard changes" }).click();
    await expect(page).toHaveURL(/\/caves(?:\?|$)/);
  });

  test("asset-only File changes are dirty and browser reload is protected", async ({ page }) => {
    await login(page);
    const accountId = await accountIdByName(page.context().request, PRIMARY_ACCOUNT_NAME);
    const { caveId } = await createCave(
      page.context().request,
      accountId,
      PRIMARY_COUNTY_ID,
      "Unsaved File Cave"
    );

    await page.goto("/caves/" + caveId + "/edit");
    const fixture = path.join(__dirname, "fixtures", "browser-e2e.pdf");
    await page.locator('input[type="file"]').first().setInputFiles(fixture);
    // Wait for staging to finish, not merely for the temporary upload card to
    // appear. The authoring-change callback fires only after the staged file is
    // committed into the form.
    await expect(page.getByRole("group", { name: "browser-e2e file editor" })).toBeVisible();

    await page.getByRole("button", { name: /Back$/ }).click();
    const modal = page.getByRole("dialog").filter({ hasText: "Discard unsaved changes?" });
    await expect(modal).toBeVisible();
    await modal.getByRole("button", { name: "Keep editing" }).click();

    const beforeUnload = page.waitForEvent("dialog");
    const reloadAttempt = page.reload({ timeout: 2_000 }).catch(() => null);
    const dialog = await beforeUnload;
    expect(dialog.type()).toBe("beforeunload");
    await dialog.dismiss();
    await reloadAttempt;
    await expect(page).toHaveURL(new RegExp("/caves/" + caveId + "/edit$"));
  });

  test("stale editor receives conflict feedback and cannot overwrite a newer save", async ({ browser, page }) => {
    await login(page);
    const accountId = await accountIdByName(page.context().request, PRIMARY_ACCOUNT_NAME);
    const { caveId } = await createCave(
      page.context().request,
      accountId,
      PRIMARY_COUNTY_ID,
      "Stale Browser Cave"
    );

    const secondContext = await browser.newContext({ ignoreHTTPSErrors: true });
    const secondPage = await secondContext.newPage();
    try {
      await login(secondPage);

      await Promise.all([
        page.goto("/caves/" + caveId + "/edit"),
        secondPage.goto("/caves/" + caveId + "/edit"),
      ]);

      const firstName = page.getByLabel("Name", { exact: true }).first();
      const secondName = secondPage.getByLabel("Name", { exact: true }).first();
      await expect(firstName).toHaveValue("Stale Browser Cave");
      await expect(secondName).toHaveValue("Stale Browser Cave");

      await firstName.fill("Winner Browser Edit");
      await page.getByRole("button", { name: "Submit", exact: true }).click();
      await expect(page.getByText("'Winner Browser Edit' has been updated successfully")).toBeVisible();

      await secondName.fill("Stale Browser Edit");
      await secondPage.getByRole("button", { name: "Submit", exact: true }).click();
      await expect(
        secondPage.getByText(
          "This Cave changed after you opened the editor. Reload before saving so you do not overwrite newer changes."
        )
      ).toBeVisible();

      const persisted = await getCave(page.context().request, accountId, caveId);
      expect(persisted.name).toBe("Winner Browser Edit");
    } finally {
      await secondContext.close();
    }
  });
});
