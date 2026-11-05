import { HttpClient } from "../../../Shared/Http/HttpClient";
import { AccountUserManagerService } from "./UserManagerService";

describe("AccountUserManagerService invitation email history", () => {
  afterEach(() => {
    jest.restoreAllMocks();
  });

  it("loads tracked invitation send attempts for the selected account user", async () => {
    const response = {
      data: [{ messageLogId: "abcdefghij", events: [] }],
    } as any;
    const get = jest.spyOn(HttpClient, "get").mockResolvedValue(response);

    const result = await AccountUserManagerService.GetInvitationEmailHistory(
      "user123456"
    );

    expect(get).toHaveBeenCalledTimes(1);
    expect(get).toHaveBeenCalledWith(
      "api/account/user-manager/user123456/invitation-email-history"
    );
    expect(result).toBe(response.data);
  });
});

describe("AccountUserManagerService access changes", () => {
  afterEach(() => jest.restoreAllMocks());

  it.each(["enable", "disable", "revoke"] as const)(
    "sends a %s action to the shared endpoint with its account pinned",
    async (action) => {
      const response = {
        data: { notificationEmailDeliveryStatus: "Submitted" },
      } as any;
      const post = jest.spyOn(HttpClient, "post").mockResolvedValue(response);

      const result = await AccountUserManagerService.ChangeAccess(
        "user123456",
        "account0001",
        action,
        "My message",
        true
      );

      expect(post).toHaveBeenCalledWith(
        "api/account/user-manager/user123456/access-change",
        { action, notificationMessage: "My message", sendEmail: true },
        { headers: { "x-account": "account0001" } }
      );
      expect(result).toBe(response.data);
    }
  );

  it("omits the message when notifications are disabled", async () => {
    const post = jest
      .spyOn(HttpClient, "post")
      .mockResolvedValue({ data: {} } as any);

    await AccountUserManagerService.ChangeAccess(
      "user123456",
      "account0001",
      "enable",
      "Not to be sent",
      false
    );

    expect(post).toHaveBeenCalledWith(
      "api/account/user-manager/user123456/access-change",
      { action: "enable", notificationMessage: null, sendEmail: false },
      { headers: { "x-account": "account0001" } }
    );
  });
});
