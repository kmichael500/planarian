import { FailedCsvRecord } from "../../Modules/Import/Models/FailedCsvRecord";

export interface ApiErrorResponse {
  message: string;
  errorCode: ApiExceptionType;
  data?: any;
}

export interface ImportApiErrorResponse<TRecord> extends ApiErrorResponse {
  data: FailedCsvRecord<TRecord>[];
}

export enum ApiExceptionType {
  BadRequest = "BadRequest",
  Unauthorized = "Unauthorized",
  Forbidden = "Forbidden",
  NotFound = "NotFound",
  Conflict = "Conflict",
  InternalServerError = "InternalServerError",
  EmailAlreadyExists = "EmailAlreadyExists",
  InvalidPasswordComplexity = "InvalidPasswordComplexity",
  InvalidPhoneNumber = "InvalidPhoneNumber",
  InvalidEmailConfirmationCode = "InvalidEmailConfirmationCode",
  EmailNotConfirmed = "EmailNotConfirmed",
  EmailDoesNotExist = "EmailDoesNotExist",
  InvalidPassword = "InvalidPassword",
  PasswordResetCodeExpired = "PasswordResetCodeExpired",
  InvalidPasswordResetCode = "InvalidPasswordResetCode",
  MessageTypeNotFound = "MessageTypeNotFound",
  EmailFailedToSend = "EmailFailedToSend",
  NoAccount = "NoAccount",
  EntranceRequired = "EntranceRequired",
  InvalidImport = "InvalidImport",
  NullValue = "NullValue",
  QueryInvalidValue = "QueryInvalidValue",
}
