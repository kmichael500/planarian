import { HttpClient } from "../../../../Shared/Http/HttpClient";
import { RegisterUserVm } from "../../Models/RegisterUserVm";
import { RegisterUserResultVm } from "../../Models/RegisterUserResultVm";

const baseUrl = "api/register";
const RegisterService = {
  async RegisterUser(user: RegisterUserVm): Promise<RegisterUserResultVm> {
    const response = await HttpClient.post<RegisterUserResultVm>(`${baseUrl}`, user);
    return response.data;
  },
};

export { RegisterService };
