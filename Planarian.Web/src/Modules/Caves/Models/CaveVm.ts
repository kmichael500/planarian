import { FileVm } from "../../Files/Models/FileVm";
import { EntranceVm } from "./EntranceVm";

export interface CaveVm {
  id: string;
  currentRevisionId: string | null;
  displayId: string;
  reportedByUserId: string | null;
  countyId: string;
  stateId: string;
  countyDisplayId: string;
  countyIdDelimiter?: string | null;
  countyNumber: number;
  name: string;
  alternateNames: string[];
  lengthFeet: number | null;
  depthFeet: number | null;
  maxPitDepthFeet: number | null;
  numberOfPits: number | null;
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
