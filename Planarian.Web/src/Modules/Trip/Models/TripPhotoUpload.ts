import { RcFile } from "antd/lib/upload";
import { PhotoMetaData } from "../../Photo/Models/PhotoMetaData";

export interface TripPhotosUpload {
  photos: TripPhotoUpload[];
}

export interface TripPhotoUpload extends PhotoMetaData {
  file: RcFile | null | undefined;
}
