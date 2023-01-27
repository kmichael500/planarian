import { HttpClient } from "../../../..";
import { RegisterUserVm } from "../../Models/RegisterUserVm";

const baseUrl = "api/register";
const RegisterService = {
  async RegisterUser(user: RegisterUserVm): Promise<void> {
    const response = await HttpClient.post<void>(`${baseUrl}`, user);
    return response.data;
  },
};

export { RegisterService };
