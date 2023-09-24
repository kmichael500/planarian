export interface FailedCsvRecord<TRecord> {
  caveCsvModel: TRecord;
  rowNumber: number;
  reason: string;
}
