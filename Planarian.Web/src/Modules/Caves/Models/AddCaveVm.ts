import { Dayjs } from "dayjs";
import { GeoJsonUploadVm } from "./GeoJsonUploadVm";
import { EditFileMetadataVm } from "../../Files/Models/EditFileMetadataVm";
import { AddEntranceVm } from "./AddEntranceVm";

export interface AddCaveVm {
  id?: string;
  expectedRevisionId?: string | null;
  name: string;
  alternateNames: string[];
  countyId: string | null;
  stateId: string;
  countyDisplayId?: string | null;
  countyNumber?: number | null;
  isCountyNumberManuallySet: boolean;
  useFirstAvailableCountyNumber: boolean;
  lengthFeet: number | null;
  depthFeet: number | null;
  maxPitDepthFeet: number | null;
  numberOfPits: number | null;
  narrative: string | null;
  reportedOn: Dayjs | null;
  entrances: AddEntranceVm[];
  geologyTagIds: string[];
  files?: EditFileMetadataVm[];
  linePlots?: GeoJsonUploadVm[];
  reportedByNameTagIds: string[];
  biologyTagIds: string[];
  archeologyTagIds: string[];
  cartographerNameTagIds: string[];
  mapStatusTagIds: string[];
  geologicAgeTagIds: string[];
  physiographicProvinceTagIds: string[];
  otherTagIds: string[];
}
