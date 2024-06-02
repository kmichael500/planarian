export interface CaveDryRunRecord {
  id: string;
  stateName: string;
  countyName: string;
  countyCode: string;
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
  geologyTagNames: string[];
  reportedByNameTagNames: string[];
  biologyTagNames: string[];
  archeologyTagNames: string[];
  cartographerNameTagNames: string[];
  geologicAgeTagNames: string[];
  physiographicProvinceTagNames: string[];
  otherTagNames: string[];
  mapStatusTagNames: string[];
}
