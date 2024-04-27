import { FileVm } from "../../Files/Models/FileVm";
import { EntranceVm } from "./EntranceVm";

export interface CaveVm {
  id: string;
  displayId: string;
  reportedByUserId: string | null;
  countyId: string;
  stateId: string;
  name: string;
  lengthFeet: number;
  depthFeet: number;
  maxPitDepthFeet: number;
  numberOfPits: number;
  narrative: string | null;
  reportedOn: string | null;
  isArchived: boolean;
  primaryEntrance: EntranceVm | null;
  mapIds: string[];
  entrances: EntranceVm[];
  geologyTagIds: string[];
  files: FileVm[];
  reportedByNameTagIds: string[];
  biologyTagIds: string[];
  archeologyTagIds: string[];
  cartographerNameTagIds: string[];
  mapStatusTagIds: string[];
  geologicAgeTagIds: string[];
  physiographicProvinceTagIds: string[];
  otherTagIds: string[];
}
