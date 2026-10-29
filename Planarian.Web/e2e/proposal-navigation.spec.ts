import { expect, test } from "@playwright/test";
import {
  SECONDARY_ACCOUNT_NAME,
  SECONDARY_COUNTY_ID,
  accountIdByName,
  createCave,
  initialize,
  login,
  switchAccount,
} from "./support";

test("Cave detail to suggest-changes to proposal works through nested routes and hard refresh", async ({ browser, page }) => {
  await login(page);
  const adminRequest = page.context().request;
  const secondaryId = await accountIdByName(adminRequest, SECONDARY_ACCOUNT_NAME);
  const { caveId } = await createCave(
    adminRequest,
    secondaryId,
    SECONDARY_COUNTY_ID,
    "Proposal Browser Cave"
  );

  const managerContext = await browser.newContext({ ignoreHTTPSErrors: true });
  const managerPage = await managerContext.newPage();
  try {
    await login(managerPage, "manager@planarian.local");
    const init = await initialize(managerContext.request);
    if (init.currentUser?.currentAccountId !== secondaryId) {
      await switchAccount(managerPage, SECONDARY_ACCOUNT_NAME, secondaryId);
    }

    await managerPage.goto("/caves/" + caveId);
    await managerPage.getByRole("button", { name: /Suggest Changes$/ }).click();
    await expect(managerPage).toHaveURL(new RegExp("/caves/" + caveId + "/suggest$"));

    await managerPage.reload();
    await expect(managerPage.getByText("Make the changes you want reviewed.")).toBeVisible();

    await managerPage.getByLabel("Name", { exact: true }).first().fill(
      "Proposal Browser Cave Suggested"
    );
    await managerPage.getByRole("button", { name: "Review changes", exact: true }).click();
    await expect(managerPage.getByRole("heading", { name: "Review your changes" })).toBeVisible();
    await expect(managerPage.getByText("Proposal Browser Cave Suggested", { exact: true })).toBeVisible();

    await managerPage.getByRole("button", { name: /Submit for review$/ }).click();
    await expect(managerPage).toHaveURL(/\/caves\/requests\/[^/]+$/);
    const requestRegion = managerPage.getByRole("region", { name: "Change request" });
    await expect(requestRegion).toContainText("1 cave field");
    await expect(requestRegion).toContainText("Proposal Browser Cave Suggested");

    const proposalUrl = managerPage.url();
    await managerPage.reload();
    await expect(managerPage).toHaveURL(proposalUrl);
    const reloadedRequestRegion = managerPage.getByRole("region", { name: "Change request" });
    await expect(reloadedRequestRegion).toContainText("1 cave field");
    await expect(reloadedRequestRegion).toContainText("Proposal Browser Cave Suggested");
  } finally {
    await managerContext.close();
  }
});
