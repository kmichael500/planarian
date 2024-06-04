export interface CaveDryRunRecord {
  state: string;
  countyName: string;
  countyCode: string;
  countyCaveNumber: number;
  caveName: string;
  alternateNames: string[];
  caveLengthFeet: number | null;
  caveDepthFeet: number | null;
  maxPitDepthFeet: number | null;
  numberOfPits: number | null;
  narrative: string | null;
  reportedOnDate: string | null;
  isArchived: boolean;
  geology: string[];
  reportedByNames: string[];
  biology: string[];
  archeology: string[];
  cartographerNames: string[];
  geologicAges: string[];
  physiographicProvinces: string[];
  otherTags: string[];
  mapStatuses: string[];
}
