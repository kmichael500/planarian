import { HttpClient } from "../../../Shared/Http/HttpClient";
import { RequestRuntimeState } from "../../../Shared/Http/RequestRuntimeState";
import { SettingsService } from "./SettingsService";

const deferred = <T,>() => {
  let resolve!: (value: T) => void;
  const promise = new Promise<T>((res) => {
    resolve = res;
  });
  return { promise, resolve };
};

describe("SettingsService account-scoped cache isolation", () => {
  beforeEach(() => {
    RequestRuntimeState.setCurrentAccountId(null);
    jest.restoreAllMocks();
  });

  afterEach(() => {
    RequestRuntimeState.setCurrentAccountId(null);
  });

  test("does not reuse county metadata from another account", async () => {
    const get = jest.spyOn(HttpClient, "get")
      .mockResolvedValueOnce({ data: { displayId: "A", countyIdDelimiter: "-" } } as never)
      .mockResolvedValueOnce({ data: { displayId: "B", countyIdDelimiter: "/" } } as never);

    RequestRuntimeState.setCurrentAccountId("account-a");
    expect(await SettingsService.GetCountyMetadata("county-1")).toMatchObject({ displayId: "A" });

    RequestRuntimeState.setCurrentAccountId("account-b");
    expect(await SettingsService.GetCountyMetadata("county-1")).toMatchObject({ displayId: "B" });

    expect(get).toHaveBeenCalledTimes(2);
  });

  test.each([
    ["state", () => SettingsService.GetStateName("state-1")],
    ["county", () => SettingsService.GetCountyName("county-1")],
    ["tag", () => SettingsService.GetTagName("tag-1")],
  ])("keeps %s display-name caches isolated by account", async (_name, load) => {
    const get = jest.spyOn(HttpClient, "get")
      .mockResolvedValueOnce({ data: "Account A value" } as never)
      .mockResolvedValueOnce({ data: "Account B value" } as never);

    RequestRuntimeState.setCurrentAccountId("account-a");
    expect(await load()).toBe("Account A value");

    RequestRuntimeState.setCurrentAccountId("account-b");
    expect(await load()).toBe("Account B value");

    RequestRuntimeState.setCurrentAccountId("account-a");
    expect(await load()).toBe("Account A value");
    expect(get).toHaveBeenCalledTimes(2);
  });

  test("a late Account A response cannot replace Account B's cached metadata", async () => {
    const a = deferred<{ data: { displayId: string; countyIdDelimiter: string } }>();
    const b = deferred<{ data: { displayId: string; countyIdDelimiter: string } }>();
    jest.spyOn(HttpClient, "get")
      .mockImplementationOnce(() => a.promise as never)
      .mockImplementationOnce(() => b.promise as never);

    RequestRuntimeState.setCurrentAccountId("account-a");
    const loadA = SettingsService.GetCountyMetadata("county-late");

    RequestRuntimeState.setCurrentAccountId("account-b");
    const loadB = SettingsService.GetCountyMetadata("county-late");

    b.resolve({ data: { displayId: "B", countyIdDelimiter: "/" } });
    expect(await loadB).toMatchObject({ displayId: "B" });

    a.resolve({ data: { displayId: "A", countyIdDelimiter: "-" } });
    expect(await loadA).toMatchObject({ displayId: "A" });

    RequestRuntimeState.setCurrentAccountId("account-b");
    expect(await SettingsService.GetCountyMetadata("county-late")).toMatchObject({ displayId: "B" });
    expect(HttpClient.get).toHaveBeenCalledTimes(2);
  });
});
