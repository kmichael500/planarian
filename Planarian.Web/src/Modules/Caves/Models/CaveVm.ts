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
  reportedByName: string | null;
  isArchived: boolean;
  primaryEntrance: EntranceVm;
  mapIds: string[];
  entrances: EntranceVm[];
  geologyTagIds: string[];

  MaxEntranceElevationFeet?: number;
  MinEntranceElevationFeet?: number;
}
