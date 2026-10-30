export interface FileVm {
  name: string;
  extension: string;
  id: string;
  uuid?: string;
  fileTypeTagId: string;
  // Historical API name: this is the user-facing File tag type name.
  fileTypeKey: string;
}
