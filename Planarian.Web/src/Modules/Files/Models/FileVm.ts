import { FileTypeKey } from "./FileTypeKey";

export interface FileVm {
  fileName: string;
  displayName: string | null;
  id: string;
  uuid?: string;
  fileTypeTagId: string;
  fileTypeKey: FileTypeKey;
  embedUrl: string;
  downloadUrl: string;
}
