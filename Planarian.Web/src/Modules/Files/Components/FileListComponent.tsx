import { FileVm } from "../Models/FileVm";

export interface FileListComponentProps {
  files: FileVm[];
  isLoading: boolean;
  updateFiles?: () => void;
}
