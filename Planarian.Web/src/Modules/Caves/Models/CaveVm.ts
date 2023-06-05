import { EntranceVm } from "./EntranceVm";

export interface CaveVm {
  id: string;
  reportedByUserId: string | null;
  primaryEntranceId: string;
  countyId: string;
  name: string;
  lengthFeet: number;
  depthFeet: number;
  maxPitDepthFeet: number | null;
  numberOfPits: number;
  narrative: string | null;
  reportedOn: string | null;
  reportedByName: string | null;
  isArchived: boolean;
  primaryEntrance: EntranceVm;
  mapIds: string[];
  entranceIds: string[];
  geologyTagIds: string[];
}
