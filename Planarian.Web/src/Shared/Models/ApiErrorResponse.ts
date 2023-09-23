export interface ApiErrorResponse {
  message: string;
  errorCode: number;
  data?: any;
}

export interface ImportApiErrorResponse<TRecord> extends ApiErrorResponse {
  data: FailedCsvRecord<TRecord>[];
}

export interface FailedCsvRecord<TRecord> {
  caveCsvModel: TRecord;
  rowNumber: number;
  reason: string;
}

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

export interface EntranceCsvModel {
  countyCaveNumber: string;
  entranceName: string | null;
  entranceDescription: string | null;
  isPrimaryEntrance: boolean | null;
  entrancePitDepth: number | null;
  entranceStatus: string | null;
  entranceHydrology: string | null;
  entranceHydrologyFrequency: string | null;
  fieldIndication: string | null;
  countyCode: string | null;
  decimalLatitude: number | null;
  decimalLongitude: number | null;
  entranceElevationFt: number | null;
  geologyFormation: string | null;
  reportedOnDate: string | null;
  reportedByName: string | null;
  locationQuality: string | null;
  caveId: string | null;
}
