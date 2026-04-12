import { ProgressState } from "./ProgressState";

export interface ProgressVm {
  statusMessage: string;
  processedCount?: number;
  totalCount?: number;
  state?: ProgressState;
}
