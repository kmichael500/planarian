export interface CaveSearchVm {
  id: string;
  name: string;
  reportedOn: string | null;
  isArchived: boolean;
  depthFeet: number | null;
  lengthFeet: number | null;
  maxPitDepthFeet: number | null;
  numberOfPits: number | null;
  countyId: string;
  displayId: string;
  primaryEntranceLatitude: number | null;
  primaryEntranceLongitude: number | null;
  primaryEntranceElevationFeet: number | null;
  archaeologyTagIds: string[];
  biologyTagIds: string[];
  cartographerNameTagIds: string[];
  geologicAgeTagIds: string[];
  geologyTagIds: string[];
  mapStatusTagIds: string[];
  otherTagIds: string[];
  physiographicProvinceTagIds: string[];
  reportedByTagIds: string[];
}
