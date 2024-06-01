import moment from "moment";
import { CaveVm } from "../../Modules/Caves/Models/CaveVm";

export const StringHelpers = {
  GenerateAbbreviation(name: string | undefined): string {
    if (name == undefined) return "";
    return name
      .split(" ")
      .map((namePart) => {
        if (typeof namePart[0] !== undefined) {
          return namePart[0];
        }
      })
      .join("");
  },
};

export function convertDistance(
  distanceInFeet: number | undefined | null
): string {
  if (distanceInFeet === undefined || distanceInFeet === null) return "0 ft";
  const milesThreshold = 2640; // Half a mile in feet (1 mile = 5280 feet)

  const formatOptions = {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  } as Intl.NumberFormatOptions;

  if (distanceInFeet >= milesThreshold) {
    const miles = distanceInFeet / 5280;
    const formattedMiles = miles.toLocaleString(undefined, formatOptions);
    return `${formattedMiles} mi`; // Display miles with 2 decimal places and commas for thousands
  }

  formatOptions.minimumFractionDigits = 0;

  return `${distanceInFeet.toLocaleString(undefined, formatOptions)} ft`; // Add commas for thousands in feet
}

export function displayCountyId(
  countyId: string,
  countyNumber: string
): string {
  return `${countyId} - ${countyNumber}`;
}

export function isNullOrWhiteSpace(
  input: string | null | undefined
): input is null | undefined {
  if (input == null || input == undefined) return true;
  return input.replace(/\s/g, "").length < 1;
}

export function getDirectionsUrl(
  latitude: string | number,
  longitude: string | number
): string {
  return `https://www.google.com/maps/dir/?api=1&destination=${latitude},${longitude}&travelmode=car`;
}

//#region nameof

export const nameof = <T extends object>(name: NestedKeyOf<T>): string => name;

// https://dev.to/pffigueiredo/typescript-utility-keyof-nested-object-2pa3
export type NestedKeyOf<ObjectType extends object> = {
  [Key in keyof ObjectType & (string | number)]: ObjectType[Key] extends object
    ? `${Key}` | `${Key}.${NestedKeyOf<ObjectType[Key]>}`
    : `${Key}`;
}[keyof ObjectType & (string | number)];

//#endregion
export function formatPhoneNumber(phoneNumber?: string): string {
  const formattedPhoneNumber = phoneNumber?.replace(
    /^(\+1)(\d{3})(\d{3})(\d{4})$/,
    "$1 ($2) $3-$4"
  );
  return formattedPhoneNumber ?? "";
}

export function formatDateTime(
  date: Date | string | null | undefined,
  formatString: string = "YYYY MMM-DD"
): string | null {
  if (typeof date === "string" && isNullOrWhiteSpace(date)) return null;
  if (date === null || date === undefined) return null;

  // Check if the date follows the pattern "YYYY-01-01 00:00:00+00:00"
  const yearPattern = /(\d{4})-01-01 00:00:00\+00:00/;

  const match = yearPattern.exec(date.toString());
  if (match) {
    // Format only the year part if the pattern matches
    return moment(match[1], "YYYY").format(formatString);
  }

  return moment(date).format(formatString);
}

export function formatNumber(value: number | null | undefined): string | null {
  if (value !== null && value !== undefined) {
    return value.toLocaleString();
  }
  return null;
}

export function splitCamelCase(input: string): string {
  const result = input.replace(/([a-z])([A-Z])/g, "$1 $2");
  return result;
}

export function defaultIfEmpty(value: string | null | undefined) {
  if (isNullOrWhiteSpace(value)) {
    return "-";
  } else return value;
}

type AbbreviationOptions = {
  delimiter?: string;
  maxLength?: number;
};
