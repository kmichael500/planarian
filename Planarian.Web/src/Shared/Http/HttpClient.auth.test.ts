import { HttpClient, registerUnauthorizedHandler } from "./HttpClient";
import { ApiExceptionType } from "../Models/ApiErrorResponse";
import { RequestRuntimeState } from "./RequestRuntimeState";

const rejectApiResponse = (status: number, errorCode: ApiExceptionType) =>
  HttpClient.get("api/test/account-access", {
    adapter: async () => Promise.reject({
      response: { status, data: { message: "Request denied", errorCode }, headers: {} },
    }),
  });

const delayedFailure = (status: number, errorCode: ApiExceptionType) => {
  let release!: () => void;
  let started!: () => void;
  let requestAccountId: string | null = null;
  const gate = new Promise<void>((resolve) => { release = resolve; });
  const began = new Promise<void>((resolve) => { started = resolve; });
  const promise = HttpClient.get("api/test/account-access", {
    adapter: async (config) => {
      requestAccountId = config.headers.get("x-account")?.toString() ?? null;
      started();
      await gate;
      throw {
        config,
        response: { status, data: { message: "Request denied", errorCode }, headers: {} },
      };
    },
  });
  return { promise, began, release, requestedAccount: () => requestAccountId };
};

describe("account access failures", () => {
  it.each([
    ["another account", "second0001"],
    ["no account during recovery", null],
  ])("does not recover from an old account response after switching to %s",
    async (_description, newerAccount) => {
      const handler = jest.fn();
      const unregister = registerUnauthorizedHandler(handler);
      RequestRuntimeState.setCurrentAccountId("first00001");
      try {
        const delayed = delayedFailure(403, ApiExceptionType.InvalidAccountSelection);
        await delayed.began;
        expect(delayed.requestedAccount()).toBe("first00001");

        RequestRuntimeState.setCurrentAccountId(newerAccount);
        delayed.release();
        await expect(delayed.promise).rejects.toMatchObject({
          errorCode: ApiExceptionType.InvalidAccountSelection,
        });
        expect(handler).not.toHaveBeenCalled();
      } finally {
        unregister();
        RequestRuntimeState.setCurrentAccountId(null);
      }
    });

  it("still recovers when an invalid-account response matches the selected account", async () => {
    const handler = jest.fn();
    const unregister = registerUnauthorizedHandler(handler);
    RequestRuntimeState.setCurrentAccountId("first00001");
    try {
      const delayed = delayedFailure(403, ApiExceptionType.InvalidAccountSelection);
      await delayed.began;
      delayed.release();
      await expect(delayed.promise).rejects.toMatchObject({
        errorCode: ApiExceptionType.InvalidAccountSelection,
      });
      expect(handler).toHaveBeenCalledWith("invalid-account");
    } finally {
      unregister();
      RequestRuntimeState.setCurrentAccountId(null);
    }
  });

  it("still signals expired authentication from a previously selected account", async () => {
    const handler = jest.fn();
    const unregister = registerUnauthorizedHandler(handler);
    RequestRuntimeState.setCurrentAccountId("first00001");
    try {
      const delayed = delayedFailure(401, ApiExceptionType.Unauthorized);
      await delayed.began;
      RequestRuntimeState.setCurrentAccountId("second0001");
      delayed.release();
      await expect(delayed.promise).rejects.toMatchObject({
        errorCode: ApiExceptionType.Unauthorized,
      });
      expect(handler).toHaveBeenCalledWith("unauthorized");
    } finally {
      unregister();
      RequestRuntimeState.setCurrentAccountId(null);
    }
  });

  it("preserves an explicitly pinned batch account instead of replacing it with current selection", async () => {
    RequestRuntimeState.setCurrentAccountId("second0001");
    const seen: Array<string | undefined> = [];
    try {
      await HttpClient.post("api/test/bulk", {}, {
        headers: { "x-account": "first00001" },
        adapter: async (config) => {
          seen.push(config.headers.get("x-account")?.toString());
          return { config, status: 200, statusText: "OK", headers: {}, data: {} };
        },
      });
      expect(seen).toEqual(["first00001"]);
    } finally {
      RequestRuntimeState.setCurrentAccountId(null);
    }
  });

  it("signals an invalid account independently from expired authentication", async () => {
    const handler = jest.fn();
    const unregister = registerUnauthorizedHandler(handler);
    try {
      await expect(rejectApiResponse(403, ApiExceptionType.InvalidAccountSelection))
        .rejects.toMatchObject({ errorCode: ApiExceptionType.InvalidAccountSelection });

      expect(handler).toHaveBeenCalledTimes(1);
      expect(handler).toHaveBeenCalledWith("invalid-account");
    } finally {
      unregister();
    }
  });

  it("does not interpret ordinary permission denial as lost account access", async () => {
    const handler = jest.fn();
    const unregister = registerUnauthorizedHandler(handler);
    try {
      await expect(rejectApiResponse(403, ApiExceptionType.Forbidden))
        .rejects.toMatchObject({ errorCode: ApiExceptionType.Forbidden });

      expect(handler).not.toHaveBeenCalled();
    } finally {
      unregister();
    }
  });

  it("signals genuine authentication failures separately", async () => {
    const handler = jest.fn();
    const unregister = registerUnauthorizedHandler(handler);
    try {
      await expect(rejectApiResponse(401, ApiExceptionType.Unauthorized))
        .rejects.toMatchObject({ errorCode: ApiExceptionType.Unauthorized });

      expect(handler).toHaveBeenCalledWith("unauthorized");
    } finally {
      unregister();
    }
  });
});
