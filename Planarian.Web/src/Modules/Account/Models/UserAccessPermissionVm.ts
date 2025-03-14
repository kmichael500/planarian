import { PermissionKey } from "../../Authentication/Models/PermissionKey";

export interface UserPermissionVm {
  id: string;
  permissionKey: PermissionKey;
  display: string;
  description: string;
}
