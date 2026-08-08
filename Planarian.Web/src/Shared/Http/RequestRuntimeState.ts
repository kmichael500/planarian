let currentAccountId: string | null = null;
let antiforgeryRequestToken: string | null = null;

export const RequestRuntimeState = {
  getCurrentAccountId(): string | null {
    return currentAccountId;
  },
  setCurrentAccountId(accountId: string | null): void {
    currentAccountId = accountId;
  },
  getAntiforgeryRequestToken(): string | null {
    return antiforgeryRequestToken;
  },
  setAntiforgeryRequestToken(token: string | null): void {
    antiforgeryRequestToken = token;
  },
};
