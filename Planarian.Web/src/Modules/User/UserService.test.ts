import { HttpClient } from "../../Shared/Http/HttpClient";
import { UserService } from "./UserService";

describe("UserService", () => {
  afterEach(() => {
    jest.restoreAllMocks();
  });

  it("posts confirmation resend requests with the email in the JSON body", async () => {
    const post = jest.spyOn(HttpClient, "post").mockResolvedValue({} as any);

    await UserService.ResendEmailConfirmation("user@example.com");

    expect(post).toHaveBeenCalledTimes(1);
    expect(post).toHaveBeenCalledWith("api/users/confirm-email/resend", {
      emailAddress: "user@example.com",
    });
  });
});
