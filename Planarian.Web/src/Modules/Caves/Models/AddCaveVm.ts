import { AddEntranceVm } from "./AddEntranceVm";

export interface AddCaveVm {
  name: string;
  countyId: string;
  lengthFeet: number;
  depthFeet: number;
  maxPitDepthFeet: number | null;
  numberOfPits: number;
  narrative: string | null;
  reportedOn: string | null;
  reportedByName: string | null;
  entrances: AddEntranceVm[];
  geologyTagIds: string[];
}
