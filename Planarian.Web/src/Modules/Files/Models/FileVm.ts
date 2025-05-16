import { FileTypeKey } from "./FileTypeKey";

export interface FileVm {
  id: string;

  displayName: string | null;
  fileTypeTagId: string;
  fileTypeKey: FileTypeKey;
  isNew: boolean;

  fileName?: string;
  uuid?: string;
  embedUrl?: string;
  downloadUrl?: string;
}
