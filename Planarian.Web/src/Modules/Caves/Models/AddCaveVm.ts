import { EditFileMetadataVm } from "../../Files/Models/EditFileMetadataVm";
import { AddEntranceVm } from "./AddEntranceVm";

export interface AddCaveVm {
  id?: string;
  name: string;
  countyId: string | null;
  stateId: string;
  lengthFeet: number;
  depthFeet: number;
  maxPitDepthFeet: number;
  numberOfPits: number;
  narrative: string | null;
  reportedOn: string | null;
  reportedByName: string | null;
  entrances: AddEntranceVm[];
  geologyTagIds: string[];
  files?: EditFileMetadataVm[];
}
