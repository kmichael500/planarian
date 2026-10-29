import { expect, Page, APIRequestContext } from "@playwright/test";
import { readFile } from "node:fs/promises";
import path from "node:path";

export const APP_ORIGIN = "https://planarian.local";
export const API_ORIGIN = "https://planarian.local:8443";
export const PRIMARY_ACCOUNT_NAME = "Planarian Local Development";
export const SECONDARY_ACCOUNT_NAME = "Planarian E2E Secondary";
export const PRIMARY_COUNTY_ID = "e2ecounty1";
export const SECONDARY_COUNTY_ID = "e2ecounty2";

type SelectItem = { value: string; display: string };
type AppInitialize = {
  accountIds: SelectItem[];
  currentUser: { id: string; currentAccountId: string | null } | null;
  antiforgeryRequestToken: string | null;
  permissions: string[];
};

export type CaveRecord = {
  id: string;
  currentRevisionId: string;
  name: string;
  alternateNames: string[];
  countyId: string;
  stateId: string;
  lengthFeet: number | null;
  depthFeet: number | null;
  maxPitDepthFeet: number | null;
  numberOfPits: number | null;
  narrative: string | null;
  reportedOn: string | null;
  entrances: Array<Record<string, unknown>>;
  geologyTagIds: string[];
  files: Array<Record<string, unknown>>;
  reportedByNameTagIds: string[];
  biologyTagIds: string[];
  archeologyTagIds: string[];
  cartographerNameTagIds: string[];
  mapStatusTagIds: string[];
  geologicAgeTagIds: string[];
  physiographicProvinceTagIds: string[];
  otherTagIds: string[];
};

export async function login(page: Page, email = "admin@planarian.local") {
  await page.goto("/login");
  await page.getByLabel("Email Address").fill(email);
  await page.getByRole("textbox", { name: /Password$/ }).fill("planarian");
  await page.getByRole("button", { name: /Login$/ }).click();
  await expect(page).not.toHaveURL(/\/login(?:\?|$)/);
  await expect(page.locator("body")).not.toContainText("Failed to initialize application");
}

export async function initialize(
  request: APIRequestContext,
  accountId?: string
): Promise<AppInitialize> {
  const response = await request.get(API_ORIGIN + "/api/app/initialize", {
    headers: accountId ? { "x-account": accountId } : undefined,
  });
  expect(response.ok(), await response.text()).toBeTruthy();
  return response.json();
}

export async function accountIdByName(
  request: APIRequestContext,
  name: string
): Promise<string> {
  const init = await initialize(request);
  const item = init.accountIds.find((candidate) => candidate.display === name);
  if (!item) throw new Error("Missing E2E account fixture: " + name);
  return item.value;
}

async function accountHeaders(
  request: APIRequestContext,
  accountId: string
): Promise<Record<string, string>> {
  const init = await initialize(request, accountId);
  if (!init.antiforgeryRequestToken) {
    throw new Error("E2E account initialization did not return an antiforgery token.");
  }
  return {
    "x-account": accountId,
    "X-XSRF-TOKEN": init.antiforgeryRequestToken,
  };
}

export async function getStateId(
  request: APIRequestContext,
  accountId: string
): Promise<string> {
  const response = await request.get(API_ORIGIN + "/api/account/states", {
    headers: { "x-account": accountId },
  });
  expect(response.ok(), await response.text()).toBeTruthy();
  const states = (await response.json()) as SelectItem[];
  const tennessee = states.find((state) => state.display === "TN");
  if (!tennessee) throw new Error("E2E seed is missing Tennessee.");
  return tennessee.value;
}

export async function getTagId(
  request: APIRequestContext,
  accountId: string,
  key: string,
  display?: string
): Promise<string> {
  const response = await request.get(
    API_ORIGIN + "/api/settings/tags?key=" + encodeURIComponent(key),
    { headers: { "x-account": accountId } }
  );
  expect(response.ok(), await response.text()).toBeTruthy();
  const tags = (await response.json()) as SelectItem[];
  const tag = display ? tags.find((item) => item.display === display) : tags[0];
  if (!tag) throw new Error("E2E seed has no " + key + " tag.");
  return tag.value;
}

