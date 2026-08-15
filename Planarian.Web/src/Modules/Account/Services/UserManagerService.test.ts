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
});
