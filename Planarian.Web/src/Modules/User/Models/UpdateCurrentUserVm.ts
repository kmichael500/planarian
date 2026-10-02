import { UserVm } from "./UserVm";

export interface UpdateCurrentUserVm extends UserVm {
  currentPassword?: string;
}
