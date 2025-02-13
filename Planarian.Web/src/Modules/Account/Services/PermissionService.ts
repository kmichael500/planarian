import { HttpClient } from "../../../index"; // or wherever you import
import { UserLocationPermissionsVm } from "../Models/UserLocationPermissionsVm";

export class UserLocationPermissionService {
  public static async getLocationPermissions(
    userId: string
  ): Promise<UserLocationPermissionsVm> {
    const response = await HttpClient.get<UserLocationPermissionsVm>(
      `api/users/${userId}/locationPermissions`
    );
    return response.data;
  }

  public static async updateLocationPermissions(
    userId: string,
    newPermissions: UserLocationPermissionsVm
  ): Promise<void> {
    await HttpClient.put<void>(
      `api/users/${userId}/locationPermissions`,
      newPermissions
    );
  }
}
