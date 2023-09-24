import { HttpClient } from "../../..";

const baseUrl = "api/account";
const AccountService = {
  async ResetAccount() {
    await HttpClient.delete(`${baseUrl}/reset`);
  },
};
export { AccountService };
