import { HttpClient } from "../../..";
import { UserLoginVm } from "../Models/UserLoginVm";

const baseUrl = "api/projects";
const AuthenticationService = {
  /**
   *
   * @param project
   * @returns Id of the project.
   */
  async Login(values: UserLoginVm): Promise<string> {
    const response = await HttpClient.post<string>(`${baseUrl}/`, values);
    return response.data;
  },
};

export { AuthenticationService };
