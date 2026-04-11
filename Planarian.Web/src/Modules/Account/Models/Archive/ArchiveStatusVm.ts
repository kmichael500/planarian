import { ArchiveProgressVm } from "./ArchiveProgressVm";

export interface ArchiveStatusVm extends ArchiveProgressVm {
  isActive: boolean;
}
