import { RcFile } from "antd/lib/upload";
import { PhotoMetaData } from "./PhotoMetaData";

export interface TripPhotosUpload {
  photos: TripPhotoUpload[];
}

export interface TripPhotoUpload extends PhotoMetaData {
  file: RcFile | null | undefined;
}
