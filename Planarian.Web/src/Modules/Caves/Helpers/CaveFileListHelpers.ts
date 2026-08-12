import { EditFileMetadataVm } from "../../Files/Models/EditFileMetadataVm";

export const fileAtFormListIndex = (
  files: EditFileMetadataVm[],
  fieldName: number
): EditFileMetadataVm | undefined => files[fieldName];
