import { HttpClient } from "../../../Shared/Http/HttpClient";
import { AccountUserManagerService } from "./UserManagerService";

describe("AccountUserManagerService invitation email history", () => {
  afterEach(() => {
    jest.restoreAllMocks();
  });

  it("loads tracked invitation send attempts for the selected account user", async () => {
    const response = { data: [{ messageLogId: "abcdefghij", events: [] }] } as any;
    const get = jest.spyOn(HttpClient, "get").mockResolvedValue(response);

    const result = await AccountUserManagerService.GetInvitationEmailHistory("user123456");

    expect(get).toHaveBeenCalledTimes(1);
    expect(get).toHaveBeenCalledWith(
      "api/account/user-manager/user123456/invitation-email-history"
    );
    expect(result).toBe(response.data);
  });

  it("sends the revocation reason in the account-access mutation body", async () => {
    const response = {
      data: { notificationEmailDeliveryStatus: "Submitted" },
    } as any;
    const remove = jest.spyOn(HttpClient, "delete").mockResolvedValue(response);

    const result = await AccountUserManagerService.RevokeAccess("user123456", {
      reason: "Membership expired.",
    });

    expect(remove).toHaveBeenCalledWith(
      "api/account/user-manager/user123456",
      { data: { reason: "Membership expired." } }
    );
    expect(result).toBe(response.data);
  });

  it("returns restore notification delivery state", async () => {
    const response = {
      data: { notificationEmailDeliveryStatus: "SendFailed" },
    } as any;
    const post = jest.spyOn(HttpClient, "post").mockResolvedValue(response);

    const result = await AccountUserManagerService.RestoreAccess("user123456");

    expect(post).toHaveBeenCalledWith(
      "api/account/user-manager/user123456/restore-access",
      {}
    );
    expect(result).toBe(response.data);
  });
});