export async function createCave(
  request: APIRequestContext,
  accountId: string,
  countyId: string,
  name: string,
  options: { pdf?: boolean; narrative?: string } = {}
): Promise<{ caveId: string; fileId?: string }> {
  const stateId = await getStateId(request, accountId);
  const locationQualityTagId = await getTagId(request, accountId, "LocationQuality");
  const headers = await accountHeaders(request, accountId);

  let files: Array<Record<string, unknown>> = [];
  let fileId: string | undefined;
  if (options.pdf) {
    const pdf = await readFile(path.join(__dirname, "fixtures", "browser-e2e.pdf"));
    const staged = await request.post(API_ORIGIN + "/api/files/staged?uuid=browser-e2e-pdf", {
      headers,
      multipart: {
        file: {
          name: "browser-e2e.pdf",
          mimeType: "application/pdf",
          buffer: pdf,
        },
      },
    });
    expect(staged.ok(), await staged.text()).toBeTruthy();
    const file = await staged.json();
    fileId = file.id;
    files = [{
      id: file.id,
      name: file.name,
      extension: file.extension,
      fileTypeTagId: file.fileTypeTagId,
      fileTypeKey: file.fileTypeKey,
    }];
  }

  const payload = {
    name,
    alternateNames: [name + " Alternate"],
    countyId,
    stateId,
    countyDisplayId: null,
    countyNumber: null,
    isCountyNumberManuallySet: false,
    useFirstAvailableCountyNumber: true,
    lengthFeet: 123,
    depthFeet: 45,
    maxPitDepthFeet: 10,
    numberOfPits: 1,
    narrative: options.narrative ?? "Narrative for " + name,
    reportedOn: null,
    entrances: [{
      isPrimary: true,
      locationQualityTagId,
      name: "Main Entrance",
      description: "Browser E2E entrance",
      latitude: 36.16,
      longitude: -86.78,
      elevationFeet: 500,
      reportedOn: null,
      pitFeet: null,
      entranceStatusTagIds: [],
      fieldIndicationTagIds: [],
      entranceHydrologyTagIds: [],
      entranceOtherTagIds: [],
      reportedByNameTagIds: [],
    }],
    geologyTagIds: [],
    files,
    linePlots: [],
    reportedByNameTagIds: [],
    biologyTagIds: [],
    archeologyTagIds: [],
    cartographerNameTagIds: [],
    mapStatusTagIds: [],
    geologicAgeTagIds: [],
    physiographicProvinceTagIds: [],
    otherTagIds: [],
  };

  const response = await request.post(API_ORIGIN + "/api/caves", {
    headers: { ...headers, "Content-Type": "application/json" },
    data: payload,
  });
  expect(response.ok(), await response.text()).toBeTruthy();
  return { caveId: await response.json(), fileId };
}

export async function getCave(
  request: APIRequestContext,
  accountId: string,
  caveId: string
): Promise<CaveRecord> {
  const response = await request.get(API_ORIGIN + "/api/caves/" + caveId, {
    headers: { "x-account": accountId },
  });
  expect(response.ok(), await response.text()).toBeTruthy();
  return response.json();
}

export async function updateCave(
  request: APIRequestContext,
  accountId: string,
  cave: CaveRecord,
  changes: Partial<CaveRecord>
) {
  const headers = await accountHeaders(request, accountId);
  const payload = {
    ...cave,
    ...changes,
    expectedRevisionId: cave.currentRevisionId,
    countyDisplayId: null,
    countyNumber: null,
    isCountyNumberManuallySet: false,
    useFirstAvailableCountyNumber: false,
    linePlots: [],
  };
  const response = await request.put(API_ORIGIN + "/api/caves", {
    headers: { ...headers, "Content-Type": "application/json" },
    data: payload,
  });
  expect(response.ok(), await response.text()).toBeTruthy();
}

export async function switchAccount(
  page: Page,
  accountName: string,
  accountId?: string
) {
  const initialized = accountId
    ? page.waitForResponse(async (response) => {
        if (!response.url().includes("/api/app/initialize") || !response.ok()) {
          return false;
        }

        const session = await response.json() as AppInitialize;
        return session.currentUser?.currentAccountId === accountId;
      })
    : Promise.resolve(undefined);

  await page.locator(".ant-avatar").click();
  await page.getByText("Switch Account", { exact: true }).click();
  const modal = page.getByRole("dialog", { name: "Switch Accounts" });
  await expect(modal).toBeVisible();
  const switching = page.getByRole("status", { name: "Switching accounts" });
  await modal.getByText(accountName, { exact: true }).click();

  const response = await initialized;
  if (response && accountId) {
    const session = await response.json() as AppInitialize;
    expect(session.currentUser?.currentAccountId).toBe(accountId);
  }
  await expect(switching).toBeHidden();
}
