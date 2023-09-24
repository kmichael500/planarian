import { FailedCsvRecord } from "../../Modules/Import/Models/FailedCsvRecord";

export interface ApiErrorResponse {
  message: string;
  errorCode: number;
  data?: any;
}

export interface ImportApiErrorResponse<TRecord> extends ApiErrorResponse {
  data: FailedCsvRecord<TRecord>[];
}
