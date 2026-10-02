import { matchRoutes } from "react-router-dom";
import { ClientRoutes } from "./ClientRoutes.generated";

describe("generated client routes", () => {
  it("returns declared paths for static routes", () => {
    expect(ClientRoutes.invitationList.get()).toBe(ClientRoutes.invitationList.path);
    expect(ClientRoutes.emailConfirmationPending.get()).toBe(
      ClientRoutes.emailConfirmationPending.path
    );
  });

  it("builds a dynamic path that React Router matches back to the original value", () => {
    const invitationCode = "code/?# ü";
    const builtPath = ClientRoutes.invitation.get(invitationCode);
    const matches = matchRoutes(
      [{ path: ClientRoutes.invitation.path }],
      builtPath
    );

    expect(matches).not.toBeNull();
    expect(matches?.[0].params.invitationCode).toBe(invitationCode);
  });

  it.each([
    ["email confirmation", ClientRoutes.emailConfirmation],
    ["password reset", ClientRoutes.passwordReset],
  ])("builds %s query values that round-trip without changing the pathname", (_, route) => {
    const code = "code/?#&= ü";
    const url = new URL(route.get(code), "https://planarian.test");

    expect(url.pathname).toBe(route.path);
    expect(url.searchParams.get("code")).toBe(code);
  });
});
