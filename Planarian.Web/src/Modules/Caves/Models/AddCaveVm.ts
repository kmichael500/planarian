import { EditFileMetadataVm } from "../../Files/Models/EditFileMetadataVm";
import { AddEntranceVm } from "./AddEntranceVm";

export interface AddCaveVm {
  id?: string;
  name: string;
  alternateNames: string[];
  countyId: string | null;
  stateId: string;
  lengthFeet: number;
  depthFeet: number;
  maxPitDepthFeet: number;
  numberOfPits: number;
  narrative: string | null;
  reportedOn: string | null;
  entrances: AddEntranceVm[];
  geologyTagIds: string[];
  files?: EditFileMetadataVm[];
  reportedByNameTagIds: string[];
  biologyTagIds: string[];
  archeologyTagIds: string[];
  cartographerNameTagIds: string[];
  mapStatusTagIds: string[];
  geologicAgeTagIds: string[];
  physiographicProvinceTagIds: string[];
  otherTagIds: string[];
}
