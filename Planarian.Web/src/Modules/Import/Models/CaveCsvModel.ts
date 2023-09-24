export interface CaveCsvModel {
  caveName: string;
  caveLengthFt: number;
  caveDepthFt: number;
  maxPitDepthFt: number;
  numberOfPits: number;
  narrative: string;
  countyCode: string;
  countyName: string;
  countyCaveNumber: number;
  state: string;
  geology: string;
  reportedOnDate: string;
  reportedByName: string;
  isArchived: boolean;
}
