import { ApiExceptionType } from "../../../Shared/Models/ApiErrorResponse";
import { ImportFileSettings } from "../Components/ImportFileSettingsForm";

const FILENAME_REGEX_MISMATCH_MESSAGE =
  "The filename does not match the provided regex pattern.";
const INVALID_ID_CAVE_NUMBER_MESSAGE =
  "The ID does not contain a valid county cave number.";
const INT32_MIN = -2147483648;
const INT32_MAX = 2147483647;

const isValidInt32 = (value: string) => {
  const trimmedValue = value.trim();
  if (!/^[+-]?\d+$/.test(trimmedValue)) {
    return false;
  }

  const parsedValue = Number(trimmedValue);
  return (
    Number.isInteger(parsedValue) &&
    parsedValue >= INT32_MIN &&
    parsedValue <= INT32_MAX
  );
};

export const validateImportFileName = (
  fileName: string,
  settings: ImportFileSettings
) => {
  let regex: RegExp;
  try {
    regex = new RegExp(settings.idRegex);
  } catch {
    return true;
  }

  const match = regex.exec(fileName);
  if (!match) {
    return {
      message: FILENAME_REGEX_MISMATCH_MESSAGE,
      failureCode: ApiExceptionType.BadRequest,
      terminalStatus: "failed" as const,
    };
  }

  const id = match[0];
  if (!settings.delimiter?.trim()) {
    const splitIndex = id.search(/\d/);
    const caveNumberText = splitIndex === -1 ? "" : id.substring(splitIndex);
    if (splitIndex === -1 || !isValidInt32(caveNumberText)) {
      return {
        message: INVALID_ID_CAVE_NUMBER_MESSAGE,
        failureCode: ApiExceptionType.BadRequest,
        terminalStatus: "failed" as const,
      };
    }

    return true;
  }

  const parts = id.split(settings.delimiter);
  if (parts.length !== 2 || !isValidInt32(parts[1])) {
    return {
      message: `'${fileName}' does not contain a valid county cave number.`,
      failureCode: ApiExceptionType.BadRequest,
      terminalStatus: "failed" as const,
    };
  }

  return true;
};
