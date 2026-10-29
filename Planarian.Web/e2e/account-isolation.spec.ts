import { expect, test } from "@playwright/test";
import {
  PRIMARY_ACCOUNT_NAME,
  PRIMARY_COUNTY_ID,
  SECONDARY_ACCOUNT_NAME,
  SECONDARY_COUNTY_ID,
  accountIdByName,
  createCave,
  getCave,
  initialize,
  login,
  switchAccount,
} from "./support";

test("Account A/B state, caches, permissions, and submissions stay isolated without a reload", async ({ browser, page }) => {
  await login(page);
  const request = page.context().request;
  const primaryId = await accountIdByName(request, PRIMARY_ACCOUNT_NAME);
  const secondaryId = await accountIdByName(request, SECONDARY_ACCOUNT_NAME);
  let current = await initialize(request);

  if (current.currentUser?.currentAccountId !== primaryId) {
    await switchAccount(page, PRIMARY_ACCOUNT_NAME, primaryId);
  }

  const primary = await createCave(
    request,
    primaryId,
    PRIMARY_COUNTY_ID,
    "Account A Browser Cave"
  );
  const secondary = await createCave(
    request,
    secondaryId,
    SECONDARY_COUNTY_ID,
    "Account B Browser Cave"
  );

  await page.goto("/caves/" + primary.caveId + "/edit");
  await expect(page.getByLabel("Alternate Names")).toBeVisible();
  await expect(page.getByText("Demo County", { exact: true })).toBeVisible();

  const geologyA = page.getByLabel("Geology");
  await geologyA.click();
  await expect(page.getByText("Bangor Limestone", { exact: true })).toBeVisible();
  await expect(page.getByText("Copper Ridge Limestone", { exact: true })).toHaveCount(0);
  await page.keyboard.press("Escape");

  await page.goto("/caves");
  await page.evaluate(() => {
    (window as any).__planarianAccountSwitchSentinel = "same-document";
  });

  await switchAccount(page, SECONDARY_ACCOUNT_NAME, secondaryId);
  expect(
    await page.evaluate(() => (window as any).__planarianAccountSwitchSentinel)
  ).toBe("same-document");

  await page.goto("/caves/" + secondary.caveId + "/edit");
  await expect(page.getByLabel("Alternate Names")).toHaveCount(0);
  await expect(page.getByText("Isolation County", { exact: true })).toBeVisible();

  const geologyB = page.getByLabel("Geology");
  await geologyB.click();
  await expect(page.getByText("Copper Ridge Limestone", { exact: true })).toBeVisible();
  await expect(page.getByText("Bangor Limestone", { exact: true })).toHaveCount(0);
  await page.keyboard.press("Escape");

  await page.getByLabel("Name", { exact: true }).first().fill("Account B Browser Cave Edited");
  await page.getByRole("button", { name: "Submit", exact: true }).click();
  await expect(
    page.getByText("'Account B Browser Cave Edited' has been updated successfully")
  ).toBeVisible();

  expect((await getCave(request, secondaryId, secondary.caveId)).name).toBe(
    "Account B Browser Cave Edited"
  );
  expect((await getCave(request, primaryId, primary.caveId)).name).toBe(
    "Account A Browser Cave"
  );

  await switchAccount(page, PRIMARY_ACCOUNT_NAME, primaryId);
  await page.goto("/caves/" + primary.caveId + "/edit");
  await expect(page.getByLabel("Alternate Names")).toBeVisible();
  await expect(page.getByText("Demo County", { exact: true })).toBeVisible();
  await page.getByLabel("Geology").click();
  await expect(page.getByText("Bangor Limestone", { exact: true })).toBeVisible();
  await expect(page.getByText("Copper Ridge Limestone", { exact: true })).toHaveCount(0);
  await page.keyboard.press("Escape");

  const managerContext = await browser.newContext({ ignoreHTTPSErrors: true });
  const managerPage = await managerContext.newPage();
  try {
    await login(managerPage, "manager@planarian.local");
    const managerRequest = managerContext.request;
    current = await initialize(managerRequest);
    if (current.currentUser?.currentAccountId !== primaryId) {
      await switchAccount(managerPage, PRIMARY_ACCOUNT_NAME, primaryId);
    }

    await managerPage.goto("/caves/" + primary.caveId);
    await expect(managerPage.getByRole("link", { name: /Edit$/ })).toBeVisible();

    await switchAccount(managerPage, SECONDARY_ACCOUNT_NAME, secondaryId);
    await managerPage.goto("/caves/" + secondary.caveId);
    await expect(managerPage.getByRole("link", { name: /Edit$/ })).toHaveCount(0);
    await expect(
      managerPage.getByRole("button", { name: /Suggest Changes$/ })
    ).toBeVisible();
  } finally {
    await managerContext.close();
  }
});


test("switching from a dirty Account A form requires confirmation and never leaves the stale form actionable", async ({ page }) => {
  await login(page);
  const request = page.context().request;
  const primaryId = await accountIdByName(request, PRIMARY_ACCOUNT_NAME);
  const secondaryId = await accountIdByName(request, SECONDARY_ACCOUNT_NAME);
  const current = await initialize(request);
  if (current.currentUser?.currentAccountId !== primaryId) {
    await switchAccount(page, PRIMARY_ACCOUNT_NAME, primaryId);
  }

  const { caveId } = await createCave(
    request,
    primaryId,
    PRIMARY_COUNTY_ID,
    "Dirty Account Switch Cave"
  );

  await page.goto("/caves/" + caveId + "/edit");
  const name = page.getByLabel("Name", { exact: true }).first();
  await name.fill("Dirty Account A value");

  page.once("dialog", async (dialog) => {
    expect(dialog.type()).toBe("confirm");
    expect(dialog.message()).toContain("Discard unsaved changes and switch accounts?");
    await dialog.dismiss();
  });
  await page.locator(".ant-avatar").click();
  await page.getByText("Switch Account", { exact: true }).click();
  await page.getByRole("dialog", { name: "Switch Accounts" })
    .getByText(SECONDARY_ACCOUNT_NAME, { exact: true }).click();

  await expect(page).toHaveURL(new RegExp("/caves/" + caveId + "/edit$"));
  await expect(name).toHaveValue("Dirty Account A value");

  page.once("dialog", async (dialog) => {
    expect(dialog.type()).toBe("confirm");
    await dialog.accept();
  });
  await switchAccount(page, SECONDARY_ACCOUNT_NAME, secondaryId);

  await expect(page.getByLabel("Name", { exact: true })).toHaveCount(0);
  await expect(page.getByRole("button", { name: "Submit", exact: true })).toHaveCount(0);
  expect((await getCave(request, primaryId, caveId)).name).toBe("Dirty Account Switch Cave");
});
