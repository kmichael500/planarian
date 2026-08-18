import { FileTypeKey } from "./FileTypeKey";

export interface FileVm {
  name: string;
  extension: string;
  id: string;
  uuid?: string;
  fileTypeTagId: string;
  fileTypeKey: FileTypeKey;
}
