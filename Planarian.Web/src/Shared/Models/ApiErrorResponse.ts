export interface ApiErrorResponse {
  message: string;
  errorCode: number;
  data?: any;
}

export interface ImportApiErrorResponse extends ApiErrorResponse {
  data: FailedCaveRecord[];
}

export interface FailedCaveRecord {
  caveCsvModel: CaveCsvModel;
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
